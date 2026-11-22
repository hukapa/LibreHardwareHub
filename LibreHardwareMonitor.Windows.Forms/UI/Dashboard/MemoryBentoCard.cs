// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>Memory card: physical usage bar, used/free, page file and module temperature.</summary>
public sealed class MemoryBentoCard : BentoCardBase
{
    private float? _usedGb;
    private float? _availableGb;
    private float? _loadPercent;
    private float? _virtualUsedGb;
    private float? _dimmTemp;

    public MemoryBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Memory);
        Title = "Memória do Sistema (RAM)";
        Subtitle = "Alocação física e paginação";
    }

    // The usage caption and bar, then two KPI rows.
    protected override int SummaryHeight => 26 + 14 + 104;

    public void UpdateSensors(IList<IHardware> hardware)
    {
        if (hardware == null)
            return;

        IHardware physical = SensorLookup.PhysicalMemory(hardware);
        _usedGb = SensorLookup.ToGigabytes(SensorLookup.Find(physical, SensorType.Data, "Memory Used")?.Value);
        _availableGb = SensorLookup.ToGigabytes(SensorLookup.Find(physical, SensorType.Data, "Memory Available")?.Value);
        _loadPercent = SensorLookup.Find(physical, SensorType.Load, "Memory")?.Value;

        _virtualUsedGb = null;
        _dimmTemp = null;
        BeginDetails();
        foreach (IHardware hw in hardware)
        {
            if (hw.HardwareType != HardwareType.Memory)
                continue;

            if (hw != physical && SensorLookup.IsVirtualMemory(hw))
                _virtualUsedGb = SensorLookup.ToGigabytes(SensorLookup.Find(hw, SensorType.Data, "Memory Used")?.Value);

            // Hottest module.
            if (SensorLookup.FindOrFirst(hw, SensorType.Temperature)?.Value is float temp && temp > _dimmTemp.GetValueOrDefault())
                _dimmTemp = temp;

            AddDetails(hw, hw.Name);
        }

        SetBadge(_loadPercent, 75f, 90f, "Disponível", "Alta", "Saturada");
        EndUpdate();
    }

    protected override void DrawSummary(Graphics g, Theme theme, Rectangle area)
    {
        float used = _usedGb.GetValueOrDefault();
        float total = used + _availableGb.GetValueOrDefault();
        float ratio = Math.Clamp(_loadPercent.HasValue ? _loadPercent.Value / 100f : total > 0.1f ? used / total : 0f, 0f, 1f);
        string usage = total > 0.1f ? $"{used:F1} / {total:F1} GB ({ratio * 100f:F0}%)" : $"{used:F1} GB";

        DrawSectionTitle(g, "MEMÓRIA FÍSICA", area, area.Y, theme, usage);
        DrawUsageBar(g, theme, area, area.Y + LogicalToDeviceUnits(18), ratio, 0.70f, 0.88f);

        Rectangle tiles = new(area.X, area.Y + LogicalToDeviceUnits(26 + 14), area.Width, area.Height);
        DrawKpiBox(g, KpiTile(tiles, 0), "Memória Usada", _usedGb?.ToString("F1"), "GB", ratio, null, theme);
        DrawKpiBox(g, KpiTile(tiles, 1), "Memória Livre", _availableGb?.ToString("F1"), "GB", 1f - ratio, null, theme);
        DrawKpiBox(g, KpiTile(tiles, 2), "Memória Virtual (Swap)", _virtualUsedGb?.ToString("F1"), "GB", null, null, theme);
        DrawKpiBox(g, KpiTile(tiles, 3), "Temp. Módulos", _dimmTemp?.ToString("F1"), _dimmTemp.HasValue ? "°C" : string.Empty,
            _dimmTemp / 100f, StateColor(theme, _dimmTemp, 50f, 70f), theme);
    }
}
