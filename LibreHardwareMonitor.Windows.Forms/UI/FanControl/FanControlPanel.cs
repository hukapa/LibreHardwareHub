// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public sealed class FanControlPanel : UserControl, IThemedControl
{
    private FanControlEngine _engine;
    private readonly List<FanChannelCard> _cards = new();

    private readonly Panel _headerPanel;
    private readonly Label _titleLabel;
    private readonly Label _tempLabel;
    private readonly ComboBox _presetCombo;
    private readonly Panel _cardContainer;
    private readonly Label _emptyLabel;

    public FanControlPanel() : this(null)
    {
    }

    public FanControlPanel(FanControlEngine engine)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Dock = DockStyle.Fill;
        Padding = new Padding(12);

        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(4) };

        _titleLabel = new Label
        {
            Text = "Controlo de Ventoinhas",
            Font = new Font(Font.FontFamily, 14f, FontStyle.Bold),
            Location = new Point(4, 4),
            AutoSize = true
        };

        _tempLabel = new Label
        {
            Text = "Telemetria: CPU -- C  •  GPU -- C",
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular),
            Location = new Point(4, 34),
            AutoSize = true
        };

        Label presetLabel = new() { Text = "Perfil Global:", Location = new Point(420, 10), AutoSize = true };
        _presetCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(500, 6),
            Width = 160
        };
        _presetCombo.Items.AddRange(new object[] { "BIOS Padrao", "Silencioso", "Equilibrado", "Desempenho", "Personalizado" });
        _presetCombo.SelectedIndex = 0;
        _presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;

        _headerPanel.Controls.Add(_titleLabel);
        _headerPanel.Controls.Add(_tempLabel);
        _headerPanel.Controls.Add(presetLabel);
        _headerPanel.Controls.Add(_presetCombo);

        _cardContainer = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 8, 8, 8) };

        _emptyLabel = new Label
        {
            Text = "Nenhum canal de controlo de ventiladores detectado (chips Super I/O ou controladores compativeis).",
            Font = new Font(Font.FontFamily, 10f, FontStyle.Italic),
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = true
        };
        _cardContainer.Controls.Add(_emptyLabel);

        Controls.Add(_cardContainer);
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
                foreach (FanChannelCard card in _cards)
                    card.SyncUiWithConfig();
            }
        }
    }

    private void OnEnginePresetChanged()
    {
        if (_engine == null) return;
        _presetCombo.SelectedIndex = (int)_engine.ActivePreset;
    }

    public void RefreshChannels()
    {
        _cardContainer.SuspendLayout();
        foreach (FanChannelCard card in _cards)
        {
            _cardContainer.Controls.Remove(card);
            card.Dispose();
        }
        _cards.Clear();

        if (_engine == null || _engine.Channels.Count == 0)
        {
            _emptyLabel.Visible = true;
        }
        else
        {
            _emptyLabel.Visible = false;
            // Adicionar cartoes de tras para a frente para manter ordem natural com Dock = Top
            for (int i = _engine.Channels.Count - 1; i >= 0; i--)
            {
                FanChannelCard card = new(_engine.Channels[i], _engine);
                _cards.Add(card);
                _cardContainer.Controls.Add(card);
            }
        }

        _cardContainer.ResumeLayout(true);
        ApplyTheme(Theme.Current);
    }

    public void UpdateTelemetry()
    {
        if (_engine == null) return;

        _tempLabel.Text = $"Telemetria Termica: CPU {_engine.LastCpuTemp:F0} C  •  GPU {_engine.LastGpuTemp:F0} C";

        if (_engine.HasEmergencyAlert)
        {
            _tempLabel.Text += "  [ALERTA DE EMERGENCIA TERMICA ATIVO]";
            _tempLabel.ForeColor = Color.Crimson;
        }
        else
        {
            _tempLabel.ForeColor = Theme.Current.MutedForegroundColor;
        }

        foreach (FanChannelCard card in _cards)
            card.UpdateTelemetry();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;
        _headerPanel.BackColor = theme.BackgroundColor;
        _titleLabel.ForeColor = theme.ForegroundColor;
        _tempLabel.ForeColor = theme.MutedForegroundColor;
        _emptyLabel.ForeColor = theme.MutedForegroundColor;

        foreach (FanChannelCard card in _cards)
            card.ApplyTheme(theme);
    }
}
