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
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>
/// Fan control page. Follows the dashboard structure: KPI strip, a one-line toolbar, then content cards
/// (fan channels grouped by controller in a responsive grid).
/// </summary>
public sealed class FanControlPanel : UserControl, IThemedControl
{
    private FanControlEngine _engine;
    private readonly List<FanChannelCard> _cards = new();

    // Last valid readings, so the KPIs never flicker to "--" between sensor updates.
    private float _lastCpuTemp;
    private float _lastGpuTemp;

    private readonly StackPanel _page;
    private readonly ScrollHost _host;

    private readonly MetricStrip _metrics;
    private readonly FluentMetricCard _cpuTempCard;
    private readonly FluentMetricCard _gpuTempCard;
    private readonly FluentMetricCard _channelsCard;
    private readonly FluentMetricCard _avgPwmCard;

    private readonly ToolbarRow _toolbar;
    private readonly Label _presetLabel;
    private readonly FluentComboBox _presetCombo;
    private readonly FluentBadge _safetyBadge;

    private readonly StackPanel _channelsStack;
    private readonly FluentSectionCard _emptyCard;
    private readonly Label _emptyLabel;

    public FanControlPanel() : this(null)
    {
    }

    public FanControlPanel(FanControlEngine engine)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Dock = DockStyle.Fill;

        // 1. KPI strip (same geometry as the dashboard summary).
        _metrics = new MetricStrip();
        _cpuTempCard = NewCard(FluentIcons.ForHardware(HardwareType.Cpu), "Temperatura CPU", "°C", "CPU Package");
        _gpuTempCard = NewCard(FluentIcons.ForSensorType(SensorType.Temperature), "Temperatura GPU", "°C", "GPU Core");
        _channelsCard = NewCard(FluentIcons.ForSensorType(SensorType.Fan), "Canais", "", "Controláveis por PWM");
        _avgPwmCard = NewCard(FluentIcons.ForSensorType(SensorType.Control), "Velocidade média", "%", "PWM dos canais");
        _metrics.AddCard(_cpuTempCard);
        _metrics.AddCard(_gpuTempCard);
        _metrics.AddCard(_channelsCard);
        _metrics.AddCard(_avgPwmCard);

        // 2. Toolbar: global profile on the left, safety state on the right.
        _toolbar = new ToolbarRow();
        _presetLabel = new Label { Text = "Perfil global", AutoSize = true, Font = FluentStyle.UiFont };
        _presetCombo = new FluentComboBox { Width = LogicalToDeviceUnits(200) };
        _presetCombo.Items.AddRange(new object[] { "BIOS (padrão)", "Silencioso", "Equilibrado", "Desempenho", "Personalizado" });
        _presetCombo.SelectedIndex = 0;
        _presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;
        _safetyBadge = new FluentBadge { Height = LogicalToDeviceUnits(24), Tag = PageMetrics.AlignRight };
        _safetyBadge.SetBadge("Proteção térmica ativa · 100 % acima de 90 °C", BadgeStyle.Success);
        _toolbar.Controls.Add(_presetLabel);
        _toolbar.Controls.Add(_presetCombo);
        _toolbar.Controls.Add(_safetyBadge);
        _toolbar.Height = _presetCombo.Height;

        // 3. Channels (groups are rebuilt by RefreshChannels).
        _channelsStack = new StackPanel { Gap = PageMetrics.Gap };

        _emptyCard = new FluentSectionCard
        {
            Title = "Nenhum canal de ventilação detetado",
            Subtitle = "Não foram encontrados controladores Super I/O ou dispositivos com escrita PWM.",
            AutoHeight = true
        };
        _emptyLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = LogicalToDeviceUnits(40),
            Font = FluentStyle.UiFont,
            Text = "O controlo PWM requer suporte no chip Super I/O (Nuvoton, ITE, Fintek) ou um controlador dedicado " +
                   "(NZXT, Corsair, Aquacomputer). Em portáteis e motherboards OEM o firmware pode reservar este controlo."
        };
        _emptyCard.Controls.Add(_emptyLabel);

        _page = new StackPanel { Dock = DockStyle.Top };
        _page.Controls.Add(_metrics);
        _page.Controls.Add(_toolbar);
        _page.Controls.Add(_emptyCard);
        _page.Controls.Add(_channelsStack);
        _host = new ScrollHost(_page) { Dock = DockStyle.Fill };
        Controls.Add(_host);

        if (engine != null)
            SetEngine(engine);

        ApplyTheme(Theme.Current);
    }

    private static FluentMetricCard NewCard(Image icon, string caption, string unit, string detail)
    {
        FluentMetricCard card = new() { Icon = icon };
        card.SetMetric(caption, "-", unit, detail);
        return card;
    }

    public void SetEngine(FanControlEngine engine)
    {
        if (_engine != null)
        {
            _engine.ChannelsChanged -= Engine_ChannelsChanged;
            _engine.PresetChanged -= Engine_PresetChanged;
        }

        _engine = engine;
        if (_engine == null)
            return;

        _engine.ChannelsChanged += Engine_ChannelsChanged;
        _engine.PresetChanged += Engine_PresetChanged;

        _presetCombo.SelectedIndex = (int)_engine.ActivePreset;
        RefreshChannels();
    }

    private void Engine_ChannelsChanged(object sender, EventArgs e) => RefreshChannels();

    private void Engine_PresetChanged(object sender, EventArgs e)
    {
        if (_engine != null)
            _presetCombo.SelectedIndex = (int)_engine.ActivePreset;
    }

    private void PresetCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_engine == null || _presetCombo.SelectedIndex < 0 || _presetCombo.SelectedIndex > 4)
            return;

        GlobalFanPreset selected = (GlobalFanPreset)_presetCombo.SelectedIndex;
        if (selected == _engine.ActivePreset)
            return;

        _engine.ApplyPreset(selected);
        foreach (FanChannelCard card in _cards)
            card.SyncUiWithConfig();
    }

    public void RefreshChannels()
    {
        _page.SuspendLayout();
        _channelsStack.SuspendLayout();

        foreach (Control group in _channelsStack.Controls.Cast<Control>().ToList())
        {
            _channelsStack.Controls.Remove(group);
            group.Dispose();
        }

        _cards.Clear();

        bool hasChannels = _engine != null && _engine.Channels.Count > 0;
        _emptyCard.Visible = !hasChannels;
        _channelsStack.Visible = hasChannels;

        if (hasChannels)
        {
            // One header + grid per controller, in discovery order.
            foreach (IGrouping<string, FanChannelRuntime> group in _engine.Channels.GroupBy(c => c.HardwareName))
            {
                int count = group.Count();
                GroupHeader header = new()
                {
                    Text = group.Key,
                    Detail = count == 1 ? "· 1 canal" : $"· {count} canais",
                    Icon = IconForController(group.First())
                };

                WrapGrid grid = new() { MinColumnWidth = 340 };
                foreach (FanChannelRuntime channel in group)
                {
                    FanChannelCard card = new(channel, _engine);
                    _cards.Add(card);
                    grid.Controls.Add(card);
                }

                // A group = header + grid with a tighter gap between them.
                StackPanel block = new() { Gap = 8 };
                block.Controls.Add(header);
                block.Controls.Add(grid);
                _channelsStack.Controls.Add(block);
            }
        }

        UpdateChannelMetrics();
        _channelsStack.ResumeLayout(true);
        _page.ResumeLayout(true);
        ApplyTheme(Theme.Current);
    }

    private static Image IconForController(FanChannelRuntime channel)
    {
        IHardware hardware = channel.Control.Sensor?.Hardware;
        return hardware != null ? FluentIcons.ForHardware(hardware.HardwareType) : FluentIcons.ForSensorType(SensorType.Fan);
    }

    private void UpdateChannelMetrics()
    {
        int count = _engine?.Channels.Count ?? 0;
        _channelsCard.SetMetric("Canais", count.ToString(), "", count == 0 ? "Nenhum controlador" : "Controláveis por PWM");

        if (count == 0)
        {
            _avgPwmCard.SetMetric("Velocidade média", "-", "%", "PWM dos canais");
            return;
        }

        float avg = _engine.Channels.Average(c => c.Control.ControlMode == ControlMode.Software
                                                    ? c.LastAppliedPwm
                                                    : (c.Control.Sensor?.Value ?? 0f));
        MetricCardSeverity sev = avg >= 85f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal;
        _avgPwmCard.SetMetric("Velocidade média", $"{avg:F0}", "%", "PWM dos canais", sev, avg / 100f);
    }

    public void UpdateTelemetry()
    {
        if (_engine == null || !Visible)
            return;

        float cpu = ReadTemperature(_engine.LastCpuTemp, h => h.HardwareType == HardwareType.Cpu, ref _lastCpuTemp);
        SetTemperature(_cpuTempCard, "Temperatura CPU", "CPU Package", cpu, 75f, 85f);

        float gpu = ReadTemperature(_engine.LastGpuTemp,
                                    h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel,
                                    ref _lastGpuTemp);
        SetTemperature(_gpuTempCard, "Temperatura GPU", "GPU Core", gpu, 72f, 83f);

        UpdateChannelMetrics();
        foreach (FanChannelCard card in _cards)
            card.UpdateTelemetry();
    }

    /// <summary>Uses the engine reading, falls back to the first temperature sensor of the hardware, then to the last good value.</summary>
    private float ReadTemperature(float engineValue, Func<IHardware, bool> match, ref float last)
    {
        float value = engineValue;
        if (value <= 0f && _engine.Computer != null)
        {
            IHardware hw = _engine.Computer.Hardware.FirstOrDefault(match);
            ISensor sensor = hw?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
            if (sensor?.Value != null)
                value = sensor.Value.Value;
        }

        if (value > 0f)
            last = value;

        return value > 0f ? value : last;
    }

    private static void SetTemperature(FluentMetricCard card, string caption, string detail, float value, float warm, float hot)
    {
        if (value <= 0f)
        {
            card.SetMetric(caption, "-", "°C", detail);
            return;
        }

        MetricCardSeverity sev = value >= hot ? MetricCardSeverity.Hot : (value >= warm ? MetricCardSeverity.Warm : MetricCardSeverity.Normal);
        card.SetMetric(caption, $"{value:F0}", "°C", detail, sev, value / 100f);
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;

        theme.Apply(_host);

        _presetLabel.ForeColor = theme.MutedForegroundColor;
        _emptyLabel.BackColor = theme.SurfaceColor;
        _emptyLabel.ForeColor = theme.MutedForegroundColor;

        Invalidate(true);
    }
}
