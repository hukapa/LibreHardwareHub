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

public sealed class FanControlPanel : UserControl, IThemedControl
{
    private FanControlEngine _engine;
    private readonly List<FanChannelCard> _cards = new();

    private readonly Panel _headerPanel;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _presetLabel;
    private readonly FluentComboBox _presetCombo;
    private readonly FluentBadge _profileBadge;
    private readonly FluentBadge _safetyBadge;

    private readonly Panel _telemetryRow;
    private readonly FluentMetricCard _cpuTempCard;
    private readonly FluentMetricCard _gpuTempCard;
    private readonly FluentMetricCard _activeFansCard;
    private readonly FluentMetricCard _profileCard;

    private readonly Panel _cardContainer;
    private readonly Panel _emptyContainer;
    private readonly FluentSectionCard _emptyCard;
    private readonly Label _emptyTitleLabel;
    private readonly Label _emptyDescLabel;

    public FanControlPanel() : this(null)
    {
    }

    public FanControlPanel(FanControlEngine engine)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        int gap = LogicalToDeviceUnits(14);

        // 1. Header
        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 80, Margin = new Padding(0) };
        _titleLabel = new Label
        {
            Text = "Controlo de Ventilacao (Fan Control & Curvas)",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 13.5f),
            Location = new Point(0, 0),
            AutoSize = true
        };

        _subtitleLabel = new Label
        {
            Text = "Curvas dinamicas de velocidade e gestao de ventiladores Super I/O e controladores.",
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f),
            Location = new Point(0, 26),
            AutoSize = true
        };

        _presetLabel = new Label
        {
            Text = "Perfil Global:",
            Location = new Point(0, 54),
            AutoSize = true,
            Font = FluentStyle.UiFontSemibold
        };

        _presetCombo = new FluentComboBox
        {
            Location = new Point(84, 50),
            Width = 180
        };
        _presetCombo.Items.AddRange(new object[] { "BIOS Padrao", "Silencioso", "Equilibrado", "Desempenho", "Personalizado" });
        _presetCombo.SelectedIndex = 0;
        _presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;

        // Badge de perfil atual — confirmacao visual do modo activo
        _profileBadge = new FluentBadge
        {
            Location = new Point(276, 50),
            Height = 22,
            Style = BadgeStyle.Neutral
        };
        _profileBadge.SetBadge("BIOS Padrao", BadgeStyle.Neutral);

        // Badge de seguranca termica
        _safetyBadge = new FluentBadge
        {
            Location = new Point(276 + 110, 50),
            Height = 22,
            Style = BadgeStyle.Success
        };
        _safetyBadge.SetBadge("Protecao Termica (>90 C → 100%)", BadgeStyle.Success);

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_presetLabel);
        _headerPanel.Controls.Add(_presetCombo);
        _headerPanel.Controls.Add(_profileBadge);
        _headerPanel.Controls.Add(_safetyBadge);

        // 2. Telemetria (4 cartoes Fluent Metric identicos ao SummaryPanel)
        _telemetryRow = new Panel { Dock = DockStyle.Top, Height = 104 + gap, Padding = new Padding(0, 0, 0, gap) };
        _cpuTempCard = new FluentMetricCard { Location = new Point(0, 0), Size = new Size(160, 104), Icon = FluentIcons.ForHardware(HardwareType.Cpu) };
        _cpuTempCard.SetMetric("TEMP CPU", "--", "°C", "Sensor Package", MetricCardSeverity.Normal);

        _gpuTempCard = new FluentMetricCard { Location = new Point(174, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Temperature) };
        _gpuTempCard.SetMetric("TEMP GPU", "--", "°C", "Sensor do Nucleo", MetricCardSeverity.Normal);

        _activeFansCard = new FluentMetricCard { Location = new Point(348, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Fan) };
        _activeFansCard.SetMetric("CANAIS ATIVOS", "0", "canais", "Super I/O", MetricCardSeverity.Normal);

        _profileCard = new FluentMetricCard { Location = new Point(522, 0), Size = new Size(160, 104), Icon = FluentIcons.ForSensorType(SensorType.Control) };
        _profileCard.SetMetric("PERFIL GLOBAL", "BIOS", "", "Modo Atual", MetricCardSeverity.Normal);

        _telemetryRow.Controls.Add(_cpuTempCard);
        _telemetryRow.Controls.Add(_gpuTempCard);
        _telemetryRow.Controls.Add(_activeFansCard);
        _telemetryRow.Controls.Add(_profileCard);

        // 3. Container de Canais
        _cardContainer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 4, 8, 4) };

        // Empty State Card
        _emptyContainer = new Panel { Dock = DockStyle.Top, Height = 110 + gap, Padding = new Padding(0, 0, 0, gap) };
        _emptyCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Nenhum Canal de Ventilacao Detectado",
            Subtitle = "O sistema nao encontrou controladores Super I/O ou dispositivos compativeis com escrita PWM."
        };

        _emptyTitleLabel = new Label
        {
            Text = "Controlos PWM requerem suporte no chip Super I/O (Nuvoton, ITE, Fintek) ou controladores dedicados (NZXT, Corsair, AquaComputer).",
            Location = new Point(16, 56),
            Size = new Size(620, 20),
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _emptyDescLabel = new Label
        {
            Text = "Dica: Em portateis ou motherboards OEM com BIOS proprietaria, o controlo e gerido exclusivamente pelo firmware do fabricante.",
            Location = new Point(16, 80),
            Size = new Size(620, 20),
            Font = FluentStyle.CreateUiFont(FontStyle.Italic, 8.0f)
        };

        _emptyCard.Controls.Add(_emptyTitleLabel);
        _emptyCard.Controls.Add(_emptyDescLabel);
        _emptyContainer.Controls.Add(_emptyCard);
        _cardContainer.Controls.Add(_emptyContainer);

        Controls.Add(_cardContainer);
        Controls.Add(_telemetryRow);
        Controls.Add(_headerPanel);

        if (engine != null)
            SetEngine(engine);

        ApplyTheme(Theme.Current);
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
        UpdateProfileBadge();
        RefreshChannels();
    }

    private void Engine_ChannelsChanged(object sender, EventArgs e) => RefreshChannels();

    private void Engine_PresetChanged(object sender, EventArgs e) => OnEnginePresetChanged();

    private void PresetCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_engine == null) return;
        if (_presetCombo.SelectedIndex >= 0 && _presetCombo.SelectedIndex <= 4)
        {
            GlobalFanPreset selected = (GlobalFanPreset)_presetCombo.SelectedIndex;
            if (selected != _engine.ActivePreset)
            {
                _engine.ApplyPreset(selected);
                UpdateProfileBadge();
                foreach (FanChannelCard card in _cards)
                    card.SyncUiWithConfig();
            }
        }
    }

    private void OnEnginePresetChanged()
    {
        if (_engine == null) return;
        _presetCombo.SelectedIndex = (int)_engine.ActivePreset;
        UpdateProfileBadge();
    }

    private void UpdateProfileBadge()
    {
        if (_engine == null) return;
        string text = _engine.ActivePreset switch
        {
            GlobalFanPreset.Silent => "Silencioso",
            GlobalFanPreset.Balanced => "Equilibrado",
            GlobalFanPreset.Performance => "Desempenho",
            GlobalFanPreset.Custom => "Personalizado",
            _ => "BIOS Padrao"
        };
        BadgeStyle style = _engine.ActivePreset switch
        {
            GlobalFanPreset.Performance => BadgeStyle.Accent,
            GlobalFanPreset.Silent => BadgeStyle.Success,
            GlobalFanPreset.Balanced => BadgeStyle.Neutral,
            _ => BadgeStyle.Neutral
        };
        _profileBadge.SetBadge(text, style);
        _profileCard.SetMetric("PERFIL GLOBAL", text, "", "Modo Atual", MetricCardSeverity.Normal);
    }

    public void RefreshChannels()
    {
        _cardContainer.SuspendLayout();
        foreach (FanChannelCard card in _cards)
        {
            if (card.Parent != null)
                _cardContainer.Controls.Remove(card.Parent);
            card.Dispose();
        }
        _cards.Clear();

        int gap = LogicalToDeviceUnits(14);

        if (_engine == null || _engine.Channels.Count == 0)
        {
            _emptyContainer.Visible = true;
            _activeFansCard.SetMetric("CANAIS ATIVOS", "0", "canais", "Nenhum Super I/O", MetricCardSeverity.Normal);
        }
        else
        {
            _emptyContainer.Visible = false;
            _activeFansCard.SetMetric("CANAIS ATIVOS", $"{_engine.Channels.Count}", "canais", "Super I/O Ativo", MetricCardSeverity.Cool);

            // Adicionar cartoes de tras para a frente com wrapper de espacamento para manter ordem natural
            for (int i = _engine.Channels.Count - 1; i >= 0; i--)
            {
                FanChannelCard card = new(_engine.Channels[i], _engine);
                _cards.Add(card);

                Panel wrapper = new()
                {
                    Dock = DockStyle.Top,
                    Height = card.Height + gap,
                    Padding = new Padding(0, 0, 0, gap)
                };
                card.Dock = DockStyle.Fill;
                wrapper.Controls.Add(card);
                _cardContainer.Controls.Add(wrapper);
            }
        }

        _cardContainer.ResumeLayout(true);
        ApplyTheme(Theme.Current);
    }

    public void UpdateTelemetry()
    {
        if (_engine == null || !Visible) return;

        // 1. CPU Temp
        float cpu = _engine.LastCpuTemp;
        if (cpu <= 0f && _engine.Computer != null)
        {
            IHardware cpuHw = _engine.Computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpuHw != null)
            {
                cpuHw.Update();
                ISensor s = cpuHw.Sensors.FirstOrDefault(sn => sn.SensorType == SensorType.Temperature);
                if (s?.Value != null) cpu = s.Value.Value;
            }
        }
        if (cpu > 0f)
        {
            MetricCardSeverity sev = cpu >= 85f ? MetricCardSeverity.Hot : (cpu >= 75f ? MetricCardSeverity.Warm : (cpu <= 45f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal));
            _cpuTempCard.SetMetric("TEMP CPU", $"{cpu:F0}", "°C", "Sensor Package", sev, cpu / 100f);
        }
        else
        {
            _cpuTempCard.SetMetric("TEMP CPU", "--", "°C", "Sensor Package", MetricCardSeverity.Normal);
        }

        // 2. GPU Temp
        float gpu = _engine.LastGpuTemp;
        if (gpu <= 0f && _engine.Computer != null)
        {
            IHardware gpuHw = _engine.Computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel);
            if (gpuHw != null)
            {
                gpuHw.Update();
                ISensor s = gpuHw.Sensors.FirstOrDefault(sn => sn.SensorType == SensorType.Temperature);
                if (s?.Value != null) gpu = s.Value.Value;
            }
        }
        if (gpu > 0f)
        {
            MetricCardSeverity sev = gpu >= 83f ? MetricCardSeverity.Hot : (gpu >= 72f ? MetricCardSeverity.Warm : (gpu <= 45f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal));
            _gpuTempCard.SetMetric("TEMP GPU", $"{gpu:F0}", "°C", "Sensor do Nucleo", sev, gpu / 100f);
        }
        else
        {
            _gpuTempCard.SetMetric("TEMP GPU", "--", "°C", "Sensor do Nucleo", MetricCardSeverity.Normal);
        }

        // 3. Atualizar cartoes individuais
        foreach (FanChannelCard card in _cards)
            card.UpdateTelemetry();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;

        _titleLabel.ForeColor = theme.ForegroundColor;
        _subtitleLabel.ForeColor = theme.MutedForegroundColor;
        _presetLabel.ForeColor = theme.ForegroundColor;

        _presetCombo.ApplyTheme(theme);
        _profileBadge.ApplyTheme(theme);
        _safetyBadge?.ApplyTheme(theme);
        _cpuTempCard.ApplyTheme(theme);
        _gpuTempCard.ApplyTheme(theme);
        _activeFansCard.ApplyTheme(theme);
        _profileCard.ApplyTheme(theme);

        _emptyCard.ApplyTheme(theme);
        _emptyTitleLabel.ForeColor = theme.ForegroundColor;
        _emptyDescLabel.ForeColor = theme.MutedForegroundColor;

        foreach (FanChannelCard card in _cards)
            card.ApplyTheme(theme);

        Invalidate();
    }
}
