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

    private readonly Panel _infoContainer;
    private readonly FluentSectionCard _infoCard;
    private readonly Label _infoTitleLabel;
    private readonly Label _infoDescLabel;

    private readonly Panel _presetsContainer;
    private readonly FluentSectionCard _presetsCard;
    private readonly Button _btnEco;
    private readonly Button _btnBalanced;
    private readonly Button _btnMaxPerf;

    private readonly Panel _watchdogContainer;
    private readonly WatchdogAlertPanel _watchdogBanner;
    private readonly Label _watchdogLabel;
    private readonly Button _btnKeep;
    private readonly Button _btnRevert;

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
    private readonly Button _btnTest;
    private readonly Button _btnResetStock;
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
            Text = "Ajuste de Placa Grafica (GPU Tuning & Clamping)",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 13.5f),
            Location = new Point(0, 0),
            AutoSize = true
        };

        _subtitleLabel = new Label
        {
            Text = "Controlo seguro de frequencias e limites de potencia (TDP) balizado por hardware.",
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
        _safetyBadge.SetBadge("Modo Seguro • Clamping Ativo", BadgeStyle.Success);

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_gpuSelectLabel);
        _headerPanel.Controls.Add(_gpuCombo);
        _headerPanel.Controls.Add(_safetyBadge);

        // 2. Telemetria (4 cartoes Fluent Metric identicos ao SummaryPanel)
        _telemetryRow = new Panel { Dock = DockStyle.Top, Height = 104 + gap, Padding = new Padding(0, 0, 0, gap) };
        _coreClockCard = new FluentMetricCard { Location = new Point(0, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Clock) };
        _coreClockCard.SetMetric("CORE CLOCK", "--", "MHz", "Frequencia GPU", MetricCardSeverity.Normal);

        _memClockCard = new FluentMetricCard { Location = new Point(174, 0), Size = new Size(160, 104), Icon = FluentIcons.ForHardware(HardwareType.Memory) };
        _memClockCard.SetMetric("VRAM CLOCK", "--", "MHz", "Memoria de Video", MetricCardSeverity.Normal);

        _tempCard = new FluentMetricCard { Location = new Point(348, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Temperature) };
        _tempCard.SetMetric("TEMPERATURA", "--", "°C", "Sensor do Nucleo", MetricCardSeverity.Normal);

        _powerCard = new FluentMetricCard { Location = new Point(522, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Load) };
        _powerCard.SetMetric("CARGA TOTAL", "--", "%", "Uso 3D", MetricCardSeverity.Normal);

        _telemetryRow.Controls.Add(_coreClockCard);
        _telemetryRow.Controls.Add(_memClockCard);
        _telemetryRow.Controls.Add(_tempCard);
        _telemetryRow.Controls.Add(_powerCard);

        // 3. Card Informativo para GPUs Integradas (quando sem suporte a escrita de overclock)
        _infoContainer = new Panel { Dock = DockStyle.Top, Height = 120 + gap, Padding = new Padding(0, 0, 0, gap), Visible = false };
        _infoCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Dispositivo em Modo de Monitorizacao",
            Subtitle = "Adaptador grafico integrado sem suporte a gravacao de frequencia/potencia por hardware."
        };

        _infoTitleLabel = new Label
        {
            Text = "Esta GPU opera exclusivamente com frequencias e tensoes geridas pela BIOS do sistema.",
            Location = new Point(16, 56),
            Size = new Size(620, 20),
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 9f)
        };

        _infoDescLabel = new Label
        {
            Text = "A telemetria completa de clocks, carga e temperaturas continua em execucao em tempo real acima.",
            Location = new Point(16, 80),
            Size = new Size(620, 20),
            Font = FluentStyle.CreateUiFont(FontStyle.Italic, 8.5f)
        };

        _infoCard.Controls.Add(_infoTitleLabel);
        _infoCard.Controls.Add(_infoDescLabel);
        _infoContainer.Controls.Add(_infoCard);

        // 4. Perfis Rapidos (FluentSectionCard envolto em espacador de respiro)
        _presetsContainer = new Panel { Dock = DockStyle.Top, Height = 96 + gap, Padding = new Padding(0, 0, 0, gap) };
        _presetsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Perfis Rapidos",
            Subtitle = "Selecione uma configuracao balanceada para a placa grafica ativa."
        };

        _btnEco = new Button { Text = "Eco / Silencioso", Location = new Point(16, 54), Width = 140, Height = 30 };
        _btnEco.Click += (_, _) => ApplyPreset(0);

        _btnBalanced = new Button { Text = "Equilibrado (Stock)", Location = new Point(166, 54), Width = 150, Height = 30 };
        _btnBalanced.Click += (_, _) => ApplyPreset(1);

        _btnMaxPerf = new Button { Text = "Desempenho Seguro", Location = new Point(326, 54), Width = 160, Height = 30 };
        _btnMaxPerf.Click += (_, _) => ApplyPreset(2);

        _presetsCard.Controls.Add(_btnEco);
        _presetsCard.Controls.Add(_btnBalanced);
        _presetsCard.Controls.Add(_btnMaxPerf);
        _presetsContainer.Controls.Add(_presetsCard);

        // 5. Banner de Watchdog (teste temporizado de 15s)
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

        _btnKeep = new Button
        {
            Text = "Confirmar e Manter",
            Location = new Point(470, 9),
            Width = 145,
            Height = 28
        };
        _btnKeep.Click += (_, _) => _engine?.ConfirmProfile();

        _btnRevert = new Button
        {
            Text = "Reverter",
            Location = new Point(625, 9),
            Width = 90,
            Height = 28
        };
        _btnRevert.Click += (_, _) => _engine?.CancelTestAndRevert("Revertido manualmente pelo utilizador.");

        _watchdogBanner.Controls.Add(_watchdogLabel);
        _watchdogBanner.Controls.Add(_btnKeep);
        _watchdogBanner.Controls.Add(_btnRevert);
        _watchdogContainer.Controls.Add(_watchdogBanner);

        // 6. Cartao de Controlos e Sliders (FluentSectionCard)
        _controlsContainer = new Panel { Dock = DockStyle.Top, Height = 250 + gap, Padding = new Padding(0, 0, 0, gap) };
        _controlsCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Parametros de Operacao da GPU",
            Subtitle = "Ajuste de offsets de clock e limites de potencia balizados por seguranca."
        };

        int rowY = 58;
        int lblX = 16;
        int ctrlX = 220;
        int sliderW = 320;
        int valX = 552;

        // Core Clock Slider
        _coreLabel = new Label { Text = "Frequencia do Core (Offset):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _coreSlider = new TrackBar { Minimum = -300, Maximum = 150, Value = 0, TickFrequency = 50, Location = new Point(ctrlX, rowY), Width = sliderW };
        _coreValLabel = new Label { Text = "+0 MHz", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _coreSlider.Scroll += (_, _) =>
        {
            _coreValLabel.Text = $"{(_coreSlider.Value >= 0 ? "+" : "")}{_coreSlider.Value} MHz";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        // Memory Clock Slider
        rowY += 46;
        _memLabel = new Label { Text = "Frequencia da VRAM (Offset):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _memSlider = new TrackBar { Minimum = -1000, Maximum = 1000, Value = 0, TickFrequency = 200, Location = new Point(ctrlX, rowY), Width = sliderW };
        _memValLabel = new Label { Text = "+0 MHz", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _memSlider.Scroll += (_, _) =>
        {
            _memValLabel.Text = $"{(_memSlider.Value >= 0 ? "+" : "")}{_memSlider.Value} MHz";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        // Power Limit Slider
        rowY += 46;
        _powerLabel = new Label { Text = "Limite de Potencia (TDP):", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _powerSlider = new TrackBar { Minimum = 50, Maximum = 115, Value = 100, TickFrequency = 10, Location = new Point(ctrlX, rowY), Width = sliderW };
        _powerValLabel = new Label { Text = "100%", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _powerSlider.Scroll += (_, _) =>
        {
            _powerValLabel.Text = $"{_powerSlider.Value}%";
            _selectedPresetIndex = -1;
            UpdatePresetButtonsHighlight();
        };

        // Temp Limit Slider
        rowY += 46;
        _tempLimitLabel = new Label { Text = "Alvo Termico Maximo:", Location = new Point(lblX, rowY + 4), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _tempLimitSlider = new TrackBar { Minimum = 65, Maximum = 88, Value = 83, TickFrequency = 5, Location = new Point(ctrlX, rowY), Width = sliderW };
        _tempLimitValLabel = new Label { Text = "83 °C", Location = new Point(valX, rowY + 4), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 9.5f) };
        _tempLimitSlider.Scroll += (_, _) =>
        {
            _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C";
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

        // 7. Barra de Acoes (Inferior)
        _actionContainer = new Panel { Dock = DockStyle.Top, Height = 64 + gap, Padding = new Padding(0, 0, 0, gap) };
        _actionBar = new Panel { Dock = DockStyle.Fill };
        _btnTest = new Button
        {
            Text = "Testar Perfil (15s)",
            Location = new Point(0, 4),
            Width = 160,
            Height = 34
        };
        _btnTest.Click += BtnTest_Click;

        _btnResetStock = new Button
        {
            Text = "Restaurar Padroes (Stock)",
            Location = new Point(172, 4),
            Width = 190,
            Height = 34
        };
        _btnResetStock.Click += BtnResetStock_Click;

        _chkApplyStartup = new CheckBox
        {
            Text = "Aplicar perfil no arranque do Windows (protegido por watchdog de 120s)",
            Location = new Point(378, 11),
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

        // Ordem inversa de Dock para posicionamento natural vertical
        Controls.Add(_actionContainer);
        Controls.Add(_controlsContainer);
        Controls.Add(_watchdogContainer);
        Controls.Add(_presetsContainer);
        Controls.Add(_infoContainer);
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
            _engine.WatchdogTick -= Engine_WatchdogTick;
            _engine.WatchdogReverted -= Engine_WatchdogReverted;
            _engine.TestConfirmed -= Engine_TestConfirmed;
        }

        _engine = engine;

        if (_engine != null)
        {
            _engine.WatchdogTick += Engine_WatchdogTick;
            _engine.WatchdogReverted += Engine_WatchdogReverted;
            _engine.TestConfirmed -= Engine_TestConfirmed;

            PopulateDeviceList();

            if (_engine.HadDirtyBoot)
            {
                _statusMessageLabel.Text = _engine.DirtyBootMessage;
                _statusMessageLabel.ForeColor = Theme.Current.StateHotColor;
            }
        }
    }

    private void PopulateDeviceList()
    {
        _gpuCombo.Items.Clear();
        if (_engine == null) return;

        foreach (IGpuTuningDevice dev in _engine.Devices)
            _gpuCombo.Items.Add(dev.Name);

        if (_gpuCombo.Items.Count > 0)
        {
            _gpuCombo.SelectedIndex = 0;
            _selectedDevice = _engine.Devices[0];
            UpdateUiFromDevice();
        }
        else
        {
            _gpuCombo.Items.Add("Nenhuma GPU compativel detectada");
            _gpuCombo.SelectedIndex = 0;
            _selectedDevice = null;
            UpdateUiFromDevice();
        }
    }

    private void GpuCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_engine == null) return;
        int idx = _gpuCombo.SelectedIndex;
        if (idx >= 0 && idx < _engine.Devices.Count)
        {
            _selectedDevice = _engine.Devices[idx];
            UpdateUiFromDevice();
        }
    }

    private void UpdateUiFromDevice()
    {
        if (_selectedDevice == null)
        {
            _infoContainer.Visible = true;
            _presetsContainer.Visible = false;
            _controlsContainer.Visible = false;
            _actionContainer.Visible = false;
            _safetyBadge.SetBadge("Sem Placa Detectada", BadgeStyle.Neutral);
            return;
        }

        bool supported = _selectedDevice.Capabilities.IsSupported;

        if (supported)
        {
            _infoContainer.Visible = false;
            _presetsContainer.Visible = true;
            _controlsContainer.Visible = true;
            _actionContainer.Visible = true;

            _safetyBadge.SetBadge("Modo Seguro • Clamping Ativo", BadgeStyle.Success);
            GpuTuningCapabilities lim = _selectedDevice.Capabilities;
            _coreSlider.Minimum = lim.MinCoreOffsetMhz;
            _coreSlider.Maximum = lim.MaxCoreOffsetMhz;
            _memSlider.Minimum = lim.MinMemoryOffsetMhz;
            _memSlider.Maximum = lim.MaxMemoryOffsetMhz;
            _powerSlider.Minimum = lim.MinPowerLimitPercent;
            _powerSlider.Maximum = lim.MaxPowerLimitPercent;
            _tempLimitSlider.Minimum = lim.MinTempLimitC;
            _tempLimitSlider.Maximum = lim.MaxTempLimitC;

            GpuTuningState state = _selectedDevice.CurrentState;
            _coreSlider.Value = Clamp(state.CoreClockOffsetMhz, _coreSlider.Minimum, _coreSlider.Maximum);
            _coreValLabel.Text = $"{(_coreSlider.Value >= 0 ? "+" : "")}{_coreSlider.Value} MHz";

            _memSlider.Value = Clamp(state.MemoryClockOffsetMhz, _memSlider.Minimum, _memSlider.Maximum);
            _memValLabel.Text = $"{(_memSlider.Value >= 0 ? "+" : "")}{_memSlider.Value} MHz";

            _powerSlider.Value = Clamp(state.PowerLimitPercent, _powerSlider.Minimum, _powerSlider.Maximum);
            _powerValLabel.Text = $"{_powerSlider.Value}%";

            _tempLimitSlider.Value = Clamp(state.TempLimitC, _tempLimitSlider.Minimum, _tempLimitSlider.Maximum);
            _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C";

            _chkApplyStartup.Checked = state.ApplyOnStartup;
            UpdatePresetButtonsHighlight();
        }
        else
        {
            // GPU integrada: esconde controlos travados e apresenta cartao informativo limpo de alta legibilidade
            _infoContainer.Visible = true;
            _presetsContainer.Visible = false;
            _controlsContainer.Visible = false;
            _actionContainer.Visible = false;
            _safetyBadge.SetBadge("Apenas Telemetria", BadgeStyle.Neutral);
        }
    }

    private void ApplyPreset(int presetIndex)
    {
        if (_selectedDevice == null || !_selectedDevice.Capabilities.IsSupported)
            return;

        _selectedPresetIndex = presetIndex;
        GpuTuningState preset = presetIndex switch
        {
            0 => new GpuTuningState { CoreClockOffsetMhz = 0, MemoryClockOffsetMhz = 0, PowerLimitPercent = 80, TempLimitC = 75 },
            2 => new GpuTuningState { CoreClockOffsetMhz = 50, MemoryClockOffsetMhz = 150, PowerLimitPercent = 110, TempLimitC = 86 },
            _ => new GpuTuningState { CoreClockOffsetMhz = 0, MemoryClockOffsetMhz = 0, PowerLimitPercent = 100, TempLimitC = 83 }
        };

        _coreSlider.Value = Clamp(preset.CoreClockOffsetMhz, _coreSlider.Minimum, _coreSlider.Maximum);
        _coreValLabel.Text = $"{(_coreSlider.Value >= 0 ? "+" : "")}{_coreSlider.Value} MHz";

        _memSlider.Value = Clamp(preset.MemoryClockOffsetMhz, _memSlider.Minimum, _memSlider.Maximum);
        _memValLabel.Text = $"{(_memSlider.Value >= 0 ? "+" : "")}{_memSlider.Value} MHz";

        _powerSlider.Value = Clamp(preset.PowerLimitPercent, _powerSlider.Minimum, _powerSlider.Maximum);
        _powerValLabel.Text = $"{_powerSlider.Value}%";

        _tempLimitSlider.Value = Clamp(preset.TempLimitC, _tempLimitSlider.Minimum, _tempLimitSlider.Maximum);
        _tempLimitValLabel.Text = $"{_tempLimitSlider.Value} °C";

        _statusMessageLabel.Text = "Preset selecionado. Carregue em 'Testar Perfil (15s)' para validar estabilidade.";
        _statusMessageLabel.ForeColor = Theme.Current.StateWarmColor;
        UpdatePresetButtonsHighlight();
    }

    private void UpdatePresetButtonsHighlight()
    {
        Theme theme = Theme.Current;
        FluentStyle.StyleChipButton(_btnEco, theme, _selectedPresetIndex == 0);
        FluentStyle.StyleChipButton(_btnBalanced, theme, _selectedPresetIndex == 1);
        FluentStyle.StyleChipButton(_btnMaxPerf, theme, _selectedPresetIndex == 2);
    }

    private void BtnTest_Click(object sender, EventArgs e)
    {
        if (_engine == null || _selectedDevice == null) return;

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
            _statusMessageLabel.Text = "Teste de GPU em execucao. Valide a estabilidade do ecra.";
            _statusMessageLabel.ForeColor = Theme.Current.StateWarmColor;
        }
        else
        {
            _statusMessageLabel.Text = error ?? "Falha ao aplicar configuracao.";
            _statusMessageLabel.ForeColor = Theme.Current.StateHotColor;
        }
    }

    private void BtnResetStock_Click(object sender, EventArgs e)
    {
        if (_engine == null || _selectedDevice == null) return;

        if (_selectedDevice.ResetToStock(out string error))
        {
            _engine.SaveSettings(_selectedDevice);
            _watchdogContainer.Visible = false;
            _selectedPresetIndex = 1;
            UpdateUiFromDevice();
            _statusMessageLabel.Text = "Valores padrao (stock) da placa grafica restaurados.";
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
        UpdateUiFromDevice();
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
            _memClockCard.SetMetric("VRAM CLOCK", $"{mem:F0}", "MHz", "Memoria de Video", MetricCardSeverity.Normal, mem / 3000f);
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

        // 4. Carga
        if (telem.LoadPercent.HasValue)
        {
            float load = telem.LoadPercent.Value;
            MetricCardSeverity sev = load >= 85f ? MetricCardSeverity.Warm : (load <= 5f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal);
            _powerCard.SetMetric("CARGA TOTAL", $"{load:F0}", "%", "Uso 3D", sev, load / 100f);
        }
        else
        {
            _powerCard.SetMetric("CARGA TOTAL", "--", "%", "Uso 3D", MetricCardSeverity.Normal);
        }
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;

        _titleLabel.ForeColor = theme.ForegroundColor;
        _subtitleLabel.ForeColor = theme.MutedForegroundColor;
        _gpuSelectLabel.ForeColor = theme.ForegroundColor;

        _infoTitleLabel.ForeColor = theme.ForegroundColor;
        _infoDescLabel.ForeColor = theme.MutedForegroundColor;

        _coreLabel.ForeColor = theme.ForegroundColor;
        _memLabel.ForeColor = theme.ForegroundColor;
        _powerLabel.ForeColor = theme.ForegroundColor;
        _tempLimitLabel.ForeColor = theme.ForegroundColor;
        _chkApplyStartup.ForeColor = theme.ForegroundColor;

        _coreValLabel.ForeColor = theme.AccentColor;
        _memValLabel.ForeColor = theme.AccentColor;
        _powerValLabel.ForeColor = theme.AccentColor;
        _tempLimitValLabel.ForeColor = theme.AccentColor;

        _coreSlider.BackColor = theme.SurfaceColor;
        _memSlider.BackColor = theme.SurfaceColor;
        _powerSlider.BackColor = theme.SurfaceColor;
        _tempLimitSlider.BackColor = theme.SurfaceColor;

        _gpuCombo.ApplyTheme(theme);

        FluentStyle.StylePrimaryButton(_btnTest, theme);
        FluentStyle.StyleSecondaryButton(_btnResetStock, theme);
        FluentStyle.StylePrimaryButton(_btnKeep, theme);
        FluentStyle.StyleSecondaryButton(_btnRevert, theme);

        _safetyBadge.ApplyTheme(theme);
        _coreClockCard.ApplyTheme(theme);
        _memClockCard.ApplyTheme(theme);
        _tempCard.ApplyTheme(theme);
        _powerCard.ApplyTheme(theme);
        _infoCard.ApplyTheme(theme);
        _presetsCard.ApplyTheme(theme);
        _controlsCard.ApplyTheme(theme);

        UpdatePresetButtonsHighlight();
        Invalidate();
    }

    private static int Clamp(int value, int min, int max)
    {
        return value < min ? min : (value > max ? max : value);
    }

    public sealed class WatchdogAlertPanel : Panel
    {
        public WatchdogAlertPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Theme theme = Theme.Current;
            e.Graphics.Clear(Parent?.BackColor ?? theme.BackgroundColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            float radius = Math.Max(4f, LogicalToDeviceUnits(theme.CornerRadius + 2));
            RectangleF bounds = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

            Color bg = FluentStyle.Blend(theme.SurfaceColor, theme.StateWarmColor, theme.IsDark ? 0.25 : 0.14);
            Color border = FluentStyle.Blend(theme.SurfaceStrokeColor, theme.StateWarmColor, 0.6);

            FluentStyle.FillRounded(e.Graphics, bg, bounds, radius);
            FluentStyle.DrawRounded(e.Graphics, border, bounds, radius);
        }
    }
}
