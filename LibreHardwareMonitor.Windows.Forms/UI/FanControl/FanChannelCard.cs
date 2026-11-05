// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public sealed class FanChannelCard : Panel, IThemedControl
{
    private readonly FanChannelRuntime _channel;
    private readonly FanControlEngine _engine;

    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly ComboBox _modeCombo;
    private readonly ComboBox _sourceCombo;
    private readonly TrackBar _fixedSlider;
    private readonly Label _sliderValLabel;
    private readonly Panel _curvePanel;
    private readonly Panel _fixedPanel;
    private readonly Label _curveInfoLabel;
    private readonly Label _emergencyLabel;

    public FanChannelRuntime Runtime => _channel;

    public FanChannelCard(FanChannelRuntime channel, FanControlEngine engine)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Height = 180;
        Padding = new Padding(14);
        Margin = new Padding(0, 0, 0, 10);
        Dock = DockStyle.Top;

        _titleLabel = new Label
        {
            Text = $"{_channel.HardwareName} — {_channel.ChannelName}",
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold),
            Location = new Point(14, 12),
            AutoSize = true
        };

        _statusLabel = new Label
        {
            Text = "PWM: -- | RPM: --",
            Font = new Font(Font.FontFamily, 9.0f, FontStyle.Regular),
            Location = new Point(14, 36),
            AutoSize = true
        };

        _emergencyLabel = new Label
        {
            Text = "[EMERGENCIA TERMICA] > 90 C - Forcado 100% PWM",
            Font = new Font(Font.FontFamily, 9.0f, FontStyle.Bold),
            ForeColor = Color.Crimson,
            Location = new Point(14, 56),
            AutoSize = true,
            Visible = false
        };

        Label modeCaption = new() { Text = "Modo:", Location = new Point(14, 82), AutoSize = true };
        _modeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(60, 78),
            Width = 140
        };
        _modeCombo.Items.AddRange(new object[] { "Auto (BIOS)", "Fixo (%)", "Curva Dinamica" });
        _modeCombo.SelectedIndex = (int)_channel.Config.Mode;
        _modeCombo.SelectedIndexChanged += ModeCombo_SelectedIndexChanged;

        // Painel Fixo
        _fixedPanel = new Panel { Location = new Point(210, 70), Size = new Size(380, 50), Visible = false };
        _fixedSlider = new TrackBar { Minimum = 0, Maximum = 100, Value = (int)_channel.Config.FixedPwm, TickFrequency = 10, Location = new Point(0, 4), Width = 280 };
        _sliderValLabel = new Label { Text = $"{(int)_channel.Config.FixedPwm}%", Location = new Point(290, 10), AutoSize = true, Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold) };
        _fixedSlider.Scroll += (_, _) =>
        {
            _channel.Config.FixedPwm = _fixedSlider.Value;
            _sliderValLabel.Text = $"{_fixedSlider.Value}%";
            _engine.SetCustomMode();
        };
        _fixedPanel.Controls.Add(_fixedSlider);
        _fixedPanel.Controls.Add(_sliderValLabel);

        // Painel Curva
        _curvePanel = new Panel { Location = new Point(210, 70), Size = new Size(480, 95), Visible = false };
        Label sourceCaption = new() { Text = "Sensor Alvo:", Location = new Point(0, 10), AutoSize = true };
        _sourceCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(80, 6),
            Width = 160
        };
        _sourceCombo.Items.AddRange(new object[] { "Max(CPU, GPU)", "CPU Package", "GPU Core" });
        _sourceCombo.SelectedIndex = _channel.Config.TemperatureSource switch
        {
            FanChannelConfig.SourceCpu => 1,
            FanChannelConfig.SourceGpu => 2,
            _ => 0
        };
        _sourceCombo.SelectedIndexChanged += SourceCombo_SelectedIndexChanged;

        Button btnSilent = new() { Text = "Silencioso", Location = new Point(250, 5), Width = 72, Height = 25 };
        btnSilent.Click += (_, _) => ApplyCurve(FanCurve.CreateSilent());

        Button btnBalanced = new() { Text = "Equilibrado", Location = new Point(326, 5), Width = 76, Height = 25 };
        btnBalanced.Click += (_, _) => ApplyCurve(FanCurve.CreateBalanced());

        Button btnPerf = new() { Text = "Desempenho", Location = new Point(406, 5), Width = 84, Height = 25 };
        btnPerf.Click += (_, _) => ApplyCurve(FanCurve.CreatePerformance());

        _curveInfoLabel = new Label
        {
            Text = $"Pontos: {_channel.Config.Curve}",
            Location = new Point(0, 36),
            Size = new Size(480, 30),
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Regular)
        };

        Label hystLabel = new()
        {
            Text = $"Histerese: +{_channel.Config.HysteresisUpC:F1} C / -{_channel.Config.HysteresisDownC:F1} C | Suavizacao: {_channel.Config.ResponseTimeSeconds:F0}s",
            Location = new Point(0, 70),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 8.0f, FontStyle.Italic)
        };

        _curvePanel.Controls.Add(sourceCaption);
        _curvePanel.Controls.Add(_sourceCombo);
        _curvePanel.Controls.Add(btnSilent);
        _curvePanel.Controls.Add(btnBalanced);
        _curvePanel.Controls.Add(btnPerf);
        _curvePanel.Controls.Add(_curveInfoLabel);
        _curvePanel.Controls.Add(hystLabel);

        Controls.Add(_titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_emergencyLabel);
        Controls.Add(modeCaption);
        Controls.Add(_modeCombo);
        Controls.Add(_fixedPanel);
        Controls.Add(_curvePanel);

        UpdateModePanels();
        ApplyTheme(Theme.Current);
    }

    private void ModeCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        _channel.Config.Mode = (FanChannelMode)_modeCombo.SelectedIndex;
        if (_channel.Config.Mode == FanChannelMode.DefaultBios)
            _channel.ResetToDefault();

        UpdateModePanels();
        _engine.SetCustomMode();
    }

    private void SourceCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        _channel.Config.TemperatureSource = _sourceCombo.SelectedIndex switch
        {
            1 => FanChannelConfig.SourceCpu,
            2 => FanChannelConfig.SourceGpu,
            _ => FanChannelConfig.SourceMaxCpuGpu
        };
        _engine.SetCustomMode();
    }

    private void ApplyCurve(FanCurve curve)
    {
        _channel.Config.Curve = curve;
        _curveInfoLabel.Text = $"Pontos: {curve}";
        _engine.SetCustomMode();
    }

    private void UpdateModePanels()
    {
        _fixedPanel.Visible = _channel.Config.Mode == FanChannelMode.Fixed;
        _curvePanel.Visible = _channel.Config.Mode == FanChannelMode.Curve;
    }

    public void UpdateTelemetry()
    {
        float pwm = _channel.Control.ControlMode == Hardware.ControlMode.Software
            ? _channel.LastAppliedPwm
            : (_channel.Control.Sensor?.Value ?? 0f);

        string rpmText = _channel.RpmSensor?.Value != null
            ? $"{_channel.RpmSensor.Value.Value:F0} RPM"
            : "Sem sensor RPM";

        _statusLabel.Text = $"Estado: {pwm:F0}% PWM  •  {rpmText}";

        _emergencyLabel.Visible = _channel.InEmergencyOverride;

        if (_channel.InEmergencyOverride)
        {
            _statusLabel.ForeColor = Color.Crimson;
        }
        else
        {
            _statusLabel.ForeColor = Theme.Current.MutedForegroundColor;
        }
    }

    public void SyncUiWithConfig()
    {
        _modeCombo.SelectedIndex = (int)_channel.Config.Mode;
        float pwm = _channel.Config.FixedPwm;
        _fixedSlider.Value = (int)(pwm < 0f ? 0f : (pwm > 100f ? 100f : pwm));
        _sliderValLabel.Text = $"{_fixedSlider.Value}%";
        _curveInfoLabel.Text = $"Pontos: {_channel.Config.Curve}";
        UpdateModePanels();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        ForeColor = theme.ForegroundColor;
        _titleLabel.ForeColor = theme.ForegroundColor;
        _statusLabel.ForeColor = theme.MutedForegroundColor;
        _curveInfoLabel.ForeColor = theme.MutedForegroundColor;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using Pen borderPen = new(Theme.Current.SurfaceStrokeColor, 1f);
        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        g.DrawRectangle(borderPen, rect);
    }
}
