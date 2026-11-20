// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DiskInfoToolkit.Smart;

namespace LibreHardwareMonitor.Windows.Forms.UI.DiskHealth;

/// <summary>
/// One row of the raw S.M.A.R.T. view. <see cref="Flag" /> is null when the value is fine; <see cref="Key" /> marks
/// the rows the verdict is computed from.
/// </summary>
public readonly record struct DiskSmartRow(string[] Cells, DiskFindingSeverity? Flag, bool Key);

/// <summary>The drive's S.M.A.R.T. values as a plain table: ATA attributes or the decoded NVMe health log.</summary>
public sealed class DiskSmartTable
{
    public string[] Headers { get; init; } = Array.Empty<string>();

    /// <summary>Columns (by index) that hold numbers and are right aligned.</summary>
    public bool[] Numeric { get; init; } = Array.Empty<bool>();

    public IReadOnlyList<DiskSmartRow> Rows { get; init; } = Array.Empty<DiskSmartRow>();

    /// <summary>One line explaining how to read the table.</summary>
    public string Note { get; init; }

    /// <summary>Plain text with aligned columns, for the clipboard.</summary>
    public string ToText(DiskHealthReport report)
    {
        StringBuilder sb = new();
        sb.AppendLine(report.Name);
        sb.AppendLine(report.Description);
        sb.AppendLine();

        int[] widths = new int[Headers.Length];
        for (int c = 0; c < Headers.Length; c++)
            widths[c] = Math.Max(Headers[c].Length, Rows.Count == 0 ? 0 : Rows.Max(r => r.Cells[c].Length));

        void Line(string[] cells, string state)
        {
            for (int c = 0; c < cells.Length; c++)
                sb.Append(Numeric[c] ? cells[c].PadLeft(widths[c]) : cells[c].PadRight(widths[c])).Append("  ");
            sb.AppendLine(state);
        }

        Line(Headers, "Estado");
        foreach (DiskSmartRow row in Rows)
        {
            Line(row.Cells, row.Flag switch
            {
                DiskFindingSeverity.Bad => "Mau",
                DiskFindingSeverity.Caution => "Atenção",
                DiskFindingSeverity.Info => "Info",
                _ => "OK"
            });
        }

        if (!string.IsNullOrEmpty(Note))
            sb.AppendLine().AppendLine(Note);

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }
}

/// <summary>Builds <see cref="DiskSmartTable" /> from the data already read by the poller.</summary>
internal static class DiskSmartTableBuilder
{
    // Counters the verdict and the HDD health score are computed from.
    private static readonly HashSet<byte> KeyAttributes = new() { 0x05, 0x0A, 0xBB, 0xC5, 0xC6, 0xC7 };

    public static DiskSmartTable FromAta(SmartAttributeEntry[] attributes, Func<SmartAttributeEntry, string> name)
    {
        if (attributes.Length == 0)
            return null;

        List<DiskSmartRow> rows = new();
        foreach (SmartAttributeEntry a in attributes.OrderBy(a => a.ID))
        {
            ulong raw = a.RawValue & 0xFFFFFFFFFFFF;
            ulong low = a.RawValue & 0xFFFFFFFF;

            DiskFindingSeverity? flag = null;
            if (a.ThresholdValue > 0 && a.CurrentValue > 0 && a.CurrentValue <= a.ThresholdValue)
                flag = DiskFindingSeverity.Bad;
            else if (KeyAttributes.Contains(a.ID) && low > 0)
                flag = a.ID == 0xC7 ? DiskFindingSeverity.Info : DiskFindingSeverity.Caution;

            rows.Add(new DiskSmartRow(new[]
            {
                a.ID.ToString("X2"),
                name(a),
                a.CurrentValue.ToString(),
                a.WorstValue.ToString(),
                a.ThresholdValue.ToString(),
                raw.ToString("X12"),
                Decoded(a.ID, raw, low)
            }, flag, KeyAttributes.Contains(a.ID)));
        }

        return new DiskSmartTable
        {
            Headers = new[] { "ID", "Atributo", "Atual", "Pior", "Limite", "Bruto (hex)", "Valor" },
            Numeric = new[] { false, false, true, true, true, true, true },
            Rows = rows,
            Note = "Atual, Pior e Limite são valores normalizados pelo fabricante (maior é melhor): o atributo falha quando Atual chega ao Limite. Valor é o contador bruto."
        };
    }

    /// <summary>Raw value in its usual unit: temperatures keep only the current reading, totals use 48 bits.</summary>
    private static string Decoded(byte id, ulong raw48, ulong low32) => id switch
    {
        0xBE or 0xC2 => $"{raw48 & 0xFF} °C",
        0xF1 or 0xF2 => raw48.ToString("N0"),
        _ => low32.ToString("N0")
    };

    public static DiskSmartTable FromNvme(byte[] log)
    {
        if (log == null || log.Length < 176)
            return null;

        List<DiskSmartRow> rows = new();
        void Add(string field, string value, DiskFindingSeverity? flag = null, bool key = false) =>
            rows.Add(new DiskSmartRow(new[] { field, value }, flag, key));

        byte warning = log[0];
        Add("Avisos críticos", warning == 0 ? "Nenhum" : $"0x{warning:X2} ({WarningBits(warning)})",
            warning == 0 ? null : (warning & 0x0D) != 0 ? DiskFindingSeverity.Bad : DiskFindingSeverity.Caution, true);

        ushort kelvin = BitConverter.ToUInt16(log, 1);
        Add("Temperatura", kelvin > 0 ? $"{kelvin - 273} °C" : "-");

        byte spare = log[3];
        byte spareThreshold = log[4];
        Add("Reserva disponível", $"{spare}%", spareThreshold > 0 && spare <= spareThreshold ? DiskFindingSeverity.Bad : null, true);
        Add("Limite da reserva", $"{spareThreshold}%");
        Add("Percentagem usada", $"{log[5]}%", log[5] >= 90 ? DiskFindingSeverity.Caution : null, true);

        Add("Dados lidos", DataUnits(BitConverter.ToUInt64(log, 32)));
        Add("Dados escritos", DataUnits(BitConverter.ToUInt64(log, 48)));
        Add("Comandos de leitura", BitConverter.ToUInt64(log, 64).ToString("N0"));
        Add("Comandos de escrita", BitConverter.ToUInt64(log, 80).ToString("N0"));
        Add("Tempo ocupado do controlador", $"{BitConverter.ToUInt64(log, 96):N0} min");
        Add("Ciclos de energia", BitConverter.ToUInt64(log, 112).ToString("N0"));
        Add("Horas ligado", $"{BitConverter.ToUInt64(log, 128):N0} h");
        Add("Desligamentos inseguros", BitConverter.ToUInt64(log, 144).ToString("N0"));

        ulong mediaErrors = BitConverter.ToUInt64(log, 160);
        Add("Erros de integridade de dados", mediaErrors.ToString("N0"), mediaErrors > 0 ? DiskFindingSeverity.Caution : null, true);

        if (log.Length >= 192)
            Add("Entradas no registo de erros", BitConverter.ToUInt64(log, 176).ToString("N0"));

        if (log.Length >= 200)
        {
            Add("Tempo acima da temperatura de aviso", $"{BitConverter.ToUInt32(log, 192):N0} min");
            Add("Tempo acima da temperatura crítica", $"{BitConverter.ToUInt32(log, 196):N0} min");
        }

        if (log.Length >= 216)
        {
            for (int i = 0; i < 8; i++)
            {
                ushort sensor = BitConverter.ToUInt16(log, 200 + (i * 2));
                if (sensor > 0)
                    Add($"Sensor de temperatura {i + 1}", $"{sensor - 273} °C");
            }
        }

        return new DiskSmartTable
        {
            Headers = new[] { "Campo", "Valor" },
            Numeric = new[] { false, true },
            Rows = rows,
            Note = "Registo de saúde NVMe (log 0x02) tal como o disco o reporta. 1 unidade de dados = 512 000 bytes."
        };
    }

    private static string DataUnits(ulong units) => $"{DiskHealthFormat.Data(units * 512000UL)} ({units:N0} unidades)";

    private static string WarningBits(byte warning)
    {
        List<string> parts = new();
        if ((warning & 0x01) != 0) parts.Add("reserva");
        if ((warning & 0x02) != 0) parts.Add("temperatura");
        if ((warning & 0x04) != 0) parts.Add("fiabilidade");
        if ((warning & 0x08) != 0) parts.Add("só leitura");
        if ((warning & 0x10) != 0) parts.Add("memória volátil");
        return parts.Count == 0 ? "outro" : string.Join(", ", parts);
    }
}
