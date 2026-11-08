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
/// CPU tuning page. Follows the dashboard structure: KPI strip, a one-line toolbar, then a settings card
/// (Windows 11 style rows) and the action bar.
/// </summary>
public sealed class CpuTuningPanel : UserControl, IThemedControl
{
    private CpuTuningEngine _engine;
    private int _selectedPresetIndex = 1; // 0 = Eco, 1 = Stock, 2 = Performance, -1 = Custom

    private readonly StackPanel _page;
    private readonly ScrollHost _host;

    // 1. KPI strip
    private readonly MetricStrip _metrics;
    private readonly FluentMetricCard _tempCard;
    private readonly FluentMetricCard _voltageCard;
    private readonly FluentMetricCard _powerCard;
    private readonly FluentMetricCard _clockCard;
    private readonly FluentMetricCard _loadCard;

    // 2. Toolbar
    private readonly ToolbarRow _toolbar;
    private readonly Label _controlModeLabel;
    private readonly FluentBadge _safetyBadge;

    // 3. Watchdog banner (15 s test)
    private readonly WatchdogAlertPanel _watchdogBanner;
    private readonly Label _watchdogLabel;
    private readonly FluentButton _btnKeep;
    private readonly FluentButton _btnRevert;

    // 4. Settings card
    private readonly FluentSectionCard _settingsCard;
    private readonly FluentButton _btnEco;
    private readonly FluentButton _btnBalanced;
    private readonly FluentButton _btnMaxPerf;
    private readonly FluentComboBox _boostCombo;
    private readonly FluentSlider _throttleMaxSlider;
    private readonly FluentSlider _pl1Slider;
    private readonly FluentSlider _pl2Slider;
    private readonly SettingRow _throttleRow;
    private readonly SettingRow _pl1Row;
    private readonly SettingRow _pl2Row;

    // 5. Actions
    private readonly ToolbarRow _actions;
    private readonly FluentButton _btnTest;
    private readonly FluentButton _btnResetStock;
    private readonly CheckBox _chkApplyStartup;
    private readonly Label _statusMessageLabel;

    public CpuTuningPanel() : this(null)
    {
    }

    public CpuTuningPanel(CpuTuningEngine engine)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Dock = DockStyle.Fill;

        // 1. KPI strip
        _metrics = new MetricStrip();
        _tempCard = NewCard(FluentIcons.ForSensorType(SensorType.Temperature), "Temperatura", "°C", "CPU Package");
        _voltageCard = NewCard(FluentIcons.ForSensorType(SensorType.Voltage), "Voltagem", "V", "CPU VCore");
        _powerCard = NewCard(FluentIcons.ForSensorType(SensorType.Power), "Potência", "W", "Package Power");
        _clockCard = NewCard(FluentIcons.ForSensorType(SensorType.Clock), "Frequência", "MHz", "Média dos núcleos");
        _loadCard = NewCard(FluentIcons.ForSensorType(SensorType.Load), "Carga", "%", "Total da CPU");
        _metrics.AddCard(_tempCard);
        _metrics.AddCard(_voltageCard);
        _metrics.AddCard(_powerCard);
        _metrics.AddCard(_clockCard);
        _metrics.AddCard(_loadCard);

        // 2. Toolbar
        _toolbar = new ToolbarRow();
        _controlModeLabel = new Label { AutoSize = false, Height = FluentStyle.UiFont.Height + 4, Font = FluentStyle.UiFont, AutoEllipsis = true, Tag = PageMetrics.Fill, Text = "A verificar o controlo de energia do processador…" };
        _safetyBadge = new FluentBadge { Height = LogicalToDeviceUnits(24), Tag = PageMetrics.AlignRight };
        _safetyBadge.SetBadge("Proteção ativa · watchdog de 15 s", BadgeStyle.Success);
        _toolbar.Controls.Add(_controlModeLabel);
        _toolbar.Controls.Add(_safetyBadge);

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
            Title = "Energia e desempenho",
            Subtitle = "Valores limitados às especificações do fabricante. Cada alteração é testada 15 s antes de ser gravada.",
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

        _boostCombo = new FluentComboBox();
        _boostCombo.Items.AddRange(new object[] { "Desativado", "Ativo (padrão)", "Agressivo", "Eficiente" });
        _boostCombo.SelectedIndex = 1;
        _boostCombo.SelectedIndexChanged += (_, _) => MarkCustom();

        _throttleMaxSlider = new FluentSlider { Minimum = 50, Maximum = 100, Value = 100, SmallChange = 1, LargeChange = 5 };
        _throttleMaxSlider.Scroll += (_, _) => { UpdateValueLabels(); MarkCustom(); };

        _pl1Slider = new FluentSlider { Minimum = 15, Maximum = 253, Value = 65, SmallChange = 1, LargeChange = 10 };
        _pl1Slider.Scroll += (_, _) => { UpdateValueLabels(); MarkCustom(); };

        _pl2Slider = new FluentSlider { Minimum = 15, Maximum = 253, Value = 125, SmallChange = 1, LargeChange = 10 };
        _pl2Slider.Scroll += (_, _) => { UpdateValueLabels(); MarkCustom(); };

        _throttleRow = new SettingRow("Estado máximo do processador", "Teto de frequência no plano de energia do Windows", _throttleMaxSlider);
        _pl1Row = new SettingRow("Limite sustentado (PL1)", "Potência que a CPU pode manter continuamente", _pl1Slider);
        _pl2Row = new SettingRow("Limite de pico (PL2)", "Potência permitida durante picos curtos", _pl2Slider);

        StackPanel rows = new() { Dock = DockStyle.Top, Gap = 0, OnSurface = true };
        rows.Controls.Add(new SettingRow("Perfil rápido", "Configurações pré-testadas", presets, false) { ShowDivider = false });
        rows.Controls.Add(new SettingRow("Turbo Boost", "Como os núcleos sobem de frequência", _boostCombo, false));
        rows.Controls.Add(_throttleRow);
        rows.Controls.Add(_pl1Row);
        rows.Controls.Add(_pl2Row);
        _settingsCard.Controls.Add(rows);
        _settingsCard.Padding = new Padding(_settingsCard.Padding.Left, _settingsCard.Padding.Top, _settingsCard.Padding.Right, LogicalToDeviceUnits(4));

        // 5. Actions
        _actions = new ToolbarRow();
        _btnTest = PageMetrics.FitButton(new FluentButton { Text = "Testar durante 15 s", Variant = FluentButtonVariant.Accent });
        _btnTest.Click += BtnTest_Click;
        _btnResetStock = PageMetrics.FitButton(new FluentButton { Text = "Repor valores de fábrica" });
        _btnResetStock.Click += BtnResetStock_Click;
        _chkApplyStartup = new CheckBox { Text = "Aplicar no arranque do Windows", AutoSize = true, Font = FluentStyle.UiFont, Tag = PageMetrics.AlignRight };
        _chkApplyStartup.CheckedChanged += (_, _) =>
        {
            if (_engine == null)
                return;

            _engine.CurrentState.ApplyOnStartup = _chkApplyStartup.Checked;
            _engine.SaveSettings();
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
        _host = new ScrollHost(_page) { Dock = DockStyle.Fill };
        Controls.Add(_host);

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

    public void SetEngine(CpuTuningEngine engine)
    {
        if (_engine != null)
        {
            _engine.WatchdogTick -= Engine_WatchdogTick;
            _engine.WatchdogReverted -= Engine_WatchdogReverted;
            _engine.TestConfirmed -= Engine_TestConfirmed;
            _engine.StateChanged -= Engine_StateChanged;
        }

        _engine = engine;
        if (_engine == null)
            return;

        _engine.WatchdogTick += Engine_WatchdogTick;
        _engine.WatchdogReverted += Engine_WatchdogReverted;
        _engine.TestConfirmed += Engine_TestConfirmed;
        _engine.StateChanged += Engine_StateChanged;

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
        int defPl1 = _engine?.Capabilities.DefaultPl1Watts ?? 65;
        int defPl2 = _engine?.Capabilities.DefaultPl2Watts ?? 125;
        _throttleRow.SetValue($"{_throttleMaxSlider.Value} %", "Padrão 100 %");
        _pl1Row.SetValue($"{_pl1Slider.Value} W", $"Padrão {defPl1} W");
        _pl2Row.SetValue($"{_pl2Slider.Value} W", $"Padrão {defPl2} W");
    }

    private void UpdateUiFromState()
    {
        if (_engine == null)
            return;

        CpuTuningState s = _engine.CurrentState;
        _throttleMaxSlider.Value = Math.Max(_throttleMaxSlider.Minimum, Math.Min(_throttleMaxSlider.Maximum, s.MaxProcessorStatePercent));
        _pl1Slider.Value = Math.Max(_pl1Slider.Minimum, Math.Min(_pl1Slider.Maximum, s.Pl1Watts));
        _pl2Slider.Value = Math.Max(_pl2Slider.Minimum, Math.Min(_pl2Slider.Maximum, s.Pl2Watts));
        _boostCombo.SelectedIndex = Math.Max(0, Math.Min(3, s.TurboBoostMode));
        _chkApplyStartup.Checked = s.ApplyOnStartup;

        bool rapl = _engine.Capabilities.CanAdjustRapl;
        _pl1Slider.Enabled = rapl;
        _pl2Slider.Enabled = rapl;
        _pl1Row.Invalidate();
        _pl2Row.Invalidate();
        _controlModeLabel.Text = $"Modo de controlo: {_engine.Capabilities.RaplStatusMessage}";

        UpdateValueLabels();
        UpdatePresetButtonsHighlight();
    }

    private void ApplyPreset(int index)
    {
        if (_engine == null)
            return;

        int defPl1 = _engine.Capabilities.DefaultPl1Watts;
        int defPl2 = _engine.Capabilities.DefaultPl2Watts;

        switch (index)
        {
            case 0: // Eco
                _throttleMaxSlider.Value = 90;
                _boostCombo.SelectedIndex = 0;
                _pl1Slider.Value = Math.Max(15, (int)(defPl1 * 0.70f));
                _pl2Slider.Value = Math.Max(15, (int)(defPl2 * 0.70f));
                break;

            case 1: // Stock
                _throttleMaxSlider.Value = 100;
                _boostCombo.SelectedIndex = 1;
                _pl1Slider.Value = defPl1;
                _pl2Slider.Value = defPl2;
                break;

            case 2: // Performance
                _throttleMaxSlider.Value = 100;
                _boostCombo.SelectedIndex = 2;
                _pl1Slider.Value = Math.Min(253, (int)(defPl1 * 1.25f));
                _pl2Slider.Value = Math.Min(253, (int)(defPl2 * 1.25f));
                break;
        }

        // Set after the editors: their change handlers mark the profile as custom.
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
        if (_engine == null)
            return;

        CpuTuningState newState = new()
        {
            MaxProcessorStatePercent = _throttleMaxSlider.Value,
            TurboBoostMode = _boostCombo.SelectedIndex,
            Pl1Watts = _pl1Slider.Value,
            Pl2Watts = _pl2Slider.Value,
            ApplyOnStartup = _chkApplyStartup.Checked
        };

        if (_engine.StartWatchdogTest(newState, out string error))
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
        if (_engine == null)
            return;

        if (_engine.ResetToStock(out string error))
        {
            UpdateUiFromState();
            _selectedPresetIndex = 1;
            UpdatePresetButtonsHighlight();
            _watchdogBanner.Visible = false;
            ShowStatus("Valores de fábrica repostos.", Theme.Current.StateCoolColor);
        }
        else
        {
            ShowStatus(error ?? "Não foi possível repor os valores de fábrica.", Theme.Current.StateHotColor);
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

    private void Engine_StateChanged(object sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(UpdateUiFromState));
            return;
        }

        UpdateUiFromState();
    }

    public void UpdateTelemetry()
    {
        if (_engine == null || !Visible)
            return;

        CpuTuningTelemetry t = _engine.ReadTelemetry();

        if (t.PackageTemperatureC is > 0f)
        {
            float temp = t.PackageTemperatureC.Value;
            MetricCardSeverity sev = temp >= 85f ? MetricCardSeverity.Hot : (temp >= 75f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal);
            _tempCard.SetMetric("Temperatura", $"{temp:F0}", "°C", "CPU Package", sev, temp / 100f);
        }
        else
        {
            _tempCard.SetMetric("Temperatura", "-", "°C", "CPU Package");
        }

        if (t.VoltageV is > 0f)
        {
            float volt = t.VoltageV.Value;
            MetricCardSeverity sev = volt >= 1.48f ? MetricCardSeverity.Hot : (volt >= 1.40f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal);
            _voltageCard.SetMetric("Voltagem", $"{volt:F3}", "V", "CPU VCore", sev, volt / 1.6f);
        }
        else
        {
            _voltageCard.SetMetric("Voltagem", "-", "V", "CPU VCore");
        }

        if (t.PackagePowerWatts is > 0f)
        {
            float power = t.PackagePowerWatts.Value;
            _powerCard.SetMetric("Potência", $"{power:F1}", "W", "Package Power", power > 90f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, power / 150f);
        }
        else
        {
            _powerCard.SetMetric("Potência", "-", "W", "Package Power");
        }

        if (t.CoreClockMhz is > 0f)
        {
            float clock = t.CoreClockMhz.Value;
            _clockCard.SetMetric("Frequência", $"{clock:F0}", "MHz", "Média dos núcleos", MetricCardSeverity.Normal, clock / 5000f);
        }
        else
        {
            _clockCard.SetMetric("Frequência", "-", "MHz", "Média dos núcleos");
        }

        if (t.TotalLoadPercent.HasValue)
        {
            float load = t.TotalLoadPercent.Value;
            _loadCard.SetMetric("Carga", $"{load:F0}", "%", "Total da CPU", load > 85f ? MetricCardSeverity.Hot : MetricCardSeverity.Normal, load / 100f);
        }
        else
        {
            _loadCard.SetMetric("Carga", "-", "%", "Total da CPU");
        }
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;

        theme.Apply(_host);
        _controlModeLabel.ForeColor = theme.MutedForegroundColor;

        Invalidate(true);
    }
}
