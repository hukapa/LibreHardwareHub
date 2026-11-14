// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>
/// Specialized Bento card for System Memory (RAM) showing usage bars,
/// free memory, swap/pagefile allocation and module thermals.
/// </summary>
public sealed class MemoryBentoCard : BentoCardBase
{
    private float? _usedGb;
    private float? _availableGb;
    private float? _loadPercent;
    private float? _virtualUsedGb;
    private float? _virtualTotalGb;
    private float? _dimmTemp;

    public MemoryBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Memory);
        Title = "Memória do Sistema (RAM)";
        Subtitle = "Alocação Física e Paginação";
        Height = LogicalToDeviceUnits(260);
    }

    public void UpdateSensors(IHardware memory)
    {
        if (memory == null)
            return;

        ISensor used = memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Memory Used"));
        _usedGb = ToGigabytes(used?.Value);

        ISensor avail = memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Memory Available"));
        _availableGb = ToGigabytes(avail?.Value);

        ISensor load = memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Memory"));
        _loadPercent = load?.Value;

        ISensor virtUsed = memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Virtual Memory Used"));
        _virtualUsedGb = ToGigabytes(virtUsed?.Value);

        ISensor virtAvail = memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Virtual Memory Available"));
        float? virtAvailGb = ToGigabytes(virtAvail?.Value);
        if (_virtualUsedGb.HasValue && virtAvailGb.HasValue)
            _virtualTotalGb = _virtualUsedGb.Value + virtAvailGb.Value;

        ISensor temp = memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
        _dimmTemp = temp?.Value;

        if (_loadPercent.HasValue)
        {
            if (_loadPercent.Value >= 90f)
            {
                BadgeText = "Saturada";
                BadgeColor = Color.FromArgb(235, 60, 60);
            }
            else if (_loadPercent.Value >= 75f)
            {
                BadgeText = "Alta";
                BadgeColor = Color.FromArgb(240, 180, 30);
            }
            else
            {
                BadgeText = "Disponível";
                BadgeColor = Color.FromArgb(70, 200, 100);
            }
        }

        Invalidate();
    }

    protected override void DrawCardContent(Graphics g, Theme theme)
    {
        int pad = LogicalToDeviceUnits(14);
        int topY = LogicalToDeviceUnits(HeaderHeightLogical) + LogicalToDeviceUnits(6);
        int availWidth = Width - (pad * 2);

        float totalGb = _usedGb.GetValueOrDefault() + _availableGb.GetValueOrDefault();
        float ratio = _loadPercent.HasValue ? Math.Max(0f, Math.Min(1f, _loadPercent.Value / 100f))
            : (totalGb > 0.1f ? Math.Max(0f, Math.Min(1f, _usedGb.GetValueOrDefault() / totalGb)) : 0f);

        // RAM Usage Bar Section
        DrawSectionTitle(g, "OCUPAÇÃO DE MEMÓRIA FÍSICA", pad, topY, theme);

        string barText = totalGb > 0.1f
            ? $"{_usedGb.GetValueOrDefault():F1} GB usados de {totalGb:F1} GB ({ratio * 100f:F0}%)"
            : $"{_usedGb.GetValueOrDefault():F1} GB";

        Rectangle barTextRect = new(pad, topY, availWidth, LogicalToDeviceUnits(14));
        TextRenderer.DrawText(g, barText, FluentStyle.MonoFont, barTextRect, theme.ForegroundColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        int barY = topY + LogicalToDeviceUnits(18);
        int barH = LogicalToDeviceUnits(8);
        if (availWidth > 0 && barH > 0)
        {
            Rectangle ramBarBounds = new(pad, barY, availWidth, barH);
            Color ramFill = ratio > 0.88f ? theme.StateHotColor : ratio > 0.70f ? theme.StateWarmColor : theme.AccentColor;
            Color ramBg = Color.FromArgb(40, theme.MutedForegroundColor);
            DrawProgressBar(g, ramBarBounds, ratio, ramFill, ramBg);
        }

        // 4 KPI Boxes (2x2)
        int kpiY = barY + barH + LogicalToDeviceUnits(14);
        int kpiW = (availWidth - LogicalToDeviceUnits(10)) / 2;
        int kpiH = LogicalToDeviceUnits(48);

        Rectangle r1 = new(pad, kpiY, kpiW, kpiH);
        Rectangle r2 = new(pad + kpiW + LogicalToDeviceUnits(10), kpiY, kpiW, kpiH);
        Rectangle r3 = new(pad, kpiY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);
        Rectangle r4 = new(pad + kpiW + LogicalToDeviceUnits(10), kpiY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);

        DrawKpiBox(g, r1, "Memória Usada", _usedGb.HasValue ? $"{_usedGb.Value:F1}" : "--", "GB", ratio, null, theme);
        DrawKpiBox(g, r2, "Memória Livre", _availableGb.HasValue ? $"{_availableGb.Value:F1}" : "--", "GB", 1f - ratio, null, theme);

        string virtVal = _virtualUsedGb.HasValue ? $"{_virtualUsedGb.Value:F1}" : "--";
        string dimmVal = _dimmTemp.HasValue ? $"{_dimmTemp.Value:F0} °C" : "Normal";

        DrawKpiBox(g, r3, "Memória Virtual (Swap)", virtVal, "GB", null, null, theme);
        DrawKpiBox(g, r4, "Temperatura Módulos", dimmVal, string.Empty, _dimmTemp / 100f, null, theme);
    }
}
