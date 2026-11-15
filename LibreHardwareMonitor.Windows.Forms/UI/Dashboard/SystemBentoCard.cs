// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>Motherboard card: main voltages, board temperature, fans and the total network traffic.</summary>
public sealed class SystemBentoCard : BentoCardBase
{
    private readonly List<ISensor> _fans = new();
    private readonly StringBuilder _fanSummary = new();
    private float? _vcore;
    private float? _rail;
    private float? _boardTemp;
    private float? _mainFanRpm;
    private float _netDownBytes;
    private float _netUpBytes;
    private int _spinningFans;

    public SystemBentoCard()
    {
        Icon = FluentIcons.ForHardware(HardwareType.Motherboard);
        Title = "Placa-Mãe, Ventoinhas e Rede";
        Subtitle = "Tensões, arrefecimento e conectividade";
    }

    // Two KPI rows, the network caption with its two tiles, and the fan line.
    protected override int SummaryHeight => 104 + 14 + 18 + 36 + 8 + 14;

    public void UpdateSensors(IList<IHardware> hardware)
    {
        if (hardware == null)
            return;

        _vcore = _rail = _boardTemp = _mainFanRpm = null;
        _netDownBytes = _netUpBytes = 0f;
        _fans.Clear();

        BeginDetails();
        foreach (IHardware hw in hardware)
        {
            if (hw.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO)
            {
                if (hw.HardwareType == HardwareType.Motherboard && !string.IsNullOrEmpty(hw.Name))
                    Title = hw.Name;

                _vcore ??= SensorLookup.Find(hw, SensorType.Voltage, "Vcore", "CPU VDD", "Core")?.Value;
                _rail ??= SensorLookup.Find(hw, SensorType.Voltage, "12V", "5V")?.Value;
                _boardTemp ??= SensorLookup.FindOrFirst(hw, SensorType.Temperature, "System", "Motherboard", "Mainboard")?.Value;
                SensorLookup.Collect(hw, _fans, SensorType.Fan);
                AddDetails(hw);
            }
            else if (hw.HardwareType == HardwareType.Network)
            {
                // Total over every adapter: the active connection is whichever one is carrying traffic.
                _netDownBytes += SensorLookup.Find(hw, SensorType.Throughput, "Download")?.Value ?? 0f;
                _netUpBytes += SensorLookup.Find(hw, SensorType.Throughput, "Upload")?.Value ?? 0f;
                AddDetails(hw, hw.Name);
            }
        }

        _spinningFans = 0;
        _fanSummary.Clear();
        foreach (ISensor fan in _fans)
        {
            if (!(fan.Value > 0))
                continue;

            _mainFanRpm ??= fan.Value;
            if (_spinningFans++ < 3)
                _fanSummary.Append(_fanSummary.Length > 0 ? "  ·  " : string.Empty).Append(fan.Name).Append(": ").Append(fan.Value.Value.ToString("F0")).Append(" RPM");
        }

        BadgeText = _spinningFans > 0 ? $"{_spinningFans} ventoinhas" : "Motherboard";
        BadgeColor = _spinningFans > 0 ? Color.FromArgb(70, 200, 100) : Color.FromArgb(170, 170, 180);
        EndUpdate();
    }

    protected override void DrawSummary(Graphics g, Theme theme, Rectangle area)
    {
        DrawKpiBox(g, KpiTile(area, 0), "CPU Vcore", _vcore?.ToString("F3"), "V", null, null, theme);
        DrawKpiBox(g, KpiTile(area, 1), "Linha +12V / +5V", _rail?.ToString("F2"), "V", null, null, theme);
        DrawKpiBox(g, KpiTile(area, 2), "Temp. Placa-Mãe", _boardTemp?.ToString("F1"), "°C", _boardTemp / 100f, StateColor(theme, _boardTemp, 50f, 70f), theme);
        DrawKpiBox(g, KpiTile(area, 3), "Ventoinha Princ.", _mainFanRpm?.ToString("F0") ?? "0", "RPM", null, null, theme);

        int y = area.Y + LogicalToDeviceUnits(104 + 14);
        DrawSectionTitle(g, "REDE (TODOS OS ADAPTADORES)", area, y, theme);

        Rectangle tiles = new(area.X, y + LogicalToDeviceUnits(18), area.Width, area.Height);
        DrawKpiBox(g, KpiTile(tiles, 0, 36), "Download", SensorLookup.FormatThroughput(_netDownBytes), string.Empty, null, theme.AccentColor, theme);
        DrawKpiBox(g, KpiTile(tiles, 1, 36), "Upload", SensorLookup.FormatThroughput(_netUpBytes), string.Empty, null, null, theme);

        string fans = _spinningFans > 0 ? _fanSummary.ToString() : "Sem ventoinhas em rotação detetadas";
        TextRenderer.DrawText(g, fans, FluentStyle.UiFontSmall, new Rectangle(area.X, tiles.Y + LogicalToDeviceUnits(36 + 8), area.Width, LogicalToDeviceUnits(14)),
            theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}
