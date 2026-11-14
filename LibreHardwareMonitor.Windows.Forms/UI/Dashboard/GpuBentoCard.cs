// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>
/// Specialized Bento card for Graphics Cards (GPU) highlighting
/// Core/HotSpot thermals, load, power, fans, VRAM bar, and an expandable detailed sensor list.
/// </summary>
public sealed class GpuBentoCard : BentoCardBase
{
    private readonly List<(string Name, string Value)> _detailedSensors = new();
    private float? _coreTemp;
    private float? _hotspotTemp;
    private float? _coreLoad;
    private float? _power;
    private float? _coreClock;
    private float? _fanRpm;
    private float? _fanPercent;
    private float? _vramUsedGb;
    private float? _vramTotalGb;

    public GpuBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.GpuNvidia);
        Title = "Placa Gráfica (GPU)";
        Subtitle = "Telemetria de Vídeo e VRAM";
        Height = LogicalToDeviceUnits(260);
    }

    public void UpdateSensors(IHardware gpu)
    {
        if (gpu == null)
            return;

        Title = gpu.Name;
        Icon = FluentIcons.ForHardware(gpu.HardwareType);

        ISensor coreTemp = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Core"))
                           ?? gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
        _coreTemp = coreTemp?.Value;

        ISensor hotSpot = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Hot Spot"));
        _hotspotTemp = hotSpot?.Value;

        ISensor load = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && (s.Name.Contains("Core") || s.Name.Contains("D3D 3D")))
                       ?? gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load);
        _coreLoad = load?.Value;

        ISensor power = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && (s.Name.Contains("Package") || s.Name.Contains("GPU")))
                        ?? gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power);
        _power = power?.Value;

        ISensor clock = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core"));
        _coreClock = clock?.Value;

        ISensor fanRpm = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Fan);
        _fanRpm = fanRpm?.Value;

        ISensor fanPct = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Control);
        _fanPercent = fanPct?.Value;

        ISensor vramUsed = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Memory Used"));
        ISensor vramFree = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Memory Free"));
        ISensor vramTotal = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Memory Total"));

        _vramUsedGb = ToGigabytes(vramUsed?.Value);
        float? vramFreeGb = ToGigabytes(vramFree?.Value);
        _vramTotalGb = ToGigabytes(vramTotal?.Value);

        if (!_vramTotalGb.HasValue && _vramUsedGb.HasValue && vramFreeGb.HasValue)
            _vramTotalGb = _vramUsedGb.Value + vramFreeGb.Value;

        if (_coreTemp.HasValue)
        {
            if (_coreTemp.Value >= 83f)
            {
                BadgeText = "Elevada";
                BadgeColor = Color.FromArgb(235, 60, 60);
            }
            else if (_coreTemp.Value >= 70f)
            {
                BadgeText = "Média";
                BadgeColor = Color.FromArgb(240, 180, 30);
            }
            else
            {
                BadgeText = "Fresca";
                BadgeColor = Color.FromArgb(70, 200, 100);
            }
        }

        // Detailed Sensors for expansion
        _detailedSensors.Clear();
        foreach (ISensor s in gpu.Sensors.Where(s => s.Value.HasValue))
        {
            if (s.SensorType == SensorType.Temperature)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F1} °C"));
            else if (s.SensorType == SensorType.Clock)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F0} MHz"));
            else if (s.SensorType == SensorType.Load)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F1} %"));
            else if (s.SensorType == SensorType.Power)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F1} W"));
            else if (s.SensorType == SensorType.Fan)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F0} RPM"));
            else if (s.SensorType == SensorType.Control)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F0} %"));
            else if (s.SensorType == SensorType.Data)
            {
                float? gb = ToGigabytes(s.Value);
                _detailedSensors.Add((s.Name, $"{gb.GetValueOrDefault():F1} GB"));
            }
        }

        int prevHeight = Height;
        RecalculateCardHeight();
        if (Height != prevHeight)
            Parent?.PerformLayout();

        Invalidate();
    }

    protected override void RecalculateCardHeight()
    {
        int baseHeight = 260;
        if (IsExpanded && _detailedSensors.Count > 0)
        {
            int availWidth = Width > 0 ? Width - LogicalToDeviceUnits(28) : LogicalToDeviceUnits(380);
            int detCols = availWidth > LogicalToDeviceUnits(300) ? 2 : 1;
            int extraRows = (int)Math.Ceiling(_detailedSensors.Count / (double)detCols);
            baseHeight += 32 + (extraRows * 22) + 16;
        }

        Height = LogicalToDeviceUnits(baseHeight);
    }

    protected override void DrawCardContent(Graphics g, Theme theme)
    {
        int pad = LogicalToDeviceUnits(14);
        int topY = LogicalToDeviceUnits(HeaderHeightLogical) + LogicalToDeviceUnits(6);
        int availWidth = Width - (pad * 2);

        // 1. Top 4 KPI Boxes (2x2) with 1 decimal precision
        int kpiW = (availWidth - LogicalToDeviceUnits(10)) / 2;
        int kpiH = LogicalToDeviceUnits(48);

        Color? tempColor = _coreTemp.HasValue
            ? (_coreTemp.Value >= 83f ? theme.StateHotColor : _coreTemp.Value >= 70f ? theme.StateWarmColor : theme.StateCoolColor)
            : null;

        string tempVal = _coreTemp.HasValue ? $"{_coreTemp.Value:F1}" : "--";
        if (_hotspotTemp.HasValue)
            tempVal += $" (HS {_hotspotTemp.Value:F1})";

        Rectangle r1 = new(pad, topY, kpiW, kpiH);
        Rectangle r2 = new(pad + kpiW + LogicalToDeviceUnits(10), topY, kpiW, kpiH);
        Rectangle r3 = new(pad, topY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);
        Rectangle r4 = new(pad + kpiW + LogicalToDeviceUnits(10), topY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);

        DrawKpiBox(g, r1, "Temp. Core", tempVal, "°C", _coreTemp / 100f, tempColor, theme);
        DrawKpiBox(g, r2, "Carga Core", _coreLoad.HasValue ? $"{_coreLoad.Value:F1}" : "--", "%", _coreLoad / 100f, null, theme);
        DrawKpiBox(g, r3, "Consumo GPU", _power.HasValue ? $"{_power.Value:F1}" : "--", "W", null, null, theme);
        DrawKpiBox(g, r4, "Clock GPU", _coreClock.HasValue ? $"{_coreClock.Value:F0}" : "--", "MHz", null, null, theme);

        // 2. VRAM Bar Section
        int vramY = topY + (kpiH * 2) + LogicalToDeviceUnits(14);
        DrawSectionTitle(g, "USO DE MEMÓRIA DE VÍDEO (VRAM)", pad, vramY, theme);

        float usedGb = _vramUsedGb.GetValueOrDefault();
        float totalGb = _vramTotalGb.GetValueOrDefault();
        float vramRatio = totalGb > 0.1f ? Math.Max(0f, Math.Min(1f, usedGb / totalGb)) : 0f;

        string vramText = totalGb > 0.1f
            ? $"{usedGb:F1} / {totalGb:F1} GB ({vramRatio * 100f:F1}%)"
            : $"{usedGb:F1} GB";

        Rectangle vramTextRect = new(pad, vramY, availWidth, LogicalToDeviceUnits(14));
        TextRenderer.DrawText(g, vramText, FluentStyle.MonoFont, vramTextRect, theme.ForegroundColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        int barY = vramY + LogicalToDeviceUnits(18);
        int barH = LogicalToDeviceUnits(8);
        if (availWidth > 0 && barH > 0)
        {
            Rectangle vramBarBounds = new(pad, barY, availWidth, barH);
            Color vramFill = vramRatio > 0.9f ? theme.StateHotColor : vramRatio > 0.75f ? theme.StateWarmColor : theme.AccentColor;
            Color vramBg = Color.FromArgb(40, theme.MutedForegroundColor);
            DrawProgressBar(g, vramBarBounds, vramRatio, vramFill, vramBg);
        }

        // 3. Ventoinhas status
        int fanY = barY + barH + LogicalToDeviceUnits(8);
        string fanVal = _fanRpm.HasValue ? $"{_fanRpm.Value:F0} RPM" : _fanPercent.HasValue ? $"{_fanPercent.Value:F0}%" : "Passivo / 0 RPM";
        Rectangle fanRect = new(pad, fanY, availWidth, LogicalToDeviceUnits(14));
        TextRenderer.DrawText(g, $"Ventilação Ativa: {fanVal}", FluentStyle.UiFontSmall, fanRect, theme.MutedForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        // 4. Expandable detailed sensor list
        if (IsExpanded && _detailedSensors.Count > 0)
        {
            DrawExpandedSensorList(g, fanY + LogicalToDeviceUnits(22), _detailedSensors, theme);
        }
    }
}
