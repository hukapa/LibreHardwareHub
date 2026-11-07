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

public sealed class GpuTuningPanel : UserControl, IThemedControl
{
    private GpuTuningEngine _engine;
    private IGpuTuningDevice _selectedDevice;
    private int _selectedPresetIndex = 1; // 0 = Eco, 1 = Stock, 2 = Perf, -1 = Custom

    private readonly Panel _headerPanel;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _gpuSelectLabel;
    private readonly FluentComboBox _gpuCombo;
    private readonly FluentBadge _safetyBadge;

    private readonly Panel _telemetryRow;
    private readonly FluentMetricCard _coreClockCard;
    private readonly FluentMetricCard _memClockCard;
    private readonly FluentMetricCard _tempCard;
    private readonly FluentMetricCard _powerCard;

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
    private readonly Label _coreLabel;
    private readonly TrackBar _coreSlider;
    private readonly Label _coreValLabel;

    private readonly Label _memLabel;
    private readonly TrackBar _memSlider;
    private readonly Label _memValLabel;

    private readonly Label _powerLabel;
    private readonly TrackBar _powerSlider;
    private readonly Label _powerValLabel;

    private readonly Label _tempLimitLabel;
    private readonly TrackBar _tempLimitSlider;
    private readonly Label _tempLimitValLabel;

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
        Padding = new Padding(16);
        AutoScroll = true;

        int gap = LogicalToDeviceUnits(14);

        // 1. Header
        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 80, Margin = new Padding(0) };
        _titleLabel = new Label
        {
            Text = "Ajuste de Placa Grafica (GPU Tuning)",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 13.5f),
            Location = new Point(0, 0),
            AutoSize = true
        };

        _subtitleLabel = new Label
        {
            Text = "Controlo de frequencias e limites de potencia (TDP) balizado pelas protecoes da VBIOS.",
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f),
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
            Location = new Point(48, 48),
            Width = 280
        };
        _gpuCombo.SelectedIndexChanged += GpuCombo_SelectedIndexChanged;

        _safetyBadge = new FluentBadge
        {
            Location = new Point(340, 48),
            Height = 22,
            Style = BadgeStyle.Success
        };
        _safetyBadge.SetBadge("Protecao VBIOS Ativa • Duplo Watchdog", BadgeStyle.Success);

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_gpuSelectLabel);
        _headerPanel.Controls.Add(_gpuCombo);
        _headerPanel.Controls.Add(_safetyBadge);

        // 2. Telemetria
        _telemetryRow = new Panel { Dock = DockStyle.Top, Height = 104 + gap, Padding = new Padding(0, 0, 0, gap) };
        _coreClockCard = new FluentMetricCard { Location = new Point(0, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Clock) };
        _coreClockCard.SetMetric("CORE CLOCK", "--", "MHz", "Frequencia GPU", MetricCardSeverity.Normal);

        _memClockCard = new FluentMetricCard { Location = new Point(174, 0), Size = new Size(160, 104), Icon = FluentIcons.ForHardware(HardwareType.Memory) };
        _memClockCard.SetMetric("VRAM CLOCK", "--", "MHz", "Memoria de Video", MetricCardSeverity.Normal);

        _tempCard = new FluentMetricCard { Location = new Point(348, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Temperature) };
        _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Sensor do Nucleo", MetricCardSeverity.Normal);

        _powerCard = new FluentMetricCard { Location = new Point(522, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Power) };
        _powerCard.SetMetric("POTENCIA / TDP", "--", "W", "Consumo Atual", MetricCardSeverity.Normal);

        _telemetryRow.Controls.Add(_coreClockCard);
        _telemetryRow.Controls.Add(_memClockCard);
        _telemetryRow.Controls.Add(_tempCard);
        _telemetryRow.Controls.Add(_powerCard);

        // 3. Cartao de Seguranca de Hardware
        _safetyContainer = new Panel { Dock = DockStyle.Top, Height = 94 + gap, Padding = new Padding(0, 0, 0, gap) };
        _safetyCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Seguranca de Hardware e Protecao Termica",
            Subtitle = "Ajustes balizados pelas tabelas internas de tensao e frequencia da fabricante."
        };
        _safetyDescLabel = new Label
        {
            Text = "Teste rapido reverte em 15s. Se desligar de forma anormal, o watchdog de arranque desativa o overclock.",
            Location = new Point(16, 56),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };
        _safetyStockLabel = new Label
        {
            Text = "Valores de Fabrica: Core = +0 MHz | VRAM = +0 MHz | TDP = 100% | Limite Termico = 83 °C",
            Location = new Point(16, 74),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 8.5f)
        };
        _safetyCard.Controls.Add(_safetyDescLabel);
        _safetyCard.Controls.Add(_safetyStockLabel);
        _safetyContainer.Controls.Add(_safetyCard);

        // 4. Perfis Rapidos — hierarquia visual: Stock=Success, Eco=Standard, Perf=Accent
        _presetsContainer = new Panel { Dock = DockStyle.Top, Height = 100 + gap, Padding = new Padding(0, 0, 0, gap) };
        _presetsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Perfis Rapidos Recomendados",
            Subtitle = "Escolha um ponto de partida seguro. Pode ajustar individualmente abaixo."
        };

        // Stock=Success (recomendado/seguro), Eco=Standard, Perf=Accent
        _btnEco = new FluentButton { Text = "Modo Silencioso (Eco)", Location = new Point(16, 56), Width = 165, Height = 34, Variant = FluentButtonVariant.Standard };
        _btnEco.Click += (_, _) => ApplyPreset(0);

        _btnBalanced = new FluentButton { Text = "Padrao de Fabrica (Stock)", Location = new Point(191, 56), Width = 190, Height = 34, Variant = FluentButtonVariant.Success };
        _btnBalanced.Click += (_, _) => ApplyPreset(1);

        _btnMaxPerf = new FluentButton { Text = "Desempenho Moderado", Location = new Point(391, 56), Width = 180, Height = 34, Variant = FluentButtonVariant.Standard };
        _btnMaxPerf.Click += (_, _) => ApplyPreset(2);

        _presetsCard.Controls.Add(_btnEco);
        _presetsCard.Controls.Add(_btnBalanced);
        _presetsCard.Controls.Add(_btnMaxPerf);
        _presetsContainer.Controls.Add(_presetsCard);

        // 5. Watchdog Banner
        _watchdogContainer = new Panel { Dock = DockStyle.Top, Height = 48 + gap, Padding = new Padding(0, 0, 0, gap), Visible = false };
        _watchdogBanner = new WatchdogAlertPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 8, 14, 8)
        };

        _watchdogLabel = new Label
        {
            Text = "Perfil de GPU em teste! Revertendo automaticamente em 15s...",
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

        // 6. Sliders de Ajuste Fino
        _controlsContainer = new Panel { Dock = DockStyle.Top, Height = 280 + gap, Padding = new Padding(0, 0, 0, gap) };
        _controlsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Parametros e Frequencias da Placa",
            Subtitle = "Ajuste manual com intervalos balizados pela fabricante."
        };

        int rowY = 56;
        int lblX = 16;
        int ctrlX = 230;
        int sliderW = 320;
        int valX = 562;

        _coreLabel = new Label { Text = "Offset de Clock do Nucleo:", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _coreSlider = new TrackBar { Minimum = -200, Maximum = 200, Value = 0, TickFrequency = 25, Location = new Point(ctrlX, rowY), Width = sliderW };
        _coreValLabel = new Label { Text = "+0 MHz (Padrao: +0 MHz)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _coreSlider.Scroll += (_, _) =>
        {
            _coreValLabel.Text = $"{FormatSigned(_coreSlider.Value)} MHz (Padrao: +0 MHz)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 50;
        _memLabel = new Label { Text = "Offset de Clock da VRAM:", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _memSlider = new TrackBar { Minimum = -500, Maximum = 500, Value = 0, TickFrequency = 50, Location = new Point(ctrlX, rowY), Width = sliderW };
        _memValLabel = new Label { Text = "+0 MHz (Padrao: +0 MHz)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _memSlider.Scroll += (_, _) =>
        {
            _memValLabel.Text = $"{FormatSigned(_memSlider.Value)} MHz (Padrao: +0 MHz)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 50;
        _powerLabel = new Label { Text = "Limite de Potencia (TDP %):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _powerSlider = new TrackBar { Minimum = 70, Maximum = 115, Value = 100, TickFrequency = 5, Location = new Point(ctrlX, rowY), Width = sliderW };
        _powerValLabel = new Label { Text = "100% (Padrao: 100%)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _powerSlider.Scroll += (_, _) =>
        {
            _powerValLabel.Text = $"{_powerSlider.Value}% (Padrao: 100%)";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        rowY += 50;
        _tempLimitLabel = new Label { Text = "Limite Termico Alvo (°C):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _tempLimitSlider = new TrackBar { Minimum = 65, Maximum = 88, Value = 83, TickFrequency = 5, Location = new Point(ctrlX, rowY), Width = sliderW };
        _tempLimitValLabel = new Label { Text = "83 °C (Padrao: 83 °C)", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _tempLimitSlider.Scroll += (_, _) =>
        {
            _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C (Padrao: 83 °C)";
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

        // 7. Barra de Acoes
        _actionContainer = new Panel { Dock = DockStyle.Top, Height = 64 + gap, Padding = new Padding(0, 0, 0, gap) };
        _actionBar = new Panel { Dock = DockStyle.Fill };
        _btnTest = new FluentButton
        {
            Text = "Testar com Seguranca (15s)",
            Location = new Point(0, 4),
            Width = 196,
            Height = 34,
            Variant = FluentButtonVariant.Success  // Verde = acao segura com watchdog
        };
        _btnTest.Click += BtnTest_Click;

        _btnResetStock = new FluentButton
        {
            Text = "Repor Valores da Placa",
            Location = new Point(208, 4),
            Width = 190,
            Height = 34,
            Variant = FluentButtonVariant.Standard
        };
        _btnResetStock.Click += BtnResetStock_Click;

        _chkApplyStartup = new CheckBox
        {
            Text = "Aplicar perfil no arranque do Windows (protegido por watchdog de 120s)",
            Location = new Point(416, 11),
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
            Location = new Point(0, 44),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _actionBar.Controls.Add(_btnTest);
        _actionBar.Controls.Add(_btnResetStock);
        _actionBar.Controls.Add(_chkApplyStartup);
        _actionBar.Controls.Add(_statusMessageLabel);
        _actionContainer.Controls.Add(_actionBar);

        // Ordem inversa de adicao para dock vertical
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
            _gpuCombo.Items.Add("Nenhuma GPU compativel detectada");
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
        _coreValLabel.Text = $"{FormatSigned(_coreSlider.Value)} MHz (Padrao: +0 MHz)";

        _memSlider.Enabled = c.CanAdjustMemoryClock;
        _memSlider.Minimum = c.MinMemoryOffsetMhz;
        _memSlider.Maximum = c.MaxMemoryOffsetMhz;
        _memSlider.Value = Math.Max(c.MinMemoryOffsetMhz, Math.Min(c.MaxMemoryOffsetMhz, s.MemoryClockOffsetMhz));
        _memValLabel.Text = $"{FormatSigned(_memSlider.Value)} MHz (Padrao: +0 MHz)";

        _powerSlider.Enabled = c.CanAdjustPowerLimit;
        _powerSlider.Minimum = c.MinPowerLimitPercent;
        _powerSlider.Maximum = c.MaxPowerLimitPercent;
        _powerSlider.Value = Math.Max(c.MinPowerLimitPercent, Math.Min(c.MaxPowerLimitPercent, s.PowerLimitPercent));
        _powerValLabel.Text = $"{_powerSlider.Value}% (Padrao: 100%)";

        _tempLimitSlider.Enabled = c.CanAdjustTempLimit;
        _tempLimitSlider.Minimum = c.MinTempLimitC;
        _tempLimitSlider.Maximum = c.MaxTempLimitC;
        _tempLimitSlider.Value = Math.Max(c.MinTempLimitC, Math.Min(c.MaxTempLimitC, s.TempLimitC));
        _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C (Padrao: 83 °C)";

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

        _coreValLabel.Text = $"{FormatSigned(_coreSlider.Value)} MHz (Padrao: +0 MHz)";
        _memValLabel.Text = $"{FormatSigned(_memSlider.Value)} MHz (Padrao: +0 MHz)";
        _powerValLabel.Text = $"{_powerSlider.Value}% (Padrao: 100%)";
        _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C (Padrao: 83 °C)";

        UpdatePresetButtonsHighlight();
    }

    private void UpdatePresetButtonsHighlight()
    {
        _btnEco.IsActive = _selectedPresetIndex == 0;
        _btnEco.Variant = FluentButtonVariant.Standard;

        // Stock: sempre Success para comunicar "seguro/padrao"
        _btnBalanced.IsActive = _selectedPresetIndex == 1;
        _btnBalanced.Variant = _selectedPresetIndex == 1 ? FluentButtonVariant.Success : FluentButtonVariant.Standard;

        // Desempenho: selecionado=Accent, nao selecionado=Standard
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
            _statusMessageLabel.Text = "Teste de seguranca iniciado. Confirme para manter.";
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
            _statusMessageLabel.Text = "Valores de fabrica da placa gráfica repostos com sucesso.";
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

        _watchdogLabel.Text = $"Perfil de GPU em teste! Revertendo automaticamente em {seconds}s a menos que confirme.";
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
        _statusMessageLabel.Text = "Perfil de GPU confirmado e gravado com sucesso.";
        _statusMessageLabel.ForeColor = Theme.Current.StateCoolColor;
    }

    public void UpdateTelemetry()
    {
        if (_selectedDevice == null || !Visible) return;

        GpuTuningTelemetry telem = _selectedDevice.ReadTelemetry();

        // 1. Core Clock
        if (telem.CoreClockMhz.HasValue)
        {
            float clock = telem.CoreClockMhz.Value;
            _coreClockCard.SetMetric("CORE CLOCK", $"{clock:F0}", "MHz", "Frequencia GPU", MetricCardSeverity.Normal, clock / 2500f);
        }
        else
        {
            _coreClockCard.SetMetric("CORE CLOCK", "--", "MHz", "Frequencia GPU", MetricCardSeverity.Normal);
        }

        // 2. VRAM Clock
        if (telem.MemoryClockMhz.HasValue)
        {
            float mem = telem.MemoryClockMhz.Value;
            _memClockCard.SetMetric("VRAM CLOCK", $"{mem:F0}", "MHz", "Memoria de Video", MetricCardSeverity.Normal, mem / 10000f);
        }
        else
        {
            _memClockCard.SetMetric("VRAM CLOCK", "--", "MHz", "Memoria de Video", MetricCardSeverity.Normal);
        }

        // 3. Temp
        if (telem.TemperatureC.HasValue)
        {
            float temp = telem.TemperatureC.Value;
            MetricCardSeverity sev = temp >= 83f ? MetricCardSeverity.Hot : (temp >= 72f ? MetricCardSeverity.Warm : (temp <= 45f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal));
            _tempCard.SetMetric("TEMPERATURA", $"{temp:F0}", "°C", "Sensor do Nucleo", sev, temp / 100f);
        }
        else
        {
            _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Sensor do Nucleo", MetricCardSeverity.Normal);
        }

        // 4. Power
        if (telem.PowerWatts.HasValue)
        {
            float power = telem.PowerWatts.Value;
            _powerCard.SetMetric("POTENCIA", $"{power:F1}", "W", "Consumo Atual", power > 250f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, power / 350f);
        }
        else if (telem.PowerPercent.HasValue)
        {
            float pct = telem.PowerPercent.Value;
            _powerCard.SetMetric("TDP %", $"{pct:F0}", "%", "Limite TDP", pct > 100f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal, pct / 120f);
        }
        else
        {
            _powerCard.SetMetric("POTENCIA", "--", "W", "Consumo Atual", MetricCardSeverity.Normal);
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
        _coreLabel.ForeColor = theme.ForegroundColor;
        _coreValLabel.ForeColor = theme.ForegroundColor;
        _memLabel.ForeColor = theme.ForegroundColor;
        _memValLabel.ForeColor = theme.ForegroundColor;
        _powerLabel.ForeColor = theme.ForegroundColor;
        _powerValLabel.ForeColor = theme.ForegroundColor;
        _tempLimitLabel.ForeColor = theme.ForegroundColor;
        _tempLimitValLabel.ForeColor = theme.ForegroundColor;

        _btnTest.ApplyTheme(theme);
        _btnResetStock.ApplyTheme(theme);
        _chkApplyStartup.ForeColor = theme.ForegroundColor;

        Invalidate();
    }
}
