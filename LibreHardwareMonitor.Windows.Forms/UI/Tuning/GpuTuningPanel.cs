// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

/// <summary>
/// GPU tuning page. Same structure as <see cref="CpuTuningPanel" />: KPI strip, toolbar (card selector),
/// settings card with Windows 11 style rows, and the action bar.
/// </summary>
public sealed class GpuTuningPanel : UserControl, IThemedControl
{
    private const int StockTempLimitC = 83;

    private GpuTuningEngine _engine;
    private IGpuTuningDevice _selectedDevice;
    private int _selectedPresetIndex = 1; // 0 = Eco, 1 = Stock, 2 = Performance, -1 = Custom

    private readonly StackPanel _page;

    // 1. KPI strip
    private readonly MetricStrip _metrics;
    private readonly FluentMetricCard _coreClockCard;
    private readonly FluentMetricCard _memClockCard;
    private readonly FluentMetricCard _tempCard;
    private readonly FluentMetricCard _powerCard;
    private readonly FluentMetricCard _loadCard;

    // 2. Toolbar
    private readonly ToolbarRow _toolbar;
    private readonly Label _gpuSelectLabel;
    private readonly FluentComboBox _gpuCombo;
    private readonly FluentBadge _safetyBadge;

    // 3. Watchdog banner
    private readonly WatchdogAlertPanel _watchdogBanner;
    private readonly Label _watchdogLabel;
    private readonly FluentButton _btnKeep;
    private readonly FluentButton _btnRevert;

    // 4. Settings card
    private readonly FluentSectionCard _settingsCard;
    private readonly FluentButton _btnEco;
    private readonly FluentButton _btnBalanced;
    private readonly FluentButton _btnMaxPerf;
    private readonly FluentSlider _coreSlider;
    private readonly FluentSlider _memSlider;
    private readonly FluentSlider _powerSlider;
    private readonly FluentSlider _tempLimitSlider;
    private readonly SettingRow _coreRow;
    private readonly SettingRow _memRow;
    private readonly SettingRow _powerRow;
    private readonly SettingRow _tempLimitRow;

    // 5. Actions
    private readonly ToolbarRow _actions;
    private readonly FluentButton _btnTest;
    private readonly FluentButton _btnResetStock;
    private readonly CheckBox _chkApplyStartup;
    private readonly Label _statusMessageLabel;

    public GpuTuningPanel() : this(null)
    {
    }

    public GpuTuningPanel(GpuTuningEngine engine)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Dock = DockStyle.Fill;
        AutoScroll = true;

        // 1. KPI strip
        _metrics = new MetricStrip();
        _coreClockCard = NewCard(FluentIcons.ForSensorType(SensorType.Clock), "Frequência do núcleo", "MHz", "GPU Core");
        _memClockCard = NewCard(FluentIcons.ForHardware(HardwareType.Memory), "Frequência da VRAM", "MHz", "Memória de vídeo");
        _tempCard = NewCard(FluentIcons.ForSensorType(SensorType.Temperature), "Temperatura", "°C", "GPU Core");
        _powerCard = NewCard(FluentIcons.ForSensorType(SensorType.Power), "Potência", "W", "Consumo atual");
        _loadCard = NewCard(FluentIcons.ForSensorType(SensorType.Load), "Carga", "%", "GPU Core");
        _metrics.AddCard(_coreClockCard);
        _metrics.AddCard(_memClockCard);
        _metrics.AddCard(_tempCard);
        _metrics.AddCard(_powerCard);
        _metrics.AddCard(_loadCard);

        // 2. Toolbar: card selector on the left, safety state on the right.
        _toolbar = new ToolbarRow();
        _gpuSelectLabel = new Label { Text = "Placa gráfica", AutoSize = true, Font = FluentStyle.UiFont };
        _gpuCombo = new FluentComboBox { Width = LogicalToDeviceUnits(280) };
        _gpuCombo.SelectedIndexChanged += GpuCombo_SelectedIndexChanged;
        _safetyBadge = new FluentBadge { Height = LogicalToDeviceUnits(24), Tag = PageMetrics.AlignRight };
        _safetyBadge.SetBadge("Proteção VBIOS ativa · watchdog de 15 s", BadgeStyle.Success);
        _toolbar.Controls.Add(_gpuSelectLabel);
        _toolbar.Controls.Add(_gpuCombo);
        _toolbar.Controls.Add(_safetyBadge);
        _toolbar.Height = Math.Max(_toolbar.Height, _gpuCombo.Height);

        // 3. Watchdog banner
        _watchdogBanner = new WatchdogAlertPanel { Visible = false };
        _watchdogLabel = new Label { AutoSize = false, Height = FluentStyle.UiFont.Height + 4, Font = FluentStyle.UiFontSemibold, AutoEllipsis = true, Tag = PageMetrics.Fill, Text = "Perfil em teste. Reverte automaticamente em 15 s." };
        _btnKeep = PageMetrics.FitButton(new FluentButton { Text = "Confirmar e manter", Variant = FluentButtonVariant.Accent, Tag = PageMetrics.AlignRight });
        _btnKeep.Click += (_, _) => _engine?.ConfirmProfile();
        _btnRevert = PageMetrics.FitButton(new FluentButton { Text = "Reverter agora", Tag = PageMetrics.AlignRight });
        _btnRevert.Click += (_, _) => _engine?.CancelTestAndRevert("Revertido manualmente pelo utilizador.");
        _watchdogBanner.Controls.Add(_watchdogLabel);
        _watchdogBanner.Controls.Add(_btnKeep);
        _watchdogBanner.Controls.Add(_btnRevert);

        // 4. Settings card
        _settingsCard = new FluentSectionCard
        {
            Title = "Frequências e limites",
            Subtitle = "Intervalos limitados pela VBIOS. Se o sistema reiniciar durante um teste, o overclock é desativado.",
            AutoHeight = true
        };

        ToolbarRow presets = new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };
        _btnEco = PageMetrics.FitButton(new FluentButton { Text = "Silencioso" });
        _btnEco.Click += (_, _) => ApplyPreset(0);
        _btnBalanced = PageMetrics.FitButton(new FluentButton { Text = "Padrão de fábrica" });
        _btnBalanced.Click += (_, _) => ApplyPreset(1);
        _btnMaxPerf = PageMetrics.FitButton(new FluentButton { Text = "Desempenho" });
        _btnMaxPerf.Click += (_, _) => ApplyPreset(2);
        presets.Controls.Add(_btnEco);
        presets.Controls.Add(_btnBalanced);
        presets.Controls.Add(_btnMaxPerf);

        _coreSlider = new FluentSlider { Minimum = -200, Maximum = 200, Value = 0, SmallChange = 5, LargeChange = 25 };
        _coreSlider.Scroll += (_, _) => { UpdateValueLabels(); MarkCustom(); };
        _memSlider = new FluentSlider { Minimum = -500, Maximum = 500, Value = 0, SmallChange = 10, LargeChange = 50 };
        _memSlider.Scroll += (_, _) => { UpdateValueLabels(); MarkCustom(); };
        _powerSlider = new FluentSlider { Minimum = 70, Maximum = 115, Value = 100, SmallChange = 1, LargeChange = 5 };
        _powerSlider.Scroll += (_, _) => { UpdateValueLabels(); MarkCustom(); };
        _tempLimitSlider = new FluentSlider { Minimum = 65, Maximum = 88, Value = StockTempLimitC, SmallChange = 1, LargeChange = 5 };
        _tempLimitSlider.Scroll += (_, _) => { UpdateValueLabels(); MarkCustom(); };

        _coreRow = new SettingRow("Offset do núcleo", "Desvio da frequência do GPU", _coreSlider);
        _memRow = new SettingRow("Offset da VRAM", "Desvio da frequência da memória", _memSlider);
        _powerRow = new SettingRow("Limite de potência", "Percentagem do TDP de fábrica", _powerSlider);
        _tempLimitRow = new SettingRow("Limite térmico", "Temperatura alvo antes de reduzir frequência", _tempLimitSlider);

        StackPanel rows = new() { Dock = DockStyle.Top, Gap = 0, OnSurface = true };
        rows.Controls.Add(new SettingRow("Perfil rápido", "Configurações pré-testadas", presets, false) { ShowDivider = false });
        rows.Controls.Add(_coreRow);
        rows.Controls.Add(_memRow);
        rows.Controls.Add(_powerRow);
        rows.Controls.Add(_tempLimitRow);
        _settingsCard.Controls.Add(rows);
        _settingsCard.Padding = new Padding(_settingsCard.Padding.Left, _settingsCard.Padding.Top, _settingsCard.Padding.Right, LogicalToDeviceUnits(4));

        // 5. Actions
        _actions = new ToolbarRow();
        _btnTest = PageMetrics.FitButton(new FluentButton { Text = "Testar durante 15 s", Variant = FluentButtonVariant.Accent });
        _btnTest.Click += BtnTest_Click;
        _btnResetStock = PageMetrics.FitButton(new FluentButton { Text = "Repor valores da placa" });
        _btnResetStock.Click += BtnResetStock_Click;
        _chkApplyStartup = new CheckBox { Text = "Aplicar no arranque do Windows", AutoSize = true, Font = FluentStyle.UiFont, Tag = PageMetrics.AlignRight };
        _chkApplyStartup.CheckedChanged += (_, _) =>
        {
            if (_selectedDevice == null)
                return;

            _selectedDevice.CurrentState.ApplyOnStartup = _chkApplyStartup.Checked;
            _engine?.SaveSettings(_selectedDevice);
        };
        _actions.Controls.Add(_btnTest);
        _actions.Controls.Add(_btnResetStock);
        _actions.Controls.Add(_chkApplyStartup);

        _statusMessageLabel = new Label { AutoSize = false, Height = FluentStyle.UiFont.Height + 4, Font = FluentStyle.UiFont, AutoEllipsis = true, Visible = false };

        _page = new StackPanel { Dock = DockStyle.Top };
        _page.Controls.Add(_metrics);
        _page.Controls.Add(_toolbar);
        _page.Controls.Add(_watchdogBanner);
        _page.Controls.Add(_settingsCard);
        _page.Controls.Add(_actions);
        _page.Controls.Add(_statusMessageLabel);
        Controls.Add(_page);

        UpdateValueLabels();
        UpdatePresetButtonsHighlight();

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

    public void SetEngine(GpuTuningEngine engine)
    {
        if (_engine != null)
        {
            _engine.DevicesChanged -= Engine_DevicesChanged;
            _engine.WatchdogTick -= Engine_WatchdogTick;
            _engine.WatchdogReverted -= Engine_WatchdogReverted;
            _engine.TestConfirmed -= Engine_TestConfirmed;
        }

        _engine = engine;
        if (_engine == null)
            return;

        _engine.DevicesChanged += Engine_DevicesChanged;
        _engine.WatchdogTick += Engine_WatchdogTick;
        _engine.WatchdogReverted += Engine_WatchdogReverted;
        _engine.TestConfirmed += Engine_TestConfirmed;

        PopulateDevices();
        UpdateTelemetry();
    }

    private void Engine_DevicesChanged(object sender, EventArgs e) => PopulateDevices();

    private void PopulateDevices()
    {
        _gpuCombo.Items.Clear();
        if (_engine == null || _engine.Devices.Count == 0)
        {
            _gpuCombo.Items.Add("Nenhuma GPU compatível");
            _gpuCombo.SelectedIndex = 0;
            _selectedDevice = null;
            UpdateUiFromState();
            return;
        }

        foreach (IGpuTuningDevice dev in _engine.Devices)
            _gpuCombo.Items.Add(dev.Name);

        _gpuCombo.SelectedIndex = 0;
        _selectedDevice = _engine.Devices[0];
        UpdateUiFromState();
    }

    private void GpuCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_engine == null || _gpuCombo.SelectedIndex < 0 || _gpuCombo.SelectedIndex >= _engine.Devices.Count)
            return;

        _selectedDevice = _engine.Devices[_gpuCombo.SelectedIndex];
        UpdateUiFromState();
        UpdateTelemetry();
    }

    private void MarkCustom()
    {
        _selectedPresetIndex = -1;
        UpdatePresetButtonsHighlight();
    }

    private void UpdateValueLabels()
    {
        _coreRow.SetValue($"{FormatSigned(_coreSlider.Value)} MHz", "Padrão +0 MHz");
        _memRow.SetValue($"{FormatSigned(_memSlider.Value)} MHz", "Padrão +0 MHz");
        _powerRow.SetValue($"{_powerSlider.Value} %", "Padrão 100 %");
        _tempLimitRow.SetValue($"{_tempLimitSlider.Value} °C", $"Padrão {StockTempLimitC} °C");
    }

    private void UpdateUiFromState()
    {
        bool hasDevice = _selectedDevice != null;
        _btnTest.Enabled = hasDevice;
        _btnResetStock.Enabled = hasDevice;
        _btnEco.Enabled = hasDevice;
        _btnBalanced.Enabled = hasDevice;
        _btnMaxPerf.Enabled = hasDevice;

        if (!hasDevice)
        {
            _coreSlider.Enabled = false;
            _memSlider.Enabled = false;
            _powerSlider.Enabled = false;
            _tempLimitSlider.Enabled = false;
            InvalidateRows();
            return;
        }

        GpuTuningState s = _selectedDevice.CurrentState;
        GpuTuningCapabilities c = _selectedDevice.Capabilities;

        Configure(_coreSlider, c.CanAdjustCoreClock, c.MinCoreOffsetMhz, c.MaxCoreOffsetMhz, s.CoreClockOffsetMhz);
        Configure(_memSlider, c.CanAdjustMemoryClock, c.MinMemoryOffsetMhz, c.MaxMemoryOffsetMhz, s.MemoryClockOffsetMhz);
        Configure(_powerSlider, c.CanAdjustPowerLimit, c.MinPowerLimitPercent, c.MaxPowerLimitPercent, s.PowerLimitPercent);
        Configure(_tempLimitSlider, c.CanAdjustTempLimit, c.MinTempLimitC, c.MaxTempLimitC, s.TempLimitC);

        _chkApplyStartup.Checked = s.ApplyOnStartup;
        UpdateValueLabels();
        UpdatePresetButtonsHighlight();
        InvalidateRows();
    }

    private static void Configure(FluentSlider slider, bool enabled, int min, int max, int value)
    {
        slider.Enabled = enabled;
        slider.Minimum = min;
        slider.Maximum = max;
        slider.Value = Math.Max(min, Math.Min(max, value));
    }

    private void InvalidateRows()
    {
        _coreRow.Invalidate();
        _memRow.Invalidate();
        _powerRow.Invalidate();
        _tempLimitRow.Invalidate();
    }

    private void ApplyPreset(int index)
    {
        if (_selectedDevice == null)
            return;

        switch (index)
        {
            case 0: // Eco
                _coreSlider.Value = Math.Max(_coreSlider.Minimum, -50);
                _memSlider.Value = 0;
                _powerSlider.Value = Math.Max(_powerSlider.Minimum, 85);
                _tempLimitSlider.Value = Math.Max(_tempLimitSlider.Minimum, 75);
                break;

            case 1: // Stock
                _coreSlider.Value = 0;
                _memSlider.Value = 0;
                _powerSlider.Value = 100;
                _tempLimitSlider.Value = StockTempLimitC;
                break;

            case 2: // Moderate performance
                _coreSlider.Value = Math.Min(_coreSlider.Maximum, 60);
                _memSlider.Value = Math.Min(_memSlider.Maximum, 150);
                _powerSlider.Value = Math.Min(_powerSlider.Maximum, 105);
                _tempLimitSlider.Value = Math.Min(_tempLimitSlider.Maximum, 85);
                break;
        }

        _selectedPresetIndex = index;
        UpdateValueLabels();
        UpdatePresetButtonsHighlight();
    }

    private void UpdatePresetButtonsHighlight()
    {
        _btnEco.IsActive = _selectedPresetIndex == 0;
        _btnBalanced.IsActive = _selectedPresetIndex == 1;
        _btnMaxPerf.IsActive = _selectedPresetIndex == 2;
    }

    private void ShowStatus(string text, Color color)
    {
        _statusMessageLabel.Text = text ?? string.Empty;
        _statusMessageLabel.ForeColor = color;
        _statusMessageLabel.Visible = _statusMessageLabel.Text.Length > 0;
    }

    private void BtnTest_Click(object sender, EventArgs e)
    {
        if (_selectedDevice == null || _engine == null)
            return;

        GpuTuningState newState = new()
        {
            CoreClockOffsetMhz = _coreSlider.Value,
            MemoryClockOffsetMhz = _memSlider.Value,
            PowerLimitPercent = _powerSlider.Value,
            TempLimitC = _tempLimitSlider.Value,
            ApplyOnStartup = _chkApplyStartup.Checked
        };

        if (_engine.StartWatchdogTest(_selectedDevice, newState, out string error))
        {
            _watchdogBanner.Visible = true;
            ShowStatus(string.Empty, Theme.Current.ForegroundColor);
        }
        else
        {
            ShowStatus(error ?? "Não foi possível iniciar o teste.", Theme.Current.StateHotColor);
        }
    }

    private void BtnResetStock_Click(object sender, EventArgs e)
    {
        if (_selectedDevice == null || _engine == null)
            return;

        if (_engine.ResetDeviceToStock(_selectedDevice, out string error))
        {
            _selectedPresetIndex = 1;
            UpdateUiFromState();
            _watchdogBanner.Visible = false;
            ShowStatus("Valores de fábrica da placa repostos.", Theme.Current.StateCoolColor);
        }
        else
        {
            ShowStatus(error ?? "Não foi possível repor os valores.", Theme.Current.StateHotColor);
        }
    }

    private void Engine_WatchdogTick(object sender, int seconds)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Engine_WatchdogTick(sender, seconds)));
            return;
        }

        _watchdogLabel.Text = $"Perfil em teste. Reverte automaticamente em {seconds} s se não confirmar.";
    }

    private void Engine_WatchdogReverted(object sender, string reason)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Engine_WatchdogReverted(sender, reason)));
            return;
        }

        _watchdogBanner.Visible = false;
        UpdateUiFromState();
        ShowStatus(reason, Theme.Current.StateHotColor);
    }

    private void Engine_TestConfirmed(object sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Engine_TestConfirmed(sender, e)));
            return;
        }

        _watchdogBanner.Visible = false;
        ShowStatus("Perfil confirmado e gravado.", Theme.Current.StateCoolColor);
    }

    public void UpdateTelemetry()
    {
        if (_selectedDevice == null || !Visible)
            return;

        GpuTuningTelemetry t = _selectedDevice.ReadTelemetry();

        if (t.CoreClockMhz is > 0f)
        {
            float clock = t.CoreClockMhz.Value;
            _coreClockCard.SetMetric("Frequência do núcleo", $"{clock:F0}", "MHz", "GPU Core", MetricCardSeverity.Normal, clock / 2500f);
        }
        else
        {
            _coreClockCard.SetMetric("Frequência do núcleo", "-", "MHz", "GPU Core");
        }

        if (t.MemoryClockMhz is > 0f)
        {
            float mem = t.MemoryClockMhz.Value;
            _memClockCard.SetMetric("Frequência da VRAM", $"{mem:F0}", "MHz", "Memória de vídeo", MetricCardSeverity.Normal, mem / 10000f);
        }
        else
        {
            _memClockCard.SetMetric("Frequência da VRAM", "-", "MHz", "Memória de vídeo");
        }

        if (t.TemperatureC is > 0f)
        {
            float temp = t.TemperatureC.Value;
            MetricCardSeverity sev = temp >= 83f ? MetricCardSeverity.Hot : (temp >= 72f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal);
            _tempCard.SetMetric("Temperatura", $"{temp:F0}", "°C", "GPU Core", sev, temp / 100f);
        }
        else
        {
            _tempCard.SetMetric("Temperatura", "-", "°C", "GPU Core");
        }

        if (t.PowerWatts is > 0f)
        {
            float power = t.PowerWatts.Value;
            _powerCard.SetMetric("Potência", $"{power:F1}", "W", "Consumo atual", power > 250f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, power / 350f);
        }
        else if (t.PowerPercent is > 0f)
        {
            float pct = t.PowerPercent.Value;
            _powerCard.SetMetric("Potência", $"{pct:F0}", "%", "do limite TDP", pct > 100f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, pct / 120f);
        }
        else
        {
            _powerCard.SetMetric("Potência", "-", "W", "Consumo atual");
        }

        if (t.LoadPercent.HasValue)
        {
            float load = t.LoadPercent.Value;
            _loadCard.SetMetric("Carga", $"{load:F0}", "%", "GPU Core", load > 85f ? MetricCardSeverity.Hot : MetricCardSeverity.Normal, load / 100f);
        }
        else
        {
            _loadCard.SetMetric("Carga", "-", "%", "GPU Core");
        }
    }

    private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;

        theme.Apply(_page);
        _gpuSelectLabel.ForeColor = theme.MutedForegroundColor;

        Invalidate(true);
    }
}
