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
/// Specialized Bento card for Storage units (SSD, NVMe, HDD) displaying
/// temperature, health percentage, space usage, live transfer rates, and expandable SMART telemetry.
/// </summary>
public sealed class StorageBentoCard : BentoCardBase
{
    private readonly List<(string Name, string Value)> _detailedSensors = new();
    private float? _temp;
    private float? _lifeRemaining;
    private float? _usedSpacePercent;
    private float? _totalGb;
    private float? _freeGb;
    private float? _readSpeedBytes;
    private float? _writeSpeedBytes;
    private float? _totalActivityPercent;

    public IHardware Hardware { get; private set; }

    public StorageBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Storage);
        Title = "Unidade de Armazenamento";
        Subtitle = "Saúde SMART e Ocupação";
        Height = LogicalToDeviceUnits(260);
    }

    public void UpdateSensors(IHardware storage)
    {
        if (storage == null)
            return;

        Hardware = storage;
        Title = storage.Name;

        ISensor totalSpace = storage.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Total"));
        _totalGb = ToGigabytes(totalSpace?.Value);

        ISensor freeSpace = storage.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Free"));
        _freeGb = ToGigabytes(freeSpace?.Value);

        ISensor temp = storage.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
        _temp = temp?.Value;

        ISensor life = storage.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Level &&
            (s.Name.Contains("Remaining") || s.Name.Contains("Life") || s.Name.Contains("Health")));
        _lifeRemaining = life?.Value;

        ISensor usedSpace = storage.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Used Space"));
        _usedSpacePercent = usedSpace?.Value;

        ISensor activity = storage.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total Activity"));
        _totalActivityPercent = activity?.Value;

        ISensor readSpeed = storage.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Throughput && s.Name.Contains("Read"));
        _readSpeedBytes = readSpeed?.Value;

        ISensor writeSpeed = storage.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Throughput && s.Name.Contains("Write"));
        _writeSpeedBytes = writeSpeed?.Value;

        if (_lifeRemaining.HasValue)
        {
            if (_lifeRemaining.Value <= 20f)
            {
                BadgeText = "Atenção";
                BadgeColor = Color.FromArgb(235, 60, 60);
            }
            else
            {
                BadgeText = $"{_lifeRemaining.Value:F0}% Saúde";
                BadgeColor = Color.FromArgb(70, 200, 100);
            }
        }
        else if (_temp.HasValue)
        {
            BadgeText = _temp.Value >= 65f ? "Quente" : "Saudável";
            BadgeColor = _temp.Value >= 65f ? Color.FromArgb(240, 180, 30) : Color.FromArgb(70, 200, 100);
        }

        // Detailed Sensors for expansion (including SMART)
        _detailedSensors.Clear();
        foreach (ISensor s in storage.Sensors.Where(s => s.Value.HasValue))
        {
            if (s.SensorType == SensorType.Temperature)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F1} °C"));
            else if (s.SensorType == SensorType.Load)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F1} %"));
            else if (s.SensorType == SensorType.Level)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F0} %"));
            else if (s.SensorType == SensorType.Throughput)
                _detailedSensors.Add((s.Name, FormatThroughput(s.Value.Value)));
            else if (s.SensorType == SensorType.Data)
            {
                float? gb = ToGigabytes(s.Value);
                _detailedSensors.Add((s.Name, $"{gb.GetValueOrDefault():F1} GB"));
            }
            else
            {
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F0}"));
            }
        }

        RecalculateCardHeight();
        Invalidate();
    }

    protected override void RecalculateCardHeight()
    {
        int baseHeight = 260;
        if (IsExpanded && _detailedSensors.Count > 0)
        {
            int extraRows = (int)Math.Ceiling(_detailedSensors.Count / 2.0);
            baseHeight += 32 + (extraRows * 22) + 16;
        }

        Height = LogicalToDeviceUnits(baseHeight);
    }

    protected override void DrawCardContent(Graphics g, Theme theme)
    {
        int pad = LogicalToDeviceUnits(14);
        int topY = LogicalToDeviceUnits(HeaderHeightLogical) + LogicalToDeviceUnits(6);
        int availWidth = Width - (pad * 2);

        // Space Used Bar Section
        DrawSectionTitle(g, "ESPAÇO EM DISCO", pad, topY, theme);

        float spaceRatio = _usedSpacePercent.HasValue ? Math.Max(0f, Math.Min(1f, _usedSpacePercent.Value / 100f)) : 0f;
        string spaceText;
        if (_totalGb.HasValue && _totalGb.Value > 1f)
        {
            float usedGb = Math.Max(0f, _totalGb.Value - _freeGb.GetValueOrDefault());
            spaceText = $"{usedGb:F1} / {_totalGb.Value:F1} GB ({spaceRatio * 100f:F1}%)";
        }
        else if (_usedSpacePercent.HasValue)
        {
            spaceText = $"{_usedSpacePercent.Value:F1}%";
        }
        else
        {
            spaceText = "--";
        }

        Rectangle spaceTextRect = new(pad, topY, availWidth, LogicalToDeviceUnits(14));
        TextRenderer.DrawText(g, spaceText, FluentStyle.MonoFont, spaceTextRect, theme.ForegroundColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        int barY = topY + LogicalToDeviceUnits(18);
        int barH = LogicalToDeviceUnits(8);
        if (availWidth > 0 && barH > 0)
        {
            Rectangle spaceBarBounds = new(pad, barY, availWidth, barH);
            Color spaceFill = spaceRatio > 0.9f ? theme.StateHotColor : spaceRatio > 0.75f ? theme.StateWarmColor : theme.AccentColor;
            Color spaceBg = Color.FromArgb(40, theme.MutedForegroundColor);
            DrawProgressBar(g, spaceBarBounds, spaceRatio, spaceFill, spaceBg);
        }

        // 4 KPI Boxes (2x2) with 1 decimal precision
        int kpiY = barY + barH + LogicalToDeviceUnits(14);
        int kpiW = (availWidth - LogicalToDeviceUnits(10)) / 2;
        int kpiH = LogicalToDeviceUnits(48);

        Color? tempColor = _temp.HasValue
            ? (_temp.Value >= 65f ? theme.StateHotColor : _temp.Value >= 50f ? theme.StateWarmColor : theme.StateCoolColor)
            : null;

        string readVal = FormatThroughput(_readSpeedBytes.GetValueOrDefault());
        string writeVal = FormatThroughput(_writeSpeedBytes.GetValueOrDefault());

        Rectangle r1 = new(pad, kpiY, kpiW, kpiH);
        Rectangle r2 = new(pad + kpiW + LogicalToDeviceUnits(10), kpiY, kpiW, kpiH);
        Rectangle r3 = new(pad, kpiY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);
        Rectangle r4 = new(pad + kpiW + LogicalToDeviceUnits(10), kpiY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);

        DrawKpiBox(g, r1, "Temperatura", _temp.HasValue ? $"{_temp.Value:F1}" : "--", "°C", _temp / 100f, tempColor, theme);
        DrawKpiBox(g, r2, "Saúde SMART", _lifeRemaining.HasValue ? $"{_lifeRemaining.Value:F0}" : "100", "%", _lifeRemaining / 100f, null, theme);
        DrawKpiBox(g, r3, "Taxa Leitura", readVal, string.Empty, null, null, theme);
        DrawKpiBox(g, r4, "Taxa Escrita", writeVal, string.Empty, null, null, theme);

        // Expandable detailed sensor list
        if (IsExpanded && _detailedSensors.Count > 0)
        {
            DrawExpandedSensorList(g, kpiY + (kpiH * 2) + LogicalToDeviceUnits(18), _detailedSensors, theme);
        }
    }

    private static string FormatThroughput(float bytesPerSec)
    {
        if (bytesPerSec <= 0)
            return "0 MB/s";
        if (bytesPerSec < 1024 * 1024)
            return $"{bytesPerSec / 1024f:F0} KB/s";
        return $"{bytesPerSec / (1024f * 1024f):F1} MB/s";
    }
}
