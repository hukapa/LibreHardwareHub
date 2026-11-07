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

public sealed class GpuTuningPanel : UserControl, IThemedControl
{
    private GpuTuningEngine _engine;
    private IGpuTuningDevice _selectedDevice;
    private int _selectedPresetIndex = 1; // 0 = Eco, 1 = Stock, 2 = Perf, -1 = Custom

    // 1. Header
    private readonly Panel _headerPanel;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _gpuSelectLabel;
    private readonly FluentComboBox _gpuCombo;
    private readonly FluentBadge _safetyBadge;

    // 2. Telemetria (5 cartões modernos)
    private readonly Panel _telemetryRow;
    private readonly FluentMetricCard _coreClockCard;
    private readonly FluentMetricCard _memClockCard;
    private readonly FluentMetricCard _tempCard;
    private readonly FluentMetricCard _powerCard;
    private readonly FluentMetricCard _loadCard;

    // 3. Perfis Rápidos & Segurança Certificada
    private readonly Panel _presetsContainer;
    private readonly FluentSectionCard _presetsCard;
    private readonly Label _safetyStockLabel;
    private readonly Label _safetyDescLabel;
    private readonly FluentButton _btnEco;
    private readonly FluentButton _btnBalanced;
    private readonly FluentButton _btnMaxPerf;

    // 4. Watchdog Banner
    private readonly Panel _watchdogContainer;
    private readonly WatchdogAlertPanel _watchdogBanner;
    private readonly Label _watchdogLabel;
    private readonly FluentButton _btnKeep;
    private readonly FluentButton _btnRevert;

    // 5. Sliders de Ajuste Fino
    private readonly Panel _controlsContainer;
    private readonly FluentSectionCard _controlsCard;
    private readonly Label _coreLabel;
    private readonly FluentSlider _coreSlider;
    private readonly Label _coreValLabel;

    private readonly Label _memLabel;
    private readonly FluentSlider _memSlider;
    private readonly Label _memValLabel;

    private readonly Label _powerLabel;
    private readonly FluentSlider _powerSlider;
    private readonly Label _powerValLabel;

    private readonly Label _tempLimitLabel;
    private readonly FluentSlider _tempLimitSlider;
    private readonly Label _tempLimitValLabel;

    // 6. Barra de Ações
    private readonly Panel _actionContainer;
    private readonly Panel _actionBar;
    private readonly FluentButton _btnTest;
    private readonly FluentButton _btnResetStock;
    private readonly CheckBox _chkApplyStartup;
    private readonly Label _statusMessageLabel;

    public GpuTuningPanel() : this(null)
    {
    }

    public GpuTuningPanel(GpuTuningEngine engine)
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
            Text = "Ajuste de Placa Gráfica (GPU Tuning)",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 14f),
            Location = new Point(0, 0),
            AutoSize = true
        };

        _subtitleLabel = new Label
        {
            Text = "Controlo de frequências e limites energéticos (TDP) balizado pelas proteções térmicas da VBIOS.",
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 9f),
            Location = new Point(0, 26),
            AutoSize = true
        };

        _gpuSelectLabel = new Label
        {
            Text = "Placa:",
            Location = new Point(0, 52),
            AutoSize = true,
            Font = FluentStyle.UiFontSemibold
        };

        _gpuCombo = new FluentComboBox
        {
            Location = new Point(50, 48),
            Width = 280
        };
        _gpuCombo.SelectedIndexChanged += GpuCombo_SelectedIndexChanged;

        _safetyBadge = new FluentBadge
        {
            Location = new Point(342, 48),
            Height = 24,
            Style = BadgeStyle.Success
        };
        _safetyBadge.SetBadge("Proteção VBIOS Ativa • Duplo Watchdog de Segurança", BadgeStyle.Success);

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_gpuSelectLabel);
        _headerPanel.Controls.Add(_gpuCombo);
        _headerPanel.Controls.Add(_safetyBadge);

        // 2. Telemetria: 5 cartões KPI
        int cardWidth = 170;
        int cardHeight = 104;
        _telemetryRow = new Panel { Dock = DockStyle.Top, Height = cardHeight + gap, Padding = new Padding(0, 0, 0, gap) };

        _coreClockCard = new FluentMetricCard { Location = new Point(0, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Clock) };
        _coreClockCard.SetMetric("CORE CLOCK", "--", "MHz", "Frequência GPU", MetricCardSeverity.Normal);

        _memClockCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 1, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForHardware(HardwareType.Memory) };
        _memClockCard.SetMetric("VRAM CLOCK", "--", "MHz", "Memória de Vídeo", MetricCardSeverity.Normal);

        _tempCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 2, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Temperature) };
        _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Sensor do Núcleo", MetricCardSeverity.Normal);

        _powerCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 3, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Power) };
        _powerCard.SetMetric("POTÊNCIA / TDP", "--", "W", "Consumo Atual", MetricCardSeverity.Normal);

        _loadCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 4, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Load) };
        _loadCard.SetMetric("USO DE GPU", "--", "%", "Carga do Núcleo", MetricCardSeverity.Normal);

        _telemetryRow.Controls.Add(_coreClockCard);
        _telemetryRow.Controls.Add(_memClockCard);
        _telemetryRow.Controls.Add(_tempCard);
        _telemetryRow.Controls.Add(_powerCard);
        _telemetryRow.Controls.Add(_loadCard);

        // 3. Perfis Rápidos & Segurança Integrada
        _presetsContainer = new Panel { Dock = DockStyle.Top, Height = 144 + gap, Padding = new Padding(0, 0, 0, gap) };
        _presetsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Perfis Rápidos e Garantia de Segurança",
            Subtitle = "Selecione uma configuração recomendada ou ajuste parâmetros individuais abaixo."
        };

        _safetyStockLabel = new Label
        {
            Text = "Valores Certificados de Fábrica: Core = +0 MHz | VRAM = +0 MHz | TDP = 100% | Limite Térmico = 83 °C",
            Location = new Point(16, 56),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9f)
        };

        _safetyDescLabel = new Label
        {
            Text = "Teste rápido reverte em 15s. Se o computador reiniciar anormalmente, o watchdog desativa automaticamente o overclock.",
            Location = new Point(16, 76),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _btnEco = new FluentButton { Text = "🍃 Modo Silencioso (Eco)", Location = new Point(16, 98), Width = 190, Height = 34, Variant = FluentButtonVariant.Standard };
        _btnEco.Click += (_, _) => ApplyPreset(0);

        _btnBalanced = new FluentButton { Text = "✓ Padrão de Fábrica (Stock)", Location = new Point(216, 98), Width = 210, Height = 34, Variant = FluentButtonVariant.Success };
        _btnBalanced.Click += (_, _) => ApplyPreset(1);

        _btnMaxPerf = new FluentButton { Text = "⚡ Desempenho Moderado", Location = new Point(436, 98), Width = 190, Height = 34, Variant = FluentButtonVariant.Standard };
        _btnMaxPerf.Click += (_, _) => ApplyPreset(2);

        _presetsCard.Controls.Add(_safetyStockLabel);
        _presetsCard.Controls.Add(_safetyDescLabel);
        _presetsCard.Controls.Add(_btnEco);
        _presetsCard.Controls.Add(_btnBalanced);
        _presetsCard.Controls.Add(_btnMaxPerf);
        _presetsContainer.Controls.Add(_presetsCard);

        // 4. Watchdog Banner
        _watchdogContainer = new Panel { Dock = DockStyle.Top, Height = 52 + gap, Padding = new Padding(0, 0, 0, gap), Visible = false };
        _watchdogBanner = new WatchdogAlertPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 8)
        };

        _watchdogLabel = new Label
        {
            Text = "Perfil de GPU em teste temporário! Revertendo automaticamente em 15s...",
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
        _controlsContainer = new Panel { Dock = DockStyle.Top, Height = 260 + gap, Padding = new Padding(0, 0, 0, gap) };
        _controlsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Parâmetros e Frequências da Placa",
            Subtitle = "Ajuste manual com intervalos estritamente balizados pelo fabricante."
        };

        int rowY = 56;
        int lblX = 16;
        int ctrlX = 240;
        int sliderW = 340;
        int valX = 596;

        _coreLabel = new Label { Text = "Offset de Clock do Núcleo:", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _coreSlider = new FluentSlider { Minimum = -200, Maximum = 200, Value = 0, SmallChange = 5, LargeChange = 25, Location = new Point(ctrlX, rowY), Width = sliderW };
        _coreValLabel = new Label { Text = "+0 MHz (Padrão: +0 MHz)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _coreSlider.Scroll += (_, _) =>
        {
            _coreValLabel.Text = $"{FormatSigned(_coreSlider.Value)} MHz (Padrão: +0 MHz)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 48;
        _memLabel = new Label { Text = "Offset de Clock da VRAM:", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _memSlider = new FluentSlider { Minimum = -500, Maximum = 500, Value = 0, SmallChange = 10, LargeChange = 50, Location = new Point(ctrlX, rowY), Width = sliderW };
        _memValLabel = new Label { Text = "+0 MHz (Padrão: +0 MHz)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _memSlider.Scroll += (_, _) =>
        {
            _memValLabel.Text = $"{FormatSigned(_memSlider.Value)} MHz (Padrão: +0 MHz)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 48;
        _powerLabel = new Label { Text = "Limite de Potência (TDP %):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _powerSlider = new FluentSlider { Minimum = 70, Maximum = 115, Value = 100, SmallChange = 1, LargeChange = 5, Location = new Point(ctrlX, rowY), Width = sliderW };
        _powerValLabel = new Label { Text = "100% (Padrão: 100%)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _powerSlider.Scroll += (_, _) =>
        {
            _powerValLabel.Text = $"{_powerSlider.Value}% (Padrão: 100%)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 48;
        _tempLimitLabel = new Label { Text = "Limite Térmico Alvo (°C):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _tempLimitSlider = new FluentSlider { Minimum = 65, Maximum = 88, Value = 83, SmallChange = 1, LargeChange = 5, Location = new Point(ctrlX, rowY), Width = sliderW };
        _tempLimitValLabel = new Label { Text = "83 °C (Padrão: 83 °C)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _tempLimitSlider.Scroll += (_, _) =>
        {
            _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C (Padrão: 83 °C)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        _controlsCard.Controls.Add(_coreLabel);
        _controlsCard.Controls.Add(_coreSlider);
        _controlsCard.Controls.Add(_coreValLabel);
        _controlsCard.Controls.Add(_memLabel);
        _controlsCard.Controls.Add(_memSlider);
        _controlsCard.Controls.Add(_memValLabel);
        _controlsCard.Controls.Add(_powerLabel);
        _controlsCard.Controls.Add(_powerSlider);
        _controlsCard.Controls.Add(_powerValLabel);
        _controlsCard.Controls.Add(_tempLimitLabel);
        _controlsCard.Controls.Add(_tempLimitSlider);
        _controlsCard.Controls.Add(_tempLimitValLabel);
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
            Text = "↺ Repor Valores da Placa",
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
            if (_selectedDevice != null)
            {
                _selectedDevice.CurrentState.ApplyOnStartup = _chkApplyStartup.Checked;
                _engine?.SaveSettings(_selectedDevice);
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
            _gpuCombo.Items.Add("Nenhuma GPU compatível detetada");
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

    private void UpdateUiFromState()
    {
        if (_selectedDevice == null)
        {
            _coreSlider.Enabled = false;
            _memSlider.Enabled = false;
            _powerSlider.Enabled = false;
            _tempLimitSlider.Enabled = false;
            _btnTest.Enabled = false;
            _btnResetStock.Enabled = false;
            return;
        }

        GpuTuningState s = _selectedDevice.CurrentState;
        GpuTuningCapabilities c = _selectedDevice.Capabilities;

        _coreSlider.Enabled = c.CanAdjustCoreClock;
        _coreSlider.Minimum = c.MinCoreOffsetMhz;
        _coreSlider.Maximum = c.MaxCoreOffsetMhz;
        _coreSlider.Value = Math.Max(c.MinCoreOffsetMhz, Math.Min(c.MaxCoreOffsetMhz, s.CoreClockOffsetMhz));
        _coreValLabel.Text = $"{FormatSigned(_coreSlider.Value)} MHz (Padrão: +0 MHz)";

        _memSlider.Enabled = c.CanAdjustMemoryClock;
        _memSlider.Minimum = c.MinMemoryOffsetMhz;
        _memSlider.Maximum = c.MaxMemoryOffsetMhz;
        _memSlider.Value = Math.Max(c.MinMemoryOffsetMhz, Math.Min(c.MaxMemoryOffsetMhz, s.MemoryClockOffsetMhz));
        _memValLabel.Text = $"{FormatSigned(_memSlider.Value)} MHz (Padrão: +0 MHz)";

        _powerSlider.Enabled = c.CanAdjustPowerLimit;
        _powerSlider.Minimum = c.MinPowerLimitPercent;
        _powerSlider.Maximum = c.MaxPowerLimitPercent;
        _powerSlider.Value = Math.Max(c.MinPowerLimitPercent, Math.Min(c.MaxPowerLimitPercent, s.PowerLimitPercent));
        _powerValLabel.Text = $"{_powerSlider.Value}% (Padrão: 100%)";

        _tempLimitSlider.Enabled = c.CanAdjustTempLimit;
        _tempLimitSlider.Minimum = c.MinTempLimitC;
        _tempLimitSlider.Maximum = c.MaxTempLimitC;
        _tempLimitSlider.Value = Math.Max(c.MinTempLimitC, Math.Min(c.MaxTempLimitC, s.TempLimitC));
        _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C (Padrão: 83 °C)";

        _chkApplyStartup.Checked = s.ApplyOnStartup;
        _btnTest.Enabled = true;
        _btnResetStock.Enabled = true;

        UpdatePresetButtonsHighlight();
    }

    private void ApplyPreset(int index)
    {
        if (_selectedDevice == null) return;
        _selectedPresetIndex = index;

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
                _tempLimitSlider.Value = 83;
                break;

            case 2: // Moderate Perf
                _coreSlider.Value = Math.Min(_coreSlider.Maximum, 60);
                _memSlider.Value = Math.Min(_memSlider.Maximum, 150);
                _powerSlider.Value = Math.Min(_powerSlider.Maximum, 105);
                _tempLimitSlider.Value = Math.Min(_tempLimitSlider.Maximum, 85);
                break;
        }

        _coreValLabel.Text = $"{FormatSigned(_coreSlider.Value)} MHz (Padrão: +0 MHz)";
        _memValLabel.Text = $"{FormatSigned(_memSlider.Value)} MHz (Padrão: +0 MHz)";
        _powerValLabel.Text = $"{_powerSlider.Value}% (Padrão: 100%)";
        _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C (Padrão: 83 °C)";

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
        if (_selectedDevice == null || _engine == null) return;

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
            _watchdogContainer.Visible = true;
            _statusMessageLabel.Text = "🛡️ Teste de GPU iniciado com segurança. O watchdog reverterá em 15s caso não confirme.";
            _statusMessageLabel.ForeColor = Theme.Current.AccentColor;
        }
        else
        {
            _statusMessageLabel.Text = error ?? "Erro ao iniciar teste de GPU.";
            _statusMessageLabel.ForeColor = Theme.Current.StateHotColor;
        }
    }

    private void BtnResetStock_Click(object sender, EventArgs e)
    {
        if (_selectedDevice == null || _engine == null) return;

        if (_engine.ResetDeviceToStock(_selectedDevice, out string error))
        {
            _selectedPresetIndex = 1;
            UpdateUiFromState();
            _watchdogContainer.Visible = false;
            _statusMessageLabel.Text = "✓ Valores de fábrica da placa gráfica repostos com sucesso.";
            _statusMessageLabel.ForeColor = Theme.Current.StateCoolColor;
        }
        else
        {
            _statusMessageLabel.Text = error ?? "Erro ao repor valores.";
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

        _watchdogLabel.Text = $"Perfil de GPU em teste temporário! Revertendo automaticamente em {seconds}s a menos que confirme.";
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
        _statusMessageLabel.Text = "✓ Perfil de GPU confirmado e gravado com segurança.";
        _statusMessageLabel.ForeColor = Theme.Current.StateCoolColor;
    }

    public void UpdateTelemetry()
    {
        if (_selectedDevice == null || !Visible) return;

        GpuTuningTelemetry telem = _selectedDevice.ReadTelemetry();

        // 1. Core Clock
        if (telem.CoreClockMhz.HasValue && telem.CoreClockMhz.Value > 0f)
        {
            float clock = telem.CoreClockMhz.Value;
            _coreClockCard.SetMetric("CORE CLOCK", $"{clock:F0}", "MHz", "Frequência GPU", MetricCardSeverity.Normal, clock / 2500f);
        }
        else
        {
            _coreClockCard.SetMetric("CORE CLOCK", "--", "MHz", "Frequência GPU", MetricCardSeverity.Normal);
        }

        // 2. VRAM Clock
        if (telem.MemoryClockMhz.HasValue && telem.MemoryClockMhz.Value > 0f)
        {
            float mem = telem.MemoryClockMhz.Value;
            _memClockCard.SetMetric("VRAM CLOCK", $"{mem:F0}", "MHz", "Memória de Vídeo", MetricCardSeverity.Normal, mem / 10000f);
        }
        else
        {
            _memClockCard.SetMetric("VRAM CLOCK", "--", "MHz", "Memória de Vídeo", MetricCardSeverity.Normal);
        }

        // 3. Temp
        if (telem.TemperatureC.HasValue && telem.TemperatureC.Value > 0f)
        {
            float temp = telem.TemperatureC.Value;
            MetricCardSeverity sev = temp >= 83f ? MetricCardSeverity.Hot : (temp >= 72f ? MetricCardSeverity.Warm : (temp <= 45f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal));
            _tempCard.SetMetric("TEMPERATURA", $"{temp:F0}", "°C", "Sensor do Núcleo", sev, temp / 100f);
        }
        else
        {
            _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Sensor do Núcleo", MetricCardSeverity.Normal);
        }

        // 4. Power
        if (telem.PowerWatts.HasValue && telem.PowerWatts.Value > 0f)
        {
            float power = telem.PowerWatts.Value;
            _powerCard.SetMetric("POTÊNCIA", $"{power:F1}", "W", "Consumo Atual", power > 250f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, power / 350f);
        }
        else if (telem.PowerPercent.HasValue && telem.PowerPercent.Value > 0f)
        {
            float pct = telem.PowerPercent.Value;
            _powerCard.SetMetric("TDP %", $"{pct:F0}", "%", "Limite TDP", pct > 100f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, pct / 120f);
        }
        else
        {
            _powerCard.SetMetric("POTÊNCIA", "--", "W", "Consumo Atual", MetricCardSeverity.Normal);
        }

        // 5. Load
        if (telem.LoadPercent.HasValue)
        {
            float load = telem.LoadPercent.Value;
            _loadCard.SetMetric("USO DE GPU", $"{load:F0}", "%", "Carga do Núcleo", load > 85f ? MetricCardSeverity.Hot : MetricCardSeverity.Normal, load / 100f);
        }
        else
        {
            _loadCard.SetMetric("USO DE GPU", "--", "%", "Carga do Núcleo", MetricCardSeverity.Normal);
        }
    }

    private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;

        _titleLabel.ForeColor = theme.ForegroundColor;
        _subtitleLabel.ForeColor = theme.MutedForegroundColor;
        _gpuSelectLabel.ForeColor = theme.ForegroundColor;
        _gpuCombo.ApplyTheme(theme);
        _safetyBadge.ApplyTheme(theme);

        _coreClockCard.ApplyTheme(theme);
        _memClockCard.ApplyTheme(theme);
        _tempCard.ApplyTheme(theme);
        _powerCard.ApplyTheme(theme);
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
        _coreLabel.ForeColor = theme.ForegroundColor;
        _coreSlider.ApplyTheme(theme);
        _coreValLabel.ForeColor = theme.ForegroundColor;

        _memLabel.ForeColor = theme.ForegroundColor;
        _memSlider.ApplyTheme(theme);
        _memValLabel.ForeColor = theme.ForegroundColor;

        _powerLabel.ForeColor = theme.ForegroundColor;
        _powerSlider.ApplyTheme(theme);
        _powerValLabel.ForeColor = theme.ForegroundColor;

        _tempLimitLabel.ForeColor = theme.ForegroundColor;
        _tempLimitSlider.ApplyTheme(theme);
        _tempLimitValLabel.ForeColor = theme.ForegroundColor;

        _btnTest.ApplyTheme(theme);
        _btnResetStock.ApplyTheme(theme);
        _chkApplyStartup.ForeColor = theme.ForegroundColor;

        Invalidate();
    }
}
