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
/// Specialized Bento card for Motherboard, Super I/O, Fans and Network,
/// presenting key rail voltages (+12V, +5V, +3.3V, Vcore), system temps, fans, and expandable sensor inventory.
/// </summary>
public sealed class SystemBentoCard : BentoCardBase
{
    private readonly List<(string Name, string Value)> _detailedSensors = new();
    private string _vcoreVal;
    private string _railVal;
    private float? _moboTemp;
    private float? _mainFanRpm;
    private readonly List<(string Name, string Value)> _allFans = new();
    private float? _netDownBytes;
    private float? _netUpBytes;

    public SystemBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Motherboard);
        Title = "Placa-Mãe, Ventoinhas e Rede";
        Subtitle = "Tensões, Arrefecimento e Conectividade";
        Height = LogicalToDeviceUnits(260);
    }

    public void UpdateSensors(IEnumerable<IHardware> hardwareList)
    {
        if (hardwareList == null)
            return;

        _vcoreVal = null;
        _railVal = null;
        _moboTemp = null;
        _mainFanRpm = null;
        _allFans.Clear();
        _detailedSensors.Clear();

        List<ISensor> allSensors = new();

        foreach (IHardware hw in hardwareList)
        {
            if (hw.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO)
            {
                if (hw.HardwareType == HardwareType.Motherboard && !string.IsNullOrEmpty(hw.Name))
                    Title = hw.Name;

                CollectSensors(hw, allSensors);
            }
            else if (hw.HardwareType == HardwareType.Network)
            {
                ISensor dl = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Throughput && s.Name.Contains("Download"));
                ISensor ul = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Throughput && s.Name.Contains("Upload"));
                if (dl != null) _netDownBytes = dl.Value;
                if (ul != null) _netUpBytes = ul.Value;

                foreach (ISensor s in hw.Sensors.Where(s => s.Value.HasValue))
                {
                    if (s.SensorType == SensorType.Throughput)
                        _detailedSensors.Add(($"{hw.Name}: {s.Name}", FormatThroughput(s.Value.Value)));
                    else if (s.SensorType == SensorType.Data)
                    {
                        float? gb = ToGigabytes(s.Value);
                        _detailedSensors.Add(($"{hw.Name}: {s.Name}", $"{gb.GetValueOrDefault():F2} GB"));
                    }
                }
            }
        }

        // 1. Voltages: Vcore with 3 decimals, Rails with 2 decimals
        ISensor vcore = allSensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage &&
            (s.Name.Contains("Vcore") || s.Name.Contains("Core") || s.Name.Contains("CPU VDD")));
        if (vcore?.Value.HasValue == true)
            _vcoreVal = $"{vcore.Value.Value:F3}";

        ISensor rail12 = allSensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Name.Contains("12V"));
        ISensor rail5 = allSensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Name.Contains("5V"));
        if (rail12?.Value.HasValue == true)
            _railVal = $"{rail12.Value.Value:F2}";
        else if (rail5?.Value.HasValue == true)
            _railVal = $"{rail5.Value.Value:F2}";

        // 2. Motherboard Temp with 1 decimal
        ISensor temp = allSensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature &&
            (s.Name.Contains("System") || s.Name.Contains("Motherboard") || s.Name.Contains("Mainboard")))
            ?? allSensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
        _moboTemp = temp?.Value;

        // 3. Fans
        var fans = allSensors.Where(s => s.SensorType == SensorType.Fan && s.Value.HasValue && s.Value.Value > 0).ToList();
        if (fans.Count > 0)
        {
            _mainFanRpm = fans[0].Value;
            foreach (ISensor f in fans)
            {
                _allFans.Add((f.Name, $"{f.Value.GetValueOrDefault():F0} RPM"));
            }
        }

        // Detailed sensors for expansion
        foreach (ISensor s in allSensors.Where(s => s.Value.HasValue))
        {
            if (s.SensorType == SensorType.Temperature)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F1} °C"));
            else if (s.SensorType == SensorType.Voltage)
            {
                string fmt = (s.Name.Contains("Vcore") || s.Name.Contains("Core")) ? $"{s.Value.Value:F3} V" : $"{s.Value.Value:F2} V";
                _detailedSensors.Add((s.Name, fmt));
            }
            else if (s.SensorType == SensorType.Fan)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F0} RPM"));
            else if (s.SensorType == SensorType.Control)
                _detailedSensors.Add((s.Name, $"{s.Value.Value:F0} %"));
        }

        BadgeText = _allFans.Count > 0 ? $"{_allFans.Count} Ventoinhas" : "Motherboard";
        BadgeColor = _allFans.Count > 0 ? Color.FromArgb(70, 200, 100) : Color.FromArgb(170, 170, 180);

        RecalculateCardHeight();
        Invalidate();
    }

    private static void CollectSensors(IHardware hw, List<ISensor> allSensors)
    {
        if (hw == null)
            return;

        allSensors.AddRange(hw.Sensors);
        if (hw.SubHardware != null)
        {
            foreach (IHardware sub in hw.SubHardware)
            {
                CollectSensors(sub, allSensors);
            }
        }
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

        // 1. Top 4 KPI Boxes (2x2)
        int kpiW = (availWidth - LogicalToDeviceUnits(10)) / 2;
        int kpiH = LogicalToDeviceUnits(48);

        Rectangle r1 = new(pad, topY, kpiW, kpiH);
        Rectangle r2 = new(pad + kpiW + LogicalToDeviceUnits(10), topY, kpiW, kpiH);
        Rectangle r3 = new(pad, topY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);
        Rectangle r4 = new(pad + kpiW + LogicalToDeviceUnits(10), topY + kpiH + LogicalToDeviceUnits(8), kpiW, kpiH);

        Color? tempColor = _moboTemp.HasValue
            ? (_moboTemp.Value >= 70f ? theme.StateHotColor : _moboTemp.Value >= 50f ? theme.StateWarmColor : theme.StateCoolColor)
            : null;

        DrawKpiBox(g, r1, "CPU Vcore", _vcoreVal ?? "--", "V", null, null, theme);
        DrawKpiBox(g, r2, "Linha +12V / +5V", _railVal ?? "--", "V", null, null, theme);
        DrawKpiBox(g, r3, "Temp. Placa-Mãe", _moboTemp.HasValue ? $"{_moboTemp.Value:F1}" : "--", "°C", _moboTemp / 100f, tempColor, theme);
        DrawKpiBox(g, r4, "Ventoinha Princ.", _mainFanRpm.HasValue ? $"{_mainFanRpm.Value:F0}" : "0", "RPM", null, null, theme);

        // 2. Network & Cooling Section
        int netY = topY + (kpiH * 2) + LogicalToDeviceUnits(14);
        DrawSectionTitle(g, "TRÁFEGO DE REDE E ARREFECIMENTO", pad, netY, theme);

        int netKpiY = netY + LogicalToDeviceUnits(16);
        int netKpiH = LogicalToDeviceUnits(36);
        Rectangle nr1 = new(pad, netKpiY, kpiW, netKpiH);
        Rectangle nr2 = new(pad + kpiW + LogicalToDeviceUnits(10), netKpiY, kpiW, netKpiH);

        string netDown = FormatThroughput(_netDownBytes.GetValueOrDefault());
        string netUp = FormatThroughput(_netUpBytes.GetValueOrDefault());

        DrawKpiBox(g, nr1, "Download", netDown, string.Empty, null, theme.AccentColor, theme);
        DrawKpiBox(g, nr2, "Upload", netUp, string.Empty, null, null, theme);

        int fanSummaryY = netKpiY + netKpiH + LogicalToDeviceUnits(8);
        string fansSummary = _allFans.Count > 0
            ? string.Join(" · ", _allFans.Take(3).Select(f => $"{f.Name}: {f.Value}"))
            : "Super I/O: Arrefecimento passivo / 0 RPM detetado";

        Rectangle fanRect = new(pad, fanSummaryY, availWidth, LogicalToDeviceUnits(14));
        TextRenderer.DrawText(g, fansSummary, FluentStyle.UiFontSmall, fanRect, theme.MutedForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        // 3. Expandable detailed sensor list
        if (IsExpanded && _detailedSensors.Count > 0)
        {
            DrawExpandedSensorList(g, fanSummaryY + LogicalToDeviceUnits(22), _detailedSensors, theme);
        }
    }

    private static string FormatThroughput(float bytesPerSec)
    {
        if (bytesPerSec <= 0)
            return "0 KB/s";
        if (bytesPerSec < 1024 * 1024)
            return $"{bytesPerSec / 1024f:F0} KB/s";
        return $"{bytesPerSec / (1024f * 1024f):F1} MB/s";
    }
}
