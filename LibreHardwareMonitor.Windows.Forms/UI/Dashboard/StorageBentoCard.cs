// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.DiskHealth;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;
using StorageDeviceLhm = LibreHardwareMonitor.Hardware.Storage.StorageDevice;

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

    private static readonly TimeSpan HealthInterval = TimeSpan.FromSeconds(5);
    private DiskHealthReport _health;
    private DateTime _healthTime;

    public StorageBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Storage);
        Title = "Unidade de Armazenamento";
        Subtitle = "Saúde SMART e ocupação";
    }

    public IHardware Hardware { get; private set; }

    // The space caption and bar, two KPI rows, then the main health reason.
    protected override int SummaryHeight => 26 + 14 + 104 + 20;

    public void UpdateSensors(IHardware storage)
    {
        if (storage == null)
            return;

        // Cards are reused by position; a different drive must not inherit the cached verdict.
        if (!ReferenceEquals(Hardware, storage))
            _health = null;

        Hardware = storage;
        Title = storage.Name;

        _totalGb = SensorLookup.ToGigabytes(SensorLookup.Find(storage, SensorType.Data, "Total")?.Value);
        _freeGb = SensorLookup.ToGigabytes(SensorLookup.Find(storage, SensorType.Data, "Free")?.Value);
        _temp = SensorLookup.FindOrFirst(storage, SensorType.Temperature)?.Value;
        _usedSpacePercent = SensorLookup.Find(storage, SensorType.Load, "Used Space")?.Value;
        _readBytes = SensorLookup.Find(storage, SensorType.Throughput, "Read")?.Value;
        _writeBytes = SensorLookup.Find(storage, SensorType.Throughput, "Write")?.Value;

        // Same verdict as the "Saúde dos discos" page; re-evaluated at most every few seconds.
        DateTime now = DateTime.UtcNow;
        if (storage is StorageDeviceLhm device && (_health == null || now - _healthTime >= HealthInterval))
        {
            try
            {
                _health = DiskHealthEvaluator.Evaluate(device.Storage, device.BytesWrittenSinceBoot, TimeSpan.FromMilliseconds(Environment.TickCount64));
                _healthTime = now;
            }
            catch (Exception)
            {
                // Keep the previous verdict if the drive changes mid-read.
            }
        }

        _lifeRemaining = _health?.LifePercent ?? SensorLookup.Find(storage, SensorType.Level, "Remaining", "Life", "Health")?.Value;

        if (_health != null)
        {
            (BadgeText, BadgeColor) = _health.Level switch
            {
                DiskHealthLevel.Good => ("Bom", Color.FromArgb(70, 200, 100)),
                DiskHealthLevel.Caution => ("Atenção", Color.FromArgb(240, 180, 30)),
                DiskHealthLevel.Bad => ("Mau", Color.FromArgb(235, 60, 60)),
                _ => ("Sem dados SMART", Color.FromArgb(140, 140, 140))
            };
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
        Color? healthColor = _health?.Level switch
        {
            DiskHealthLevel.Bad => theme.StateHotColor,
            DiskHealthLevel.Caution => theme.StateWarmColor,
            _ => null
        };
        string healthLabel = _health == null ? "Saúde SMART" : _health.IsHdd ? "Saúde (estimada)" : "Vida (reportada)";
        DrawKpiBox(g, KpiTile(tiles, 1), healthLabel, _lifeRemaining?.ToString("F0"), "%", _lifeRemaining / 100f, healthColor, theme);
        DrawKpiBox(g, KpiTile(tiles, 2), "Leitura", SensorLookup.FormatThroughput(_readBytes.GetValueOrDefault()), string.Empty, null, null, theme);
        DrawKpiBox(g, KpiTile(tiles, 3), "Escrita", SensorLookup.FormatThroughput(_writeBytes.GetValueOrDefault()), string.Empty, null, null, theme);

        DrawHealthReason(g, theme, new Rectangle(area.X, area.Y + LogicalToDeviceUnits(26 + 14 + 104), area.Width, LogicalToDeviceUnits(20)));
    }

    /// <summary>The most serious reason behind the verdict; full list on the "Saúde dos discos" page.</summary>
    private void DrawHealthReason(Graphics g, Theme theme, Rectangle bounds)
    {
        if (_health == null)
            return;

        DiskFinding? top = null;
        foreach (DiskFinding f in _health.Findings)
        {
            if (f.Severity != DiskFindingSeverity.Info && (top == null || f.Severity > top.Value.Severity))
                top = f;
        }

        (string text, Color dot) = top is DiskFinding finding
            ? (finding.Text, finding.Severity == DiskFindingSeverity.Bad ? theme.StateHotColor : theme.StateWarmColor)
            : _health.Level == DiskHealthLevel.Unknown
                ? ("Sem dados SMART para avaliar este disco.", theme.MutedForegroundColor)
                : ("Sem problemas detetados nos dados SMART.", theme.StateCoolColor);

        int size = LogicalToDeviceUnits(6);
        using (SolidBrush brush = new(dot))
            g.FillEllipse(brush, bounds.X, bounds.Y + ((bounds.Height - size) / 2), size, size);

        Rectangle textBounds = new(bounds.X + size + LogicalToDeviceUnits(8), bounds.Y, bounds.Width - size - LogicalToDeviceUnits(8), bounds.Height);
        TextRenderer.DrawText(g, text, FluentStyle.UiFontSmall, textBounds, top.HasValue ? theme.ForegroundColor : theme.MutedForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
