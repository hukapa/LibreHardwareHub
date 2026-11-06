// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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

    private readonly Panel _headerPanel;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly FluentBadge _safetyBadge;

    private readonly Panel _telemetryRow;
    private readonly FluentMetricCard _tempCard;
    private readonly FluentMetricCard _powerCard;
    private readonly FluentMetricCard _clockCard;
    private readonly FluentMetricCard _loadCard;

    private readonly Panel _safetyContainer;
    private readonly FluentSectionCard _safetyCard;
    private readonly Label _safetyDescLabel;
    private readonly Label _safetyStockLabel;

    private readonly Panel _presetsContainer;
    private readonly FluentSectionCard _presetsCard;
    private readonly FluentButton _btnEco;
    private readonly FluentButton _btnBalanced;
    private readonly FluentButton _btnMaxPerf;

    private readonly Panel _watchdogContainer;
    private readonly WatchdogAlertPanel _watchdogBanner;
    private readonly Label _watchdogLabel;
    private readonly FluentButton _btnKeep;
    private readonly FluentButton _btnRevert;

    private readonly Panel _controlsContainer;
    private readonly FluentSectionCard _controlsCard;
    private readonly Label _boostLabel;
    private readonly FluentComboBox _boostCombo;
    private readonly Label _boostHelpLabel;

    private readonly Label _throttleMaxLabel;
    private readonly TrackBar _throttleMaxSlider;
    private readonly Label _throttleMaxValLabel;

    private readonly Label _pl1Label;
    private readonly TrackBar _pl1Slider;
    private readonly Label _pl1ValLabel;

    private readonly Label _pl2Label;
    private readonly TrackBar _pl2Slider;
    private readonly Label _pl2ValLabel;

    private readonly Label _raplInfoLabel;

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
        Padding = new Padding(16);
        AutoScroll = true;

        int gap = LogicalToDeviceUnits(14);

        // 1. Header
        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 80, Margin = new Padding(0) };
        _titleLabel = new Label
        {
            Text = "Ajuste de Processador (CPU Tuning)",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 13.5f),
            Location = new Point(0, 0),
            AutoSize = true
        };

        _subtitleLabel = new Label
        {
            Text = "Controlo de limites de potencia e politicas energeticas balizadas pelo processador.",
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f),
            Location = new Point(0, 26),
            AutoSize = true
        };

        _safetyBadge = new FluentBadge
        {
            Location = new Point(0, 50),
            Height = 22,
            Style = BadgeStyle.Success
        };
        _safetyBadge.SetBadge("Protecao de Hardware Ativa • Risco Zero", BadgeStyle.Success);

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_safetyBadge);

        // 2. Telemetria
        _telemetryRow = new Panel { Dock = DockStyle.Top, Height = 104 + gap, Padding = new Padding(0, 0, 0, gap) };
        _tempCard = new FluentMetricCard { Location = new Point(0, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Temperature) };
        _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Sensor Package", MetricCardSeverity.Normal);

        _powerCard = new FluentMetricCard { Location = new Point(174, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Power) };
        _powerCard.SetMetric("POTENCIA", "--", "W", "Package Power", MetricCardSeverity.Normal);

        _clockCard = new FluentMetricCard { Location = new Point(348, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Clock) };
        _clockCard.SetMetric("FREQUENCIA", "--", "MHz", "Clock Medio", MetricCardSeverity.Normal);

        _loadCard = new FluentMetricCard { Location = new Point(522, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Load) };
        _loadCard.SetMetric("CARGA TOTAL", "--", "%", "Uso de CPU", MetricCardSeverity.Normal);

        _telemetryRow.Controls.Add(_tempCard);
        _telemetryRow.Controls.Add(_powerCard);
        _telemetryRow.Controls.Add(_clockCard);
        _telemetryRow.Controls.Add(_loadCard);

        // 3. Cartao de Seguranca e Garantias
        _safetyContainer = new Panel { Dock = DockStyle.Top, Height = 84 + gap, Padding = new Padding(0, 0, 0, gap) };
        _safetyCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Seguranca e Limites de Hardware Protegidos",
            Subtitle = "Todas as alteracoes respeitam as protecoes termicas e eletricas de fabrica do fabricante."
        };
        _safetyDescLabel = new Label
        {
            Text = "O sistema aplica clamping automatico. Se algum teste for instavel, o watchdog repoe os valores de fabrica em 15 segundos.",
            Location = new Point(16, 52),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };
        _safetyStockLabel = new Label
        {
            Text = "Valores de Fabrica: A carregar...",
            Location = new Point(16, 68),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 8.5f)
        };
        _safetyCard.Controls.Add(_safetyDescLabel);
        _safetyCard.Controls.Add(_safetyStockLabel);
        _safetyContainer.Controls.Add(_safetyCard);

        // 4. Perfis Rapidos
        _presetsContainer = new Panel { Dock = DockStyle.Top, Height = 96 + gap, Padding = new Padding(0, 0, 0, gap) };
        _presetsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Perfis Rapidos Recomendados",
            Subtitle = "Selecione uma configuracao balanceada ou ajuste parametros individuais abaixo."
        };

        _btnEco = new FluentButton { Text = "Eco / Silencioso", Location = new Point(16, 52), Width = 150, Height = 32 };
        _btnEco.Click += (_, _) => ApplyPreset(0);

        _btnBalanced = new FluentButton { Text = "Equilibrado (Fabrica)", Location = new Point(176, 52), Width = 170, Height = 32, IsActive = true };
        _btnBalanced.Click += (_, _) => ApplyPreset(1);

        _btnMaxPerf = new FluentButton { Text = "Desempenho Maximo", Location = new Point(356, 52), Width = 170, Height = 32 };
        _btnMaxPerf.Click += (_, _) => ApplyPreset(2);

        _presetsCard.Controls.Add(_btnEco);
        _presetsCard.Controls.Add(_btnBalanced);
        _presetsCard.Controls.Add(_btnMaxPerf);
        _presetsContainer.Controls.Add(_presetsCard);

        // 5. Banner de Watchdog (teste de 15s)
        _watchdogContainer = new Panel { Dock = DockStyle.Top, Height = 48 + gap, Padding = new Padding(0, 0, 0, gap), Visible = false };
        _watchdogBanner = new WatchdogAlertPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 8, 14, 8)
        };

        _watchdogLabel = new Label
        {
            Text = "Perfil de CPU em teste! Revertendo automaticamente em 15s...",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9f),
            Location = new Point(14, 14),
            AutoSize = true
        };

        _btnKeep = new FluentButton
        {
            Text = "Confirmar e Manter",
            Location = new Point(470, 8),
            Width = 150,
            Height = 32,
            Variant = FluentButtonVariant.Accent
        };
        _btnKeep.Click += (_, _) => _engine?.ConfirmProfile();

        _btnRevert = new FluentButton
        {
            Text = "Reverter",
            Location = new Point(630, 8),
            Width = 100,
            Height = 32,
            Variant = FluentButtonVariant.Danger
        };
        _btnRevert.Click += (_, _) => _engine?.CancelTestAndRevert("Revertido manualmente pelo utilizador.");

        _watchdogBanner.Controls.Add(_watchdogLabel);
        _watchdogBanner.Controls.Add(_btnKeep);
        _watchdogBanner.Controls.Add(_btnRevert);
        _watchdogContainer.Controls.Add(_watchdogBanner);

        // 6. Parametros e Sliders
        _controlsContainer = new Panel { Dock = DockStyle.Top, Height = 280 + gap, Padding = new Padding(0, 0, 0, gap) };
        _controlsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Parametros de Energia e Frequencia",
            Subtitle = "Ajuste fino de throttling do Windows e limites sustentados PL1/PL2 de hardware."
        };

        int rowY = 56;
        int lblX = 16;
        int ctrlX = 230;
        int sliderW = 320;
        int valX = 562;

        _boostLabel = new Label { Text = "Modo de Turbo Boost:", Location = new Point(lblX, rowY + 3), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _boostCombo = new FluentComboBox { Location = new Point(ctrlX, rowY), Width = 280 };
        _boostCombo.Items.AddRange(new object[]
        {
            "0: Desativado (Silencio e Frio)",
            "1: Ativo (Padrao de Fabrica)",
            "2: Agressivo (Frequencia Maxima)",
            "3: Eficiente (Poupanca Inteligente)"
        });
        _boostCombo.SelectedIndex = 1;
        _boostCombo.SelectedIndexChanged += (_, _) => _selectedPresetIndex = -1;

        _boostHelpLabel = new Label
        {
            Text = "Velocidade com que os nucleos elevam o clock sob carga.",
            Location = new Point(ctrlX + 290, rowY + 5),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Italic, 8f)
        };

        rowY += 48;
        _throttleMaxLabel = new Label { Text = "Estado Maximo da CPU (%):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _throttleMaxSlider = new TrackBar { Minimum = 50, Maximum = 100, Value = 100, TickFrequency = 5, Location = new Point(ctrlX, rowY), Width = sliderW };
        _throttleMaxValLabel = new Label { Text = "100% (Padrao: 100%)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _throttleMaxSlider.Scroll += (_, _) =>
        {
            _throttleMaxValLabel.Text = $"{_throttleMaxSlider.Value}% (Padrao: 100%)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 50;
        _pl1Label = new Label { Text = "Limite Sustentado PL1 (Watts):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _pl1Slider = new TrackBar { Minimum = 15, Maximum = 253, Value = 65, TickFrequency = 15, Location = new Point(ctrlX, rowY), Width = sliderW };
        _pl1ValLabel = new Label { Text = "65 W", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _pl1Slider.Scroll += (_, _) =>
        {
            int def = _engine?.Capabilities.DefaultPl1Watts ?? 65;
            _pl1ValLabel.Text = $"{_pl1Slider.Value} W (Padrao: {def} W)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 50;
        _pl2Label = new Label { Text = "Limite de Rajada PL2 (Watts):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _pl2Slider = new TrackBar { Minimum = 15, Maximum = 253, Value = 125, TickFrequency = 15, Location = new Point(ctrlX, rowY), Width = sliderW };
        _pl2ValLabel = new Label { Text = "125 W", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _pl2Slider.Scroll += (_, _) =>
        {
            int def = _engine?.Capabilities.DefaultPl2Watts ?? 125;
            _pl2ValLabel.Text = $"{_pl2Slider.Value} W (Padrao: {def} W)";
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

        // 7. Barra de Acoes
        _actionContainer = new Panel { Dock = DockStyle.Top, Height = 64 + gap, Padding = new Padding(0, 0, 0, gap) };
        _actionBar = new Panel { Dock = DockStyle.Fill };
        _btnTest = new FluentButton
        {
            Text = "Testar com Seguranca (15s)",
            Location = new Point(0, 4),
            Width = 190,
            Height = 34,
            Variant = FluentButtonVariant.Accent
        };
        _btnTest.Click += BtnTest_Click;

        _btnResetStock = new FluentButton
        {
            Text = "Repor Valores de Fabrica",
            Location = new Point(202, 4),
            Width = 190,
            Height = 34,
            Variant = FluentButtonVariant.Standard
        };
        _btnResetStock.Click += BtnResetStock_Click;

        _chkApplyStartup = new CheckBox
        {
            Text = "Aplicar perfil no arranque do Windows (protegido por watchdog de 120s)",
            Location = new Point(410, 11),
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
            Location = new Point(0, 44),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _actionBar.Controls.Add(_btnTest);
        _actionBar.Controls.Add(_btnResetStock);
        _actionBar.Controls.Add(_chkApplyStartup);
        _actionBar.Controls.Add(_statusMessageLabel);
        _actionContainer.Controls.Add(_actionBar);

        // Ordem inversa de adicao para dock vertical correto
        Controls.Add(_actionContainer);
        Controls.Add(_controlsContainer);
        Controls.Add(_watchdogContainer);
        Controls.Add(_presetsContainer);
        Controls.Add(_safetyContainer);
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
        _safetyStockLabel.Text = $"Valores de Fabrica: PL1 = {defPl1} W | PL2 = {defPl2} W | Estado Maximo = 100% | Turbo Boost = Ativo";
        _pl1ValLabel.Text = $"{_pl1Slider.Value} W (Padrao: {defPl1} W)";
        _pl2ValLabel.Text = $"{_pl2Slider.Value} W (Padrao: {defPl2} W)";
        _throttleMaxValLabel.Text = $"{_throttleMaxSlider.Value}% (Padrao: 100%)";
    }

    private void UpdateUiFromState()
    {
        if (_engine == null) return;

        CpuTuningState s = _engine.CurrentState;
        _throttleMaxSlider.Value = Math.Max(_throttleMaxSlider.Minimum, Math.Min(_throttleMaxSlider.Maximum, s.MaxProcessorStatePercent));
        _throttleMaxValLabel.Text = $"{_throttleMaxSlider.Value}% (Padrao: 100%)";

        _pl1Slider.Value = Math.Max(_pl1Slider.Minimum, Math.Min(_pl1Slider.Maximum, s.Pl1Watts));
        _pl1ValLabel.Text = $"{_pl1Slider.Value} W (Padrao: {_engine.Capabilities.DefaultPl1Watts} W)";

        _pl2Slider.Value = Math.Max(_pl2Slider.Minimum, Math.Min(_pl2Slider.Maximum, s.Pl2Watts));
        _pl2ValLabel.Text = $"{_pl2Slider.Value} W (Padrao: {_engine.Capabilities.DefaultPl2Watts} W)";

        _boostCombo.SelectedIndex = Math.Max(0, Math.Min(3, s.TurboBoostMode));
        _chkApplyStartup.Checked = s.ApplyOnStartup;

        _pl1Slider.Enabled = _engine.Capabilities.CanAdjustRapl;
        _pl2Slider.Enabled = _engine.Capabilities.CanAdjustRapl;
        _raplInfoLabel.Text = $"Estado: {_engine.Capabilities.RaplStatusMessage}";

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
        _btnBalanced.IsActive = _selectedPresetIndex == 1;
        _btnMaxPerf.IsActive = _selectedPresetIndex == 2;
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
            _statusMessageLabel.Text = "Teste de seguranca iniciado. Confirme para manter.";
            _statusMessageLabel.ForeColor = Theme.Current.AccentColor;
        }
        else
        {
            _statusMessageLabel.Text = error ?? "Erro ao iniciar teste.";
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
            _statusMessageLabel.Text = "Valores de fabrica (stock) repostos com sucesso.";
            _statusMessageLabel.ForeColor = Theme.Current.StateCoolColor;
        }
        else
        {
            _statusMessageLabel.Text = error ?? "Erro ao repor padroes.";
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

        _watchdogLabel.Text = $"Perfil de CPU em teste! Revertendo automaticamente em {seconds}s a menos que confirme.";
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
        _statusMessageLabel.Text = "Perfil de CPU confirmado e gravado com sucesso.";
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
        if (telem.PackageTemperatureC.HasValue)
        {
            float temp = telem.PackageTemperatureC.Value;
            MetricCardSeverity sev = temp >= 85f ? MetricCardSeverity.Hot : (temp >= 75f ? MetricCardSeverity.Warm : (temp <= 45f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal));
            _tempCard.SetMetric("TEMPERATURA", $"{temp:F0}", "°C", "Package Sensor", sev, temp / 100f);
        }
        else
        {
            _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Package Sensor", MetricCardSeverity.Normal);
        }

        // 2. Potencia
        if (telem.PackagePowerWatts.HasValue)
        {
            float power = telem.PackagePowerWatts.Value;
            _powerCard.SetMetric("POTENCIA", $"{power:F1}", "W", "Package Power", power > 90f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, power / 150f);
        }
        else
        {
            _powerCard.SetMetric("POTENCIA", "--", "W", "Package Power", MetricCardSeverity.Normal);
        }

        // 3. Clock
        if (telem.CoreClockMhz.HasValue)
        {
            float clock = telem.CoreClockMhz.Value;
            _clockCard.SetMetric("FREQUENCIA", $"{clock:F0}", "MHz", "Clock Medio", MetricCardSeverity.Normal, clock / 5000f);
        }
        else
        {
            _clockCard.SetMetric("FREQUENCIA", "--", "MHz", "Clock Medio", MetricCardSeverity.Normal);
        }

        // 4. Carga
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
        _powerCard.ApplyTheme(theme);
        _clockCard.ApplyTheme(theme);
        _loadCard.ApplyTheme(theme);

        _safetyCard.ApplyTheme(theme);
        _safetyDescLabel.ForeColor = theme.ForegroundColor;
        _safetyStockLabel.ForeColor = theme.AccentColor;

        _presetsCard.ApplyTheme(theme);
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
        _throttleMaxValLabel.ForeColor = theme.ForegroundColor;
        _pl1Label.ForeColor = theme.ForegroundColor;
        _pl1ValLabel.ForeColor = theme.ForegroundColor;
        _pl2Label.ForeColor = theme.ForegroundColor;
        _pl2ValLabel.ForeColor = theme.ForegroundColor;
        _raplInfoLabel.ForeColor = theme.MutedForegroundColor;

        _btnTest.ApplyTheme(theme);
        _btnResetStock.ApplyTheme(theme);
        _chkApplyStartup.ForeColor = theme.ForegroundColor;

        Invalidate();
    }
}
