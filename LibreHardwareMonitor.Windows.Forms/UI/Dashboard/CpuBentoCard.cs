// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>Processor card: package KPIs and a compact per-thread load matrix.</summary>
public sealed class CpuBentoCard : BentoCardBase
{
    private const string CorePrefix = "CPU Core #";
    private static readonly string[] PackageTemperatureNames = { "Package", "Tctl", "Tdie" };

    private readonly List<float> _coreLoads = new();
    private float? _packageTemp;
    private string _tempLabel = "Temp. Package";
    private float? _totalLoad;
    private float? _packagePower;
    private float? _peakClock;

    public CpuBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Cpu);
        Title = "Processador (CPU)";
        Subtitle = "Telemetria e matriz de núcleos";
    }

    private int MatrixColumns => _coreLoads.Count > 16 ? 5 : 4;

    private int MatrixRows => Math.Max(1, (_coreLoads.Count + MatrixColumns - 1) / MatrixColumns);

    // Two KPI rows, the matrix caption and the matrix itself.
    protected override int SummaryHeight => 104 + 14 + 18 + (MatrixRows * 28);

    // The per-thread loads are already plotted by the matrix.
    protected override bool IncludeDetail(ISensor sensor) => !(sensor.SensorType == SensorType.Load && sensor.Name.StartsWith(CorePrefix));

    public void UpdateSensors(IHardware cpu)
    {
        if (cpu == null)
            return;

        Title = cpu.Name;
        ISensor temperature = SensorLookup.CpuTemperature(cpu);
        _packageTemp = temperature?.Value;
        // A fallback sensor (no Package/Tctl/Tdie/Core Max on this CPU) is named instead of being called "Package".
        _tempLabel = temperature == null || SensorLookup.NameContainsAny(temperature, PackageTemperatureNames) ? "Temp. Package" : temperature.Name;
        _totalLoad = SensorLookup.Find(cpu, SensorType.Load, "Total")?.Value;
        _packagePower = SensorLookup.FindOrFirst(cpu, SensorType.Power, "Package")?.Value;

        _peakClock = null;
        _coreLoads.Clear();
        foreach (ISensor s in cpu.Sensors)
        {
            if (s.SensorType == SensorType.Load && s.Name.StartsWith(CorePrefix))
                _coreLoads.Add(s.Value.GetValueOrDefault());
            else if (s.SensorType == SensorType.Clock && !SensorLookup.IsBusClock(s) && s.Value > _peakClock.GetValueOrDefault())
                _peakClock = s.Value;
        }

        SetBadge(_packageTemp, 70f, 85f, "Normal", "Quente", "Crítico");

        BeginDetails();
        AddDetails(cpu);
        EndUpdate();
    }

    protected override void DrawSummary(Graphics g, Theme theme, Rectangle area)
    {
        Color? tempColor = StateColor(theme, _packageTemp, 70f, 85f);
        DrawKpiBox(g, KpiTile(area, 0), _tempLabel,_packageTemp?.ToString("F1"), "°C", _packageTemp / 100f, tempColor, theme);
        DrawKpiBox(g, KpiTile(area, 1), "Carga Total", _totalLoad?.ToString("F1"), "%", _totalLoad / 100f, null, theme);
        DrawKpiBox(g, KpiTile(area, 2), "Consumo Package", _packagePower?.ToString("F1"), "W", null, null, theme);
        DrawKpiBox(g, KpiTile(area, 3), "Clock Pico", _peakClock?.ToString("F0"), "MHz", null, null, theme);

        int matrixY = area.Y + LogicalToDeviceUnits(104 + 14);
        DrawSectionTitle(g, _coreLoads.Count > 0 ? $"MATRIZ DE NÚCLEOS ({_coreLoads.Count} THREADS)" : "MATRIZ DE NÚCLEOS", area, matrixY, theme);
        if (_coreLoads.Count == 0)
            return;

        int columns = MatrixColumns;
        int gap = LogicalToDeviceUnits(4);
        int cellWidth = (area.Width - ((columns - 1) * gap)) / columns;
        int cellHeight = LogicalToDeviceUnits(24);
        int gridY = matrixY + LogicalToDeviceUnits(18);
        if (cellWidth <= 8)
            return;

        Color cellFill = FluentStyle.Blend(theme.SurfaceColor, theme.SubtleSurfaceColor, 0.55);
        using SolidBrush track = new(Color.FromArgb(30, theme.MutedForegroundColor));
        using SolidBrush fill = new(theme.AccentColor);

        for (int i = 0; i < _coreLoads.Count; i++)
        {
            float load = _coreLoads[i];
            Rectangle cell = new(area.X + (i % columns * (cellWidth + gap)), gridY + (i / columns * (cellHeight + gap)), cellWidth, cellHeight);
            FluentStyle.FillRounded(g, cellFill, cell, LogicalToDeviceUnits(4));

            TextRenderer.DrawText(g, $"C{i}", FluentStyle.UiFontMini, new Rectangle(cell.X + 4, cell.Y + 2, cellWidth / 2, LogicalToDeviceUnits(12)), theme.MutedForegroundColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, $"{load:F0}%", FluentStyle.MonoFontSmall, new Rectangle(cell.X + (cellWidth / 2), cell.Y + 2, (cellWidth / 2) - 4, LogicalToDeviceUnits(12)), theme.ForegroundColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            // Silent at idle: the 2px bar only appears once the thread carries some load.
            if (load <= 1f)
                continue;

            float ratio = Math.Clamp(load / 100f, 0f, 1f);
            int barY = cell.Bottom - LogicalToDeviceUnits(5);
            int barWidth = cellWidth - 12;
            if (barWidth <= 0)
                continue;

            g.FillRectangle(track, cell.X + 6, barY, barWidth, 2);
            fill.Color = ratio > 0.85f ? theme.StateHotColor : ratio > 0.6f ? theme.StateWarmColor : theme.AccentColor;
            g.FillRectangle(fill, cell.X + 6, barY, Math.Max(2, (int)(barWidth * ratio)), 2);
        }
    }
}
