// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>Graphics card: core temperature, load, power and clock, the VRAM bar and the fan status.</summary>
public sealed class GpuBentoCard : BentoCardBase
{
    private float? _coreTemp;
    private float? _hotSpotTemp;
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
        Subtitle = "Telemetria de vídeo e VRAM";
    }

    // Two KPI rows, the VRAM caption and bar, and the fan line.
    protected override int SummaryHeight => 104 + 14 + 26 + 8 + 14;

    public void UpdateSensors(IHardware gpu)
    {
        if (gpu == null)
            return;

        Title = gpu.Name;
        Icon = FluentIcons.ForHardware(gpu.HardwareType);

        _coreTemp = SensorLookup.FindOrFirst(gpu, SensorType.Temperature, "Core")?.Value;
        _hotSpotTemp = SensorLookup.Find(gpu, SensorType.Temperature, "Hot Spot")?.Value;
        _coreLoad = SensorLookup.FindOrFirst(gpu, SensorType.Load, "Core", "D3D 3D")?.Value;
        _power = SensorLookup.FindOrFirst(gpu, SensorType.Power, "Package", "GPU")?.Value;
        _coreClock = SensorLookup.Find(gpu, SensorType.Clock, "Core")?.Value;
        _fanRpm = SensorLookup.FindOrFirst(gpu, SensorType.Fan)?.Value;
        _fanPercent = SensorLookup.FindOrFirst(gpu, SensorType.Control)?.Value;

        _vramUsedGb = SensorLookup.ToGigabytes(SensorLookup.Find(gpu, SensorType.Data, "Memory Used")?.Value);
        _vramTotalGb = SensorLookup.ToGigabytes(SensorLookup.Find(gpu, SensorType.Data, "Memory Total")?.Value);
        if (!_vramTotalGb.HasValue && SensorLookup.ToGigabytes(SensorLookup.Find(gpu, SensorType.Data, "Memory Free")?.Value) is float free)
            _vramTotalGb = _vramUsedGb.GetValueOrDefault() + free;

        SetBadge(_coreTemp, 70f, 83f, "Fresca", "Média", "Elevada");

        BeginDetails();
        AddDetails(gpu);
        EndUpdate();
    }

    protected override void DrawSummary(Graphics g, Theme theme, Rectangle area)
    {
        string temp = _coreTemp?.ToString("F1");
        if (temp != null && _hotSpotTemp.HasValue)
            temp += $" (HS {_hotSpotTemp.Value:F0})";

        DrawKpiBox(g, KpiTile(area, 0), "Temp. Core", temp, "°C", _coreTemp / 100f, StateColor(theme, _coreTemp, 70f, 83f), theme);
        DrawKpiBox(g, KpiTile(area, 1), "Carga Core", _coreLoad?.ToString("F1"), "%", _coreLoad / 100f, null, theme);
        DrawKpiBox(g, KpiTile(area, 2), "Consumo GPU", _power?.ToString("F1"), "W", null, null, theme);
        DrawKpiBox(g, KpiTile(area, 3), "Clock GPU", _coreClock?.ToString("F0"), "MHz", null, null, theme);

        float used = _vramUsedGb.GetValueOrDefault();
        float total = _vramTotalGb.GetValueOrDefault();
        float ratio = total > 0.1f ? Math.Clamp(used / total, 0f, 1f) : 0f;
        string vram = total > 0.1f ? $"{used:F1} / {total:F1} GB ({ratio * 100f:F0}%)" : $"{used:F1} GB";

        int y = area.Y + LogicalToDeviceUnits(104 + 14);
        DrawSectionTitle(g, "MEMÓRIA DE VÍDEO (VRAM)", area, y, theme, vram);
        DrawUsageBar(g, theme, area, y + LogicalToDeviceUnits(18), ratio, 0.75f, 0.9f);

        string fan = _fanRpm.HasValue ? $"{_fanRpm.Value:F0} RPM" : _fanPercent.HasValue ? $"{_fanPercent.Value:F0} %" : "parada";
        if (_fanRpm.HasValue && _fanPercent.HasValue)
            fan += $" ({_fanPercent.Value:F0} %)";

        TextRenderer.DrawText(g, $"Ventoinha: {fan}", FluentStyle.UiFontSmall, new Rectangle(area.X, y + LogicalToDeviceUnits(34), area.Width, LogicalToDeviceUnits(14)),
            theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
