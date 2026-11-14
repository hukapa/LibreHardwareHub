// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>
/// Specialized Bento card for Processors (CPU) displaying 4 vital KPIs
/// and a compact Core Matrix (mini-grelha de núcleos) in place of giant tables.
/// </summary>
public sealed class CpuBentoCard : BentoCardBase
{
    private readonly List<(string Name, float Load, float? Clock)> _cores = new();
    private float? _packageTemp;
    private float? _totalLoad;
    private float? _packagePower;
    private float? _peakClock;

    public CpuBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Cpu);
        Title = "Processador (CPU)";
        Subtitle = "Telemetria e Matriz de Núcleos";
        Height = LogicalToDeviceUnits(260);
    }

    public void UpdateSensors(IHardware cpu)
    {
        if (cpu == null)
            return;

        Title = cpu.Name;

        ISensor temp = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature &&
            (s.Name.Contains("Package") || s.Name.Contains("Tctl") || s.Name.Contains("Core Max")))
            ?? cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
        _packageTemp = temp?.Value;

        ISensor load = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"));
        _totalLoad = load?.Value;

        ISensor power = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name.Contains("Package"))
            ?? cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power);
        _packagePower = power?.Value;

        ISensor clock = cpu.Sensors.Where(s => s.SensorType == SensorType.Clock && !s.Name.Contains("Bus"))
            .OrderByDescending(s => s.Value.GetValueOrDefault())
            .FirstOrDefault();
        _peakClock = clock?.Value;

        if (_packageTemp.HasValue)
        {
            if (_packageTemp.Value >= 85f)
            {
                BadgeText = "Crítico";
                BadgeColor = Color.FromArgb(235, 60, 60);
            }
            else if (_packageTemp.Value >= 70f)
            {
                BadgeText = "Quente";
                BadgeColor = Color.FromArgb(240, 180, 30);
            }
            else
            {
                BadgeText = "Normal";
                BadgeColor = Color.FromArgb(70, 200, 100);
            }
        }

        _cores.Clear();
        var coreLoads = cpu.Sensors.Where(s => s.SensorType == SensorType.Load && s.Name.StartsWith("CPU Core #")).ToList();
        var coreClocks = cpu.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.StartsWith("CPU Core #")).ToList();

        for (int i = 0; i < coreLoads.Count; i++)
        {
            float l = coreLoads[i].Value.GetValueOrDefault();
            float? c = i < coreClocks.Count ? coreClocks[i].Value : null;
            _cores.Add(($"C{i}", l, c));
        }

        int cols = _cores.Count > 16 ? 5 : 4;
        int rows = Math.Max(1, (int)Math.Ceiling(_cores.Count / (double)cols));
        int calculatedHeight = 48 + 6 + (48 * 2) + 8 + 16 + 14 + 6 + (rows * 28) + 16;
        Height = LogicalToDeviceUnits(Math.Max(260, calculatedHeight));

        Invalidate();
    }

    protected override void DrawCardContent(Graphics g, Theme theme)
    {
        int pad = LogicalToDeviceUnits(14);
        int topY = LogicalToDeviceUnits(HeaderHeightLogical) + LogicalToDeviceUnits(6);
        int availWidth = Width - (pad * 2);

        // 1. Top 4 KPI Boxes (2x2)
        int kpiW = (availWidth - LogicalToDeviceUnits(10)) / 2;
        int kpiH = LogicalToDeviceUnits(48);

        Color? tempColor = _packageTemp.HasValue
            ? (_packageTemp.Value >= 85f ? theme.StateHotColor : _packageTemp.Value >= 70f ? theme.StateWarmColor : theme.StateCoolColor)
            : null;

        Rectangle r1 = new(pad, topY, kpiW, kpiH);
        Rectangle r2 = new(pad + kpiW + LogicalToDeviceUnits(10), topY, kpiW, kpiH);
        Rectangle r3 = new(pad, topY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);
        Rectangle r4 = new(pad + kpiW + LogicalToDeviceUnits(10), topY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);

        DrawKpiBox(g, r1, "Temp. Package", _packageTemp.HasValue ? $"{_packageTemp.Value:F0}" : "--", "°C", _packageTemp / 100f, tempColor, theme);
        DrawKpiBox(g, r2, "Carga Total", _totalLoad.HasValue ? $"{_totalLoad.Value:F0}" : "--", "%", _totalLoad / 100f, null, theme);
        DrawKpiBox(g, r3, "Consumo Package", _packagePower.HasValue ? $"{_packagePower.Value:F0}" : "--", "W", null, null, theme);
        DrawKpiBox(g, r4, "Clock Pico", _peakClock.HasValue ? $"{_peakClock.Value:F0}" : "--", "MHz", null, null, theme);

        // 2. Core Matrix (Mini-Grelha de Núcleos)
        int matrixY = topY + (kpiH * 2) + LogicalToDeviceUnits(14);
        string matrixTitle = _cores.Count > 0 ? $"MATRIZ DE NÚCLEOS ({_cores.Count} THREADS)" : "MATRIZ DE NÚCLEOS";
        DrawSectionTitle(g, matrixTitle, pad, matrixY, theme);

        int gridY = matrixY + LogicalToDeviceUnits(16);
        int gridH = Height - gridY - pad;
        if (_cores.Count > 0 && gridH > 10)
        {
            int cols = _cores.Count > 16 ? 5 : 4;
            int rows = Math.Max(1, (int)Math.Ceiling(_cores.Count / (double)cols));
            int cellW = (availWidth - ((cols - 1) * LogicalToDeviceUnits(4))) / cols;
            int cellH = LogicalToDeviceUnits(24);

            if (cellW > 8 && cellH > 8)
            {
                using SolidBrush barBg = new(Color.FromArgb(40, theme.MutedForegroundColor));

                for (int i = 0; i < _cores.Count; i++)
                {
                    int r = i / cols;
                    int c = i % cols;
                    int cx = pad + (c * (cellW + LogicalToDeviceUnits(4)));
                    int cy = gridY + (r * (cellH + LogicalToDeviceUnits(4)));

                    Rectangle cellRect = new(cx, cy, cellW, cellH);
                    using (GraphicsPath cp = FluentStyle.RoundedRectangle(cellRect, 4f))
                    {
                        using SolidBrush cb = new(Color.FromArgb(theme.IsDark ? 20 : 10, theme.ForegroundColor));
                        g.FillPath(cb, cp);
                        using Pen cpen = new(Color.FromArgb(theme.IsDark ? 30 : 15, theme.ForegroundColor), 1f);
                        g.DrawPath(cpen, cp);
                    }

                    var (name, coreLoad, _) = _cores[i];

                    Rectangle nameRect = new(cx + 4, cy + 2, cellW / 2, LogicalToDeviceUnits(12));
                    TextRenderer.DrawText(g, name ?? string.Empty, FluentStyle.UiFontMini, nameRect, theme.MutedForegroundColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    Rectangle pctRect = new(cx + (cellW / 2), cy + 2, (cellW / 2) - 4, LogicalToDeviceUnits(12));
                    TextRenderer.DrawText(g, $"{coreLoad:F0}%", FluentStyle.MonoFontSmall, pctRect, theme.ForegroundColor,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                    int barY = cy + cellH - LogicalToDeviceUnits(4);
                    int barW = Math.Max(2, cellW - 6);
                    g.FillRectangle(barBg, cx + 3, barY, barW, 2);

                    float ratio = Math.Max(0f, Math.Min(1f, coreLoad / 100f));
                    Color coreColor = ratio > 0.85f ? theme.StateHotColor : ratio > 0.6f ? theme.StateWarmColor : theme.AccentColor;
                    using SolidBrush coreBarFill = new(coreColor);
                    int fillW = Math.Max(0, Math.Min(barW, (int)(barW * ratio)));
                    g.FillRectangle(coreBarFill, cx + 3, barY, fillW, 2);
                }
            }
        }
    }
}
