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

public sealed class CpuTuningPanel : UserControl, IThemedControl
{
    private CpuTuningEngine _engine;
    private int _selectedPresetIndex = 1; // 0 = Eco, 1 = Balanced, 2 = MaxPerf, -1 = Custom

    // 1. Header
    private readonly Panel _headerPanel;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly FluentBadge _safetyBadge;

    // 2. Telemetria (5 cartões modernos)
    private readonly Panel _telemetryRow;
    private readonly FluentMetricCard _tempCard;
    private readonly FluentMetricCard _voltageCard;
    private readonly FluentMetricCard _powerCard;
    private readonly FluentMetricCard _clockCard;
    private readonly FluentMetricCard _loadCard;

    // 3. Perfis Rápidos & Segurança Certificada
    private readonly Panel _presetsContainer;
    private readonly FluentSectionCard _presetsCard;
    private readonly Label _safetyStockLabel;
    private readonly Label _safetyDescLabel;
    private readonly FluentButton _btnEco;
    private readonly FluentButton _btnBalanced;
    private readonly FluentButton _btnMaxPerf;

    // 4. Banner de Watchdog (teste de 15s)
    private readonly Panel _watchdogContainer;
    private readonly WatchdogAlertPanel _watchdogBanner;
    private readonly Label _watchdogLabel;
    private readonly FluentButton _btnKeep;
    private readonly FluentButton _btnRevert;

    // 5. Parâmetros e Ajuste Fino
    private readonly Panel _controlsContainer;
    private readonly FluentSectionCard _controlsCard;
    private readonly Label _boostLabel;
    private readonly FluentComboBox _boostCombo;
    private readonly Label _boostHelpLabel;

    private readonly Label _throttleMaxLabel;
    private readonly FluentSlider _throttleMaxSlider;
    private readonly Label _throttleMaxValLabel;

    private readonly Label _pl1Label;
    private readonly FluentSlider _pl1Slider;
    private readonly Label _pl1ValLabel;

    private readonly Label _pl2Label;
    private readonly FluentSlider _pl2Slider;
    private readonly Label _pl2ValLabel;

    private readonly Label _raplInfoLabel;

    // 6. Barra de Ações
    private readonly Panel _actionContainer;
    private readonly Panel _actionBar;
    private readonly FluentButton _btnTest;
    private readonly FluentButton _btnResetStock;
    private readonly CheckBox _chkApplyStartup;
    private readonly Label _statusMessageLabel;

    public CpuTuningPanel() : this(null)
    {
    }

    public CpuTuningPanel(CpuTuningEngine engine)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Dock = DockStyle.Fill;
        Padding = new Padding(20);
        AutoScroll = true;

        int gap = LogicalToDeviceUnits(16);

        // 1. Header (Espaçoso e elegante)
        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 76, Margin = new Padding(0) };
        _titleLabel = new Label
        {
            Text = "Ajuste de Processador (CPU Tuning)",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 14f),
            Location = new Point(0, 0),
            AutoSize = true
        };

        _subtitleLabel = new Label
        {
            Text = "Controlo de limites de consumo (PL1/PL2) e políticas energéticas com proteções de hardware ativas.",
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 9f),
            Location = new Point(0, 26),
            AutoSize = true
        };

        _safetyBadge = new FluentBadge
        {
            Location = new Point(0, 50),
            Height = 24,
            Style = BadgeStyle.Success
        };
        _safetyBadge.SetBadge("Proteção de Hardware Ativa • Clamping Automático & Watchdog", BadgeStyle.Success);

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_safetyBadge);

        // 2. Telemetria: 5 cartões KPI
        int cardWidth = 170;
        int cardHeight = 104;
        _telemetryRow = new Panel { Dock = DockStyle.Top, Height = cardHeight + gap, Padding = new Padding(0, 0, 0, gap) };

        _tempCard = new FluentMetricCard { Location = new Point(0, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Temperature) };
        _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Sensor Package", MetricCardSeverity.Normal);

        _voltageCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 1, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Voltage) };
        _voltageCard.SetMetric("VOLTAGEM", "--", "V", "CPU VCore", MetricCardSeverity.Normal);

        _powerCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 2, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Power) };
        _powerCard.SetMetric("POTÊNCIA", "--", "W", "Package Power", MetricCardSeverity.Normal);

        _clockCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 3, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Clock) };
        _clockCard.SetMetric("FREQUÊNCIA", "--", "MHz", "Clock Médio", MetricCardSeverity.Normal);

        _loadCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 4, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Load) };
        _loadCard.SetMetric("CARGA TOTAL", "--", "%", "Uso de CPU", MetricCardSeverity.Normal);

        _telemetryRow.Controls.Add(_tempCard);
        _telemetryRow.Controls.Add(_voltageCard);
        _telemetryRow.Controls.Add(_powerCard);
        _telemetryRow.Controls.Add(_clockCard);
        _telemetryRow.Controls.Add(_loadCard);

        // 3. Perfis Rápidos & Segurança Integrada
        _presetsContainer = new Panel { Dock = DockStyle.Top, Height = 144 + gap, Padding = new Padding(0, 0, 0, gap) };
        _presetsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Perfis Rápidos e Garantia de Segurança",
            Subtitle = "Selecione uma configuração pré-testada. Todos os valores respeitam as especificações elétricas do fabricante."
        };

        _safetyStockLabel = new Label
        {
            Text = "Valores Certificados de Fábrica: A carregar...",
            Location = new Point(16, 56),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9f)
        };

        _safetyDescLabel = new Label
        {
            Text = "Clamping de hardware permanente. Em caso de instabilidade num teste, o watchdog repõe os padrões em 15 segundos.",
            Location = new Point(16, 76),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _btnEco = new FluentButton { Text = "🍃 Modo Silencioso (Eco)", Location = new Point(16, 98), Width = 190, Height = 34, Variant = FluentButtonVariant.Standard };
        _btnEco.Click += (_, _) => ApplyPreset(0);

        _btnBalanced = new FluentButton { Text = "✓ Padrão de Fábrica (Stock)", Location = new Point(216, 98), Width = 210, Height = 34, Variant = FluentButtonVariant.Success };
        _btnBalanced.Click += (_, _) => ApplyPreset(1);

        _btnMaxPerf = new FluentButton { Text = "⚡ Desempenho Otimizado", Location = new Point(436, 98), Width = 190, Height = 34, Variant = FluentButtonVariant.Standard };
        _btnMaxPerf.Click += (_, _) => ApplyPreset(2);

        _presetsCard.Controls.Add(_safetyStockLabel);
        _presetsCard.Controls.Add(_safetyDescLabel);
        _presetsCard.Controls.Add(_btnEco);
        _presetsCard.Controls.Add(_btnBalanced);
        _presetsCard.Controls.Add(_btnMaxPerf);
        _presetsContainer.Controls.Add(_presetsCard);

        // 4. Banner de Watchdog (teste de 15s)
        _watchdogContainer = new Panel { Dock = DockStyle.Top, Height = 52 + gap, Padding = new Padding(0, 0, 0, gap), Visible = false };
        _watchdogBanner = new WatchdogAlertPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 8)
        };

        _watchdogLabel = new Label
        {
            Text = "Perfil de CPU em teste temporário! Revertendo automaticamente em 15s...",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f),
            Location = new Point(16, 15),
            AutoSize = true
        };

        _btnKeep = new FluentButton
        {
            Text = "✓ Confirmar e Manter",
            Location = new Point(500, 8),
            Width = 160,
            Height = 34,
            Variant = FluentButtonVariant.Success
        };
        _btnKeep.Click += (_, _) => _engine?.ConfirmProfile();

        _btnRevert = new FluentButton
        {
            Text = "✕ Reverter Agora",
            Location = new Point(670, 8),
            Width = 130,
            Height = 34,
            Variant = FluentButtonVariant.Danger
        };
        _btnRevert.Click += (_, _) => _engine?.CancelTestAndRevert("Revertido manualmente pelo utilizador.");

        _watchdogBanner.Controls.Add(_watchdogLabel);
        _watchdogBanner.Controls.Add(_btnKeep);
        _watchdogBanner.Controls.Add(_btnRevert);
        _watchdogContainer.Controls.Add(_watchdogBanner);

        // 5. Parâmetros e Ajuste Fino
        _controlsContainer = new Panel { Dock = DockStyle.Top, Height = 270 + gap, Padding = new Padding(0, 0, 0, gap) };
        _controlsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Parâmetros e Ajuste Fino de Energia",
            Subtitle = "Ajuste manual com intervalos seguros validados pela arquitetura do processador."
        };

        int rowY = 56;
        int lblX = 16;
        int ctrlX = 240;
        int sliderW = 340;
        int valX = 596;

        _boostLabel = new Label { Text = "Modo de Turbo Boost:", Location = new Point(lblX, rowY + 3), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _boostCombo = new FluentComboBox { Location = new Point(ctrlX, rowY), Width = 300 };
        _boostCombo.Items.AddRange(new object[]
        {
            "0: Desativado (Silêncio absoluto e frio)",
            "1: Ativo (Padrão de Fábrica)",
            "2: Agressivo (Frequência máxima sustentada)",
            "3: Eficiente (Poupança inteligente sob carga)"
        });
        _boostCombo.SelectedIndex = 1;
        _boostCombo.SelectedIndexChanged += (_, _) =>
        {
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        _boostHelpLabel = new Label
        {
            Text = "Comportamento com que os núcleos elevam a frequência.",
            Location = new Point(ctrlX + 312, rowY + 5),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Italic, 8.5f)
        };

        rowY += 46;
        _throttleMaxLabel = new Label { Text = "Estado Máximo da CPU (%):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _throttleMaxSlider = new FluentSlider { Minimum = 50, Maximum = 100, Value = 100, SmallChange = 1, LargeChange = 5, Location = new Point(ctrlX, rowY), Width = sliderW };
        _throttleMaxValLabel = new Label { Text = "100% (Padrão: 100%)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _throttleMaxSlider.Scroll += (_, _) =>
        {
            _throttleMaxValLabel.Text = $"{_throttleMaxSlider.Value}% (Padrão: 100%)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 48;
        _pl1Label = new Label { Text = "Limite Sustentado PL1 (Watts):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _pl1Slider = new FluentSlider { Minimum = 15, Maximum = 253, Value = 65, SmallChange = 1, LargeChange = 10, Location = new Point(ctrlX, rowY), Width = sliderW };
        _pl1ValLabel = new Label { Text = "65 W", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _pl1Slider.Scroll += (_, _) =>
        {
            int def = _engine?.Capabilities.DefaultPl1Watts ?? 65;
            _pl1ValLabel.Text = $"{_pl1Slider.Value} W (Padrão: {def} W)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 48;
        _pl2Label = new Label { Text = "Limite de Rajada PL2 (Watts):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _pl2Slider = new FluentSlider { Minimum = 15, Maximum = 253, Value = 125, SmallChange = 1, LargeChange = 10, Location = new Point(ctrlX, rowY), Width = sliderW };
        _pl2ValLabel = new Label { Text = "125 W", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _pl2Slider.Scroll += (_, _) =>
        {
            int def = _engine?.Capabilities.DefaultPl2Watts ?? 125;
            _pl2ValLabel.Text = $"{_pl2Slider.Value} W (Padrão: {def} W)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 42;
        _raplInfoLabel = new Label
        {
            Text = "Estado: A verificar limites de hardware do processador...",
            Font = FluentStyle.CreateUiFont(FontStyle.Italic, 8.5f),
            Location = new Point(lblX, rowY),
            AutoSize = true
        };

        _controlsCard.Controls.Add(_boostLabel);
        _controlsCard.Controls.Add(_boostCombo);
        _controlsCard.Controls.Add(_boostHelpLabel);
        _controlsCard.Controls.Add(_throttleMaxLabel);
        _controlsCard.Controls.Add(_throttleMaxSlider);
        _controlsCard.Controls.Add(_throttleMaxValLabel);
        _controlsCard.Controls.Add(_pl1Label);
        _controlsCard.Controls.Add(_pl1Slider);
        _controlsCard.Controls.Add(_pl1ValLabel);
        _controlsCard.Controls.Add(_pl2Label);
        _controlsCard.Controls.Add(_pl2Slider);
        _controlsCard.Controls.Add(_pl2ValLabel);
        _controlsCard.Controls.Add(_raplInfoLabel);
        _controlsContainer.Controls.Add(_controlsCard);

        // 6. Barra de Ações (Grande, clara e segura)
        _actionContainer = new Panel { Dock = DockStyle.Top, Height = 70 + gap, Padding = new Padding(0, 0, 0, gap) };
        _actionBar = new Panel { Dock = DockStyle.Fill };
        _btnTest = new FluentButton
        {
            Text = "🛡️ Testar com Segurança (15s)",
            Location = new Point(0, 4),
            Width = 230,
            Height = 36,
            Variant = FluentButtonVariant.Success
        };
        _btnTest.Click += BtnTest_Click;

        _btnResetStock = new FluentButton
        {
            Text = "↺ Repor Valores de Fábrica",
            Location = new Point(242, 4),
            Width = 210,
            Height = 36,
            Variant = FluentButtonVariant.Standard
        };
        _btnResetStock.Click += BtnResetStock_Click;

        _chkApplyStartup = new CheckBox
        {
            Text = "Aplicar perfil no arranque do Windows (protegido por watchdog de segurança)",
            Location = new Point(468, 12),
            AutoSize = true,
            Font = FluentStyle.UiFont
        };
        _chkApplyStartup.CheckedChanged += (_, _) =>
        {
            if (_engine != null)
            {
                _engine.CurrentState.ApplyOnStartup = _chkApplyStartup.Checked;
                _engine.SaveSettings();
            }
        };

        _statusMessageLabel = new Label
        {
            Text = string.Empty,
            Location = new Point(0, 46),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _actionBar.Controls.Add(_btnTest);
        _actionBar.Controls.Add(_btnResetStock);
        _actionBar.Controls.Add(_chkApplyStartup);
        _actionBar.Controls.Add(_statusMessageLabel);
        _actionContainer.Controls.Add(_actionBar);

        // Ordem inversa de adição para dock vertical correto
        Controls.Add(_actionContainer);
        Controls.Add(_controlsContainer);
        Controls.Add(_watchdogContainer);
        Controls.Add(_presetsContainer);
        Controls.Add(_telemetryRow);
        Controls.Add(_headerPanel);

        if (engine != null)
            SetEngine(engine);

        ApplyTheme(Theme.Current);
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
        UpdateStockLabels();
        UpdateTelemetry();
    }

    private void UpdateStockLabels()
    {
        if (_engine == null) return;
        int defPl1 = _engine.Capabilities.DefaultPl1Watts;
        int defPl2 = _engine.Capabilities.DefaultPl2Watts;
        _safetyStockLabel.Text = $"Valores Certificados de Fábrica: PL1 = {defPl1} W | PL2 = {defPl2} W | Estado Máximo = 100% | Turbo Boost = Ativo";
        _pl1ValLabel.Text = $"{_pl1Slider.Value} W (Padrão: {defPl1} W)";
        _pl2ValLabel.Text = $"{_pl2Slider.Value} W (Padrão: {defPl2} W)";
        _throttleMaxValLabel.Text = $"{_throttleMaxSlider.Value}% (Padrão: 100%)";
    }

    private void UpdateUiFromState()
    {
        if (_engine == null) return;

        CpuTuningState s = _engine.CurrentState;
        _throttleMaxSlider.Value = Math.Max(_throttleMaxSlider.Minimum, Math.Min(_throttleMaxSlider.Maximum, s.MaxProcessorStatePercent));
        _throttleMaxValLabel.Text = $"{_throttleMaxSlider.Value}% (Padrão: 100%)";

        _pl1Slider.Value = Math.Max(_pl1Slider.Minimum, Math.Min(_pl1Slider.Maximum, s.Pl1Watts));
        _pl1ValLabel.Text = $"{_pl1Slider.Value} W (Padrão: {_engine.Capabilities.DefaultPl1Watts} W)";

        _pl2Slider.Value = Math.Max(_pl2Slider.Minimum, Math.Min(_pl2Slider.Maximum, s.Pl2Watts));
        _pl2ValLabel.Text = $"{_pl2Slider.Value} W (Padrão: {_engine.Capabilities.DefaultPl2Watts} W)";

        _boostCombo.SelectedIndex = Math.Max(0, Math.Min(3, s.TurboBoostMode));
        _chkApplyStartup.Checked = s.ApplyOnStartup;

        _pl1Slider.Enabled = _engine.Capabilities.CanAdjustRapl;
        _pl2Slider.Enabled = _engine.Capabilities.CanAdjustRapl;
        _raplInfoLabel.Text = $"Modo de Controlo: {_engine.Capabilities.RaplStatusMessage}";

        UpdatePresetButtonsHighlight();
    }

    private void ApplyPreset(int index)
    {
        if (_engine == null) return;
        _selectedPresetIndex = index;
        int defPl1 = _engine.Capabilities.DefaultPl1Watts;
        int defPl2 = _engine.Capabilities.DefaultPl2Watts;

        switch (index)
        {
            case 0: // Eco
                _throttleMaxSlider.Value = 90;
                _boostCombo.SelectedIndex = 0; // Desativado
                _pl1Slider.Value = Math.Max(15, (int)(defPl1 * 0.70f));
                _pl2Slider.Value = Math.Max(15, (int)(defPl2 * 0.70f));
                break;

            case 1: // Balanced / Stock
                _throttleMaxSlider.Value = 100;
                _boostCombo.SelectedIndex = 1; // Ativo
                _pl1Slider.Value = defPl1;
                _pl2Slider.Value = defPl2;
                break;

            case 2: // Max Perf
                _throttleMaxSlider.Value = 100;
                _boostCombo.SelectedIndex = 2; // Agressivo
                _pl1Slider.Value = Math.Min(253, (int)(defPl1 * 1.25f));
                _pl2Slider.Value = Math.Min(253, (int)(defPl2 * 1.25f));
                break;
        }

        UpdateStockLabels();
        UpdatePresetButtonsHighlight();
    }

    private void UpdatePresetButtonsHighlight()
    {
        _btnEco.IsActive = _selectedPresetIndex == 0;
        _btnEco.Variant = _selectedPresetIndex == 0 ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;

        _btnBalanced.IsActive = _selectedPresetIndex == 1;
        _btnBalanced.Variant = _selectedPresetIndex == 1 ? FluentButtonVariant.Success : FluentButtonVariant.Standard;

        _btnMaxPerf.IsActive = _selectedPresetIndex == 2;
        _btnMaxPerf.Variant = _selectedPresetIndex == 2 ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;
    }

    private void BtnTest_Click(object sender, EventArgs e)
    {
        if (_engine == null) return;

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
            _watchdogContainer.Visible = true;
            _statusMessageLabel.Text = "🛡️ Teste de segurança iniciado com sucesso. O watchdog reverterá em 15s caso não confirme.";
            _statusMessageLabel.ForeColor = Theme.Current.AccentColor;
        }
        else
        {
            _statusMessageLabel.Text = error ?? "Erro ao iniciar teste de CPU.";
            _statusMessageLabel.ForeColor = Theme.Current.StateHotColor;
        }
    }

    private void BtnResetStock_Click(object sender, EventArgs e)
    {
        if (_engine == null) return;

        if (_engine.ResetToStock(out string error))
        {
            _selectedPresetIndex = 1;
            UpdateUiFromState();
            _watchdogContainer.Visible = false;
            _statusMessageLabel.Text = "✓ Valores certificados de fábrica repostos com sucesso.";
            _statusMessageLabel.ForeColor = Theme.Current.StateCoolColor;
        }
        else
        {
            _statusMessageLabel.Text = error ?? "Erro ao repor padrões.";
            _statusMessageLabel.ForeColor = Theme.Current.StateHotColor;
        }
    }

    private void Engine_WatchdogTick(object sender, int seconds)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Engine_WatchdogTick(sender, seconds)));
            return;
        }

        _watchdogLabel.Text = $"Perfil de CPU em teste temporário! Revertendo automaticamente em {seconds}s a menos que confirme.";
    }

    private void Engine_WatchdogReverted(object sender, string reason)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Engine_WatchdogReverted(sender, reason)));
            return;
        }

        _watchdogContainer.Visible = false;
        UpdateUiFromState();
        _statusMessageLabel.Text = reason;
        _statusMessageLabel.ForeColor = Theme.Current.StateHotColor;
    }

    private void Engine_TestConfirmed(object sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Engine_TestConfirmed(sender, e)));
            return;
        }

        _watchdogContainer.Visible = false;
        _statusMessageLabel.Text = "✓ Perfil de CPU confirmado e gravado com segurança.";
        _statusMessageLabel.ForeColor = Theme.Current.StateCoolColor;
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

        CpuTuningTelemetry telem = _engine.ReadTelemetry();

        // 1. Temp
        if (telem.PackageTemperatureC.HasValue && telem.PackageTemperatureC.Value > 0f)
        {
            float temp = telem.PackageTemperatureC.Value;
            MetricCardSeverity sev = temp >= 85f ? MetricCardSeverity.Hot : (temp >= 75f ? MetricCardSeverity.Warm : (temp <= 45f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal));
            _tempCard.SetMetric("TEMPERATURA", $"{temp:F0}", "°C", "Sensor Package", sev, temp / 100f);
        }
        else
        {
            _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Sensor Package", MetricCardSeverity.Normal);
        }

        // 2. Voltagem (CPU VCore)
        if (telem.VoltageV.HasValue && telem.VoltageV.Value > 0f)
        {
            float volt = telem.VoltageV.Value;
            MetricCardSeverity sev = volt >= 1.40f ? MetricCardSeverity.Hot : (volt >= 1.30f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal);
            _voltageCard.SetMetric("VOLTAGEM", $"{volt:F3}", "V", "CPU VCore", sev, volt / 1.5f);
        }
        else
        {
            _voltageCard.SetMetric("VOLTAGEM", "--", "V", "CPU VCore", MetricCardSeverity.Normal);
        }

        // 3. Potência
        if (telem.PackagePowerWatts.HasValue && telem.PackagePowerWatts.Value > 0f)
        {
            float power = telem.PackagePowerWatts.Value;
            _powerCard.SetMetric("POTÊNCIA", $"{power:F1}", "W", "Package Power", power > 90f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, power / 150f);
        }
        else
        {
            _powerCard.SetMetric("POTÊNCIA", "--", "W", "Package Power", MetricCardSeverity.Normal);
        }

        // 4. Clock
        if (telem.CoreClockMhz.HasValue && telem.CoreClockMhz.Value > 0f)
        {
            float clock = telem.CoreClockMhz.Value;
            _clockCard.SetMetric("FREQUÊNCIA", $"{clock:F0}", "MHz", "Clock Médio", MetricCardSeverity.Normal, clock / 5000f);
        }
        else
        {
            _clockCard.SetMetric("FREQUÊNCIA", "--", "MHz", "Clock Médio", MetricCardSeverity.Normal);
        }

        // 5. Carga
        if (telem.TotalLoadPercent.HasValue)
        {
            float load = telem.TotalLoadPercent.Value;
            _loadCard.SetMetric("CARGA TOTAL", $"{load:F0}", "%", "Uso de CPU", load > 85f ? MetricCardSeverity.Hot : MetricCardSeverity.Normal, load / 100f);
        }
        else
        {
            _loadCard.SetMetric("CARGA TOTAL", "--", "%", "Uso de CPU", MetricCardSeverity.Normal);
        }
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;

        _titleLabel.ForeColor = theme.ForegroundColor;
        _subtitleLabel.ForeColor = theme.MutedForegroundColor;
        _safetyBadge.ApplyTheme(theme);

        _tempCard.ApplyTheme(theme);
        _voltageCard.ApplyTheme(theme);
        _powerCard.ApplyTheme(theme);
        _clockCard.ApplyTheme(theme);
        _loadCard.ApplyTheme(theme);

        _presetsCard.ApplyTheme(theme);
        _safetyStockLabel.ForeColor = theme.AccentColor;
        _safetyDescLabel.ForeColor = theme.MutedForegroundColor;
        _btnEco.ApplyTheme(theme);
        _btnBalanced.ApplyTheme(theme);
        _btnMaxPerf.ApplyTheme(theme);

        _watchdogBanner.ApplyTheme(theme);
        _watchdogLabel.ForeColor = theme.ForegroundColor;
        _btnKeep.ApplyTheme(theme);
        _btnRevert.ApplyTheme(theme);

        _controlsCard.ApplyTheme(theme);
        _boostLabel.ForeColor = theme.ForegroundColor;
        _boostCombo.ApplyTheme(theme);
        _boostHelpLabel.ForeColor = theme.MutedForegroundColor;

        _throttleMaxLabel.ForeColor = theme.ForegroundColor;
        _throttleMaxSlider.ApplyTheme(theme);
        _throttleMaxValLabel.ForeColor = theme.ForegroundColor;

        _pl1Label.ForeColor = theme.ForegroundColor;
        _pl1Slider.ApplyTheme(theme);
        _pl1ValLabel.ForeColor = theme.ForegroundColor;

        _pl2Label.ForeColor = theme.ForegroundColor;
        _pl2Slider.ApplyTheme(theme);
        _pl2ValLabel.ForeColor = theme.ForegroundColor;
        _raplInfoLabel.ForeColor = theme.MutedForegroundColor;

        _btnTest.ApplyTheme(theme);
        _btnResetStock.ApplyTheme(theme);
        _chkApplyStartup.ForeColor = theme.ForegroundColor;

        Invalidate();
    }
}
