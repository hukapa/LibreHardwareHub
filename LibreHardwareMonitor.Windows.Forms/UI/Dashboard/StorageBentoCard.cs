// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>Drive card: used space bar, temperature, SMART health and read/write rates.</summary>
public sealed class StorageBentoCard : BentoCardBase
{
    private float? _temp;
    private float? _lifeRemaining;
    private float? _usedSpacePercent;
    private float? _totalGb;
    private float? _freeGb;
    private float? _readBytes;
    private float? _writeBytes;

    public StorageBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Storage);
        Title = "Unidade de Armazenamento";
        Subtitle = "Saúde SMART e ocupação";
    }

    public IHardware Hardware { get; private set; }

    // The space caption and bar, then two KPI rows.
    protected override int SummaryHeight => 26 + 14 + 104;

    public void UpdateSensors(IHardware storage)
    {
        if (storage == null)
            return;

        Hardware = storage;
        Title = storage.Name;

        _totalGb = SensorLookup.ToGigabytes(SensorLookup.Find(storage, SensorType.Data, "Total")?.Value);
        _freeGb = SensorLookup.ToGigabytes(SensorLookup.Find(storage, SensorType.Data, "Free")?.Value);
        _temp = SensorLookup.FindOrFirst(storage, SensorType.Temperature)?.Value;
        _lifeRemaining = SensorLookup.Find(storage, SensorType.Level, "Remaining", "Life", "Health")?.Value;
        _usedSpacePercent = SensorLookup.Find(storage, SensorType.Load, "Used Space")?.Value;
        _readBytes = SensorLookup.Find(storage, SensorType.Throughput, "Read")?.Value;
        _writeBytes = SensorLookup.Find(storage, SensorType.Throughput, "Write")?.Value;

        if (_lifeRemaining.HasValue)
        {
            BadgeText = _lifeRemaining.Value <= 20f ? "Atenção" : $"{_lifeRemaining.Value:F0}% Saúde";
            BadgeColor = _lifeRemaining.Value <= 20f ? Color.FromArgb(235, 60, 60) : Color.FromArgb(70, 200, 100);
        }
        else
        {
            SetBadge(_temp, 65f, 80f, "Saudável", "Quente", "Crítico");
        }

        BeginDetails();
        AddDetails(storage);
        EndUpdate();
    }

    protected override void DrawSummary(Graphics g, Theme theme, Rectangle area)
    {
        float ratio = Math.Clamp(_usedSpacePercent.GetValueOrDefault() / 100f, 0f, 1f);
        string space = _totalGb is > 1f
            ? $"{Math.Max(0f, _totalGb.Value - _freeGb.GetValueOrDefault()):F0} / {_totalGb.Value:F0} GB ({ratio * 100f:F0}%)"
            : _usedSpacePercent.HasValue ? $"{_usedSpacePercent.Value:F0}%" : "--";

        DrawSectionTitle(g, "ESPAÇO EM DISCO", area, area.Y, theme, space);
        DrawUsageBar(g, theme, area, area.Y + LogicalToDeviceUnits(18), ratio, 0.75f, 0.9f);

        Rectangle tiles = new(area.X, area.Y + LogicalToDeviceUnits(26 + 14), area.Width, area.Height);
        DrawKpiBox(g, KpiTile(tiles, 0), "Temperatura", _temp?.ToString("F1"), "°C", _temp / 100f, StateColor(theme, _temp, 50f, 65f), theme);
        DrawKpiBox(g, KpiTile(tiles, 1), "Saúde SMART", _lifeRemaining?.ToString("F0"), "%", _lifeRemaining / 100f, null, theme);
        DrawKpiBox(g, KpiTile(tiles, 2), "Leitura", SensorLookup.FormatThroughput(_readBytes.GetValueOrDefault()), string.Empty, null, null, theme);
        DrawKpiBox(g, KpiTile(tiles, 3), "Escrita", SensorLookup.FormatThroughput(_writeBytes.GetValueOrDefault()), string.Empty, null, null, theme);
    }
}
