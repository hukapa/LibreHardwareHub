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

    // Cache de telemetria para evitar qualquer cintilação
    private float _lastCpuTemp = 0f;
    private float _lastGpuTemp = 0f;

    private readonly Panel _headerPanel;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _presetLabel;
    private readonly FluentComboBox _presetCombo;
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
        Padding = new Padding(20);

        int gap = LogicalToDeviceUnits(16);

        // 1. Header (Espaçoso e limpo)
        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 76, Margin = new Padding(0) };
        _titleLabel = new Label
        {
            Text = "Controlo de Ventilação (Fan Control & Curvas)",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 14f),
            Location = new Point(0, 0),
            AutoSize = true
        };

        _subtitleLabel = new Label
        {
            Text = "Curvas dinâmicas e gestão inteligente de ventiladores Super I/O e controladores.",
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 9f),
            Location = new Point(0, 26),
            AutoSize = true
        };

        _presetLabel = new Label
        {
            Text = "Perfil Global:",
            Location = new Point(0, 52),
            AutoSize = true,
            Font = FluentStyle.UiFontSemibold
        };

        _presetCombo = new FluentComboBox
        {
            Location = new Point(88, 48),
            Width = 180
        };
        _presetCombo.Items.AddRange(new object[] { "BIOS Padrão", "Silencioso", "Equilibrado", "Desempenho", "Personalizado" });
        _presetCombo.SelectedIndex = 0;
        _presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;

        _safetyBadge = new FluentBadge
        {
            Location = new Point(282, 48),
            Height = 24,
            Style = BadgeStyle.Success
        };
        _safetyBadge.SetBadge("Proteção Térmica Ativa (>90 °C → 100% Forçado)", BadgeStyle.Success);

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);
        _headerPanel.Controls.Add(_presetLabel);
        _headerPanel.Controls.Add(_presetCombo);
        _headerPanel.Controls.Add(_safetyBadge);

        // 2. Telemetria: 4 cartões KPI
        int cardWidth = 170;
        int cardHeight = 104;
        _telemetryRow = new Panel { Dock = DockStyle.Top, Height = cardHeight + gap, Padding = new Padding(0, 0, 0, gap) };

        _cpuTempCard = new FluentMetricCard { Location = new Point(0, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForHardware(HardwareType.Cpu) };
        _cpuTempCard.SetMetric("TEMP CPU", "--", "°C", "Sensor Package", MetricCardSeverity.Normal);

        _gpuTempCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 1, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Temperature) };
        _gpuTempCard.SetMetric("TEMP GPU", "--", "°C", "Sensor do Núcleo", MetricCardSeverity.Normal);

        _activeFansCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 2, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Fan) };
        _activeFansCard.SetMetric("CANAIS ATIVOS", "0", "canais", "Super I/O", MetricCardSeverity.Normal);

        _profileCard = new FluentMetricCard { Location = new Point((cardWidth + 14) * 3, 0), Size = new Size(cardWidth, cardHeight), Icon = FluentIcons.ForSensorType(SensorType.Control) };
        _profileCard.SetMetric("PERFIL GLOBAL", "BIOS", "", "Modo Atual", MetricCardSeverity.Normal);

        _telemetryRow.Controls.Add(_cpuTempCard);
        _telemetryRow.Controls.Add(_gpuTempCard);
        _telemetryRow.Controls.Add(_activeFansCard);
        _telemetryRow.Controls.Add(_profileCard);

        // 3. Container de Canais
        _cardContainer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 6, 8, 6) };

        // Empty State Card
        _emptyContainer = new Panel { Dock = DockStyle.Top, Height = 110 + gap, Padding = new Padding(0, 0, 0, gap) };
        _emptyCard = new FluentSectionCard
        {
            Dock = DockStyle.Fill,
            Title = "Nenhum Canal de Ventilação Detetado",
            Subtitle = "O sistema não encontrou controladores Super I/O ou dispositivos compatíveis com escrita PWM."
        };

        _emptyTitleLabel = new Label
        {
            Text = "Controlos PWM requerem suporte no chip Super I/O (Nuvoton, ITE, Fintek) ou controladores dedicados (NZXT, Corsair, AquaComputer).",
            Location = new Point(16, 56),
            Size = new Size(640, 20),
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _emptyDescLabel = new Label
        {
            Text = "Dica: Em portáteis ou motherboards OEM com BIOS proprietária, o controlo é gerido exclusivamente pelo firmware do fabricante.",
            Location = new Point(16, 80),
            Size = new Size(640, 20),
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
        UpdateProfileCard();
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
                UpdateProfileCard();
                foreach (FanChannelCard card in _cards)
                    card.SyncUiWithConfig();
            }
        }
    }

    private void OnEnginePresetChanged()
    {
        if (_engine == null) return;
        _presetCombo.SelectedIndex = (int)_engine.ActivePreset;
        UpdateProfileCard();
    }

    private void UpdateProfileCard()
    {
        if (_engine == null) return;
        string text = _engine.ActivePreset switch
        {
            GlobalFanPreset.Silent => "Silencioso",
            GlobalFanPreset.Balanced => "Equilibrado",
            GlobalFanPreset.Performance => "Desempenho",
            GlobalFanPreset.Custom => "Personalizado",
            _ => "BIOS Padrão"
        };
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

            // Adicionar cartões de trás para a frente com wrapper de espaçamento para manter ordem natural
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

        // 1. CPU Temp (sem invocar Update na UI thread)
        float cpu = _engine.LastCpuTemp;
        if (cpu <= 0f && _engine.Computer != null)
        {
            IHardware cpuHw = _engine.Computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpuHw != null)
            {
                ISensor s = cpuHw.Sensors.FirstOrDefault(sn => sn.SensorType == SensorType.Temperature);
                if (s?.Value != null) cpu = s.Value.Value;
            }
        }
        if (cpu > 0f)
            _lastCpuTemp = cpu;
        else if (_lastCpuTemp > 0f)
            cpu = _lastCpuTemp;

        if (cpu > 0f)
        {
            MetricCardSeverity sev = cpu >= 85f ? MetricCardSeverity.Hot : (cpu >= 75f ? MetricCardSeverity.Warm : (cpu <= 45f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal));
            _cpuTempCard.SetMetric("TEMP CPU", $"{cpu:F0}", "°C", "Sensor Package", sev, cpu / 100f);
        }
        else
        {
            _cpuTempCard.SetMetric("TEMP CPU", "--", "°C", "Sensor Package", MetricCardSeverity.Normal);
        }

        // 2. GPU Temp (sem invocar Update na UI thread)
        float gpu = _engine.LastGpuTemp;
        if (gpu <= 0f && _engine.Computer != null)
        {
            IHardware gpuHw = _engine.Computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAmd || h.HardwareType == HardwareType.GpuIntel);
            if (gpuHw != null)
            {
                ISensor s = gpuHw.Sensors.FirstOrDefault(sn => sn.SensorType == SensorType.Temperature);
                if (s?.Value != null) gpu = s.Value.Value;
            }
        }
        if (gpu > 0f)
            _lastGpuTemp = gpu;
        else if (_lastGpuTemp > 0f)
            gpu = _lastGpuTemp;

        if (gpu > 0f)
        {
            MetricCardSeverity sev = gpu >= 83f ? MetricCardSeverity.Hot : (gpu >= 72f ? MetricCardSeverity.Warm : (gpu <= 45f ? MetricCardSeverity.Cool : MetricCardSeverity.Normal));
            _gpuTempCard.SetMetric("TEMP GPU", $"{gpu:F0}", "°C", "Sensor do Núcleo", sev, gpu / 100f);
        }
        else
        {
            _gpuTempCard.SetMetric("TEMP GPU", "--", "°C", "Sensor do Núcleo", MetricCardSeverity.Normal);
        }

        // 3. Atualizar cartões individuais
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
