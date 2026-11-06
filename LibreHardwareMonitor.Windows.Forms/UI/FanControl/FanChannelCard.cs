// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public sealed class FanChannelCard : Panel, IThemedControl
{
    private readonly FanChannelRuntime _channel;
    private readonly FanControlEngine _engine;
    private bool _isHovered;

    private readonly Label _titleLabel;
    private readonly FluentBadge _statusBadge;
    private readonly FluentBadge _emergencyBadge;

    private readonly Label _modeCaption;
    private readonly FluentComboBox _modeCombo;

    private readonly Panel _fixedPanel;
    private readonly TrackBar _fixedSlider;
    private readonly Label _sliderValLabel;

    private readonly Panel _curvePanel;
    private readonly Label _sourceCaption;
    private readonly FluentComboBox _sourceCombo;
    private readonly FluentButton _btnSilent;
    private readonly FluentButton _btnBalanced;
    private readonly FluentButton _btnPerf;
    private readonly Label _curveInfoLabel;
    private readonly Label _hystLabel;

    public FanChannelRuntime Runtime => _channel;

    public FanChannelCard(FanChannelRuntime channel, FanControlEngine engine)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Height = 186;
        Padding = new Padding(16);
        Dock = DockStyle.Top;

        // 1. Title & Status Row
        _titleLabel = new Label
        {
            Text = $"{_channel.HardwareName} — {_channel.ChannelName}",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 10.5f),
            Location = new Point(16, 12),
            AutoSize = true
        };

        _statusBadge = new FluentBadge
        {
            Location = new Point(360, 10),
            Height = 22,
            Style = BadgeStyle.Neutral
        };
        _statusBadge.SetBadge("PWM: -- • RPM: --", BadgeStyle.Neutral);

        _emergencyBadge = new FluentBadge
        {
            Location = new Point(540, 10),
            Height = 22,
            Style = BadgeStyle.Danger,
            Visible = false
        };
        _emergencyBadge.SetBadge("Emergencia Termica >90 C (100% Forcado)", BadgeStyle.Danger);

        // 2. Mode Selector Row
        _modeCaption = new Label
        {
            Text = "Modo:",
            Location = new Point(16, 48),
            AutoSize = true,
            Font = FluentStyle.UiFontSemibold
        };

        _modeCombo = new FluentComboBox
        {
            Location = new Point(66, 44),
            Width = 160
        };
        _modeCombo.Items.AddRange(new object[] { "Auto (BIOS Padrao)", "Fixo (% Manual)", "Curva Dinamica" });
        _modeCombo.SelectedIndex = (int)_channel.Config.Mode;
        _modeCombo.SelectedIndexChanged += ModeCombo_SelectedIndexChanged;

        // 3. Fixed Mode Panel
        _fixedPanel = new Panel { Location = new Point(236, 40), Size = new Size(460, 50), Visible = false };
        _fixedSlider = new TrackBar { Minimum = 0, Maximum = 100, Value = (int)_channel.Config.FixedPwm, TickFrequency = 10, Location = new Point(0, 4), Width = 300 };
        _sliderValLabel = new Label { Text = $"{(int)_channel.Config.FixedPwm}%", Location = new Point(312, 8), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 10f) };
        _fixedSlider.Scroll += (_, _) =>
        {
            _channel.Config.FixedPwm = _fixedSlider.Value;
            _sliderValLabel.Text = $"{_fixedSlider.Value}%";
            _engine.SetCustomMode();
        };
        _fixedPanel.Controls.Add(_fixedSlider);
        _fixedPanel.Controls.Add(_sliderValLabel);

        // 4. Dynamic Curve Mode Panel
        _curvePanel = new Panel { Location = new Point(236, 40), Size = new Size(540, 126), Visible = false };
        _sourceCaption = new Label { Text = "Sensor Alvo:", Location = new Point(0, 6), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _sourceCombo = new FluentComboBox
        {
            Location = new Point(90, 2),
            Width = 150
        };
        _sourceCombo.Items.AddRange(new object[] { "Max(CPU, GPU)", "CPU Package", "GPU Core" });
        _sourceCombo.SelectedIndex = _channel.Config.TemperatureSource switch
        {
            FanChannelConfig.SourceCpu => 1,
            FanChannelConfig.SourceGpu => 2,
            _ => 0
        };
        _sourceCombo.SelectedIndexChanged += SourceCombo_SelectedIndexChanged;

        _btnSilent = new FluentButton { Text = "Silencioso", Location = new Point(252, 2), Width = 90, Height = 28 };
        _btnSilent.Click += (_, _) => ApplyCurve(FanCurve.CreateSilent(), 0);

        _btnBalanced = new FluentButton { Text = "Equilibrado", Location = new Point(348, 2), Width = 96, Height = 28 };
        _btnBalanced.Click += (_, _) => ApplyCurve(FanCurve.CreateBalanced(), 1);

        _btnPerf = new FluentButton { Text = "Desempenho", Location = new Point(450, 2), Width = 104, Height = 28 };
        _btnPerf.Click += (_, _) => ApplyCurve(FanCurve.CreatePerformance(), 2);

        _curveInfoLabel = new Label
        {
            Text = $"Curva: {_channel.Config.Curve}",
            Location = new Point(0, 38),
            Size = new Size(530, 24),
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _hystLabel = new Label
        {
            Text = $"Histerese: +{_channel.Config.HysteresisUpC:F1} C / -{_channel.Config.HysteresisDownC:F1} C  •  Suavizacao: {_channel.Config.ResponseTimeSeconds:F0}s",
            Location = new Point(0, 66),
            AutoSize = true,
            Font = FluentStyle.CreateUiFont(FontStyle.Italic, 8.0f)
        };

        _curvePanel.Controls.Add(_sourceCaption);
        _curvePanel.Controls.Add(_sourceCombo);
        _curvePanel.Controls.Add(_btnSilent);
        _curvePanel.Controls.Add(_btnBalanced);
        _curvePanel.Controls.Add(_btnPerf);
        _curvePanel.Controls.Add(_curveInfoLabel);
        _curvePanel.Controls.Add(_hystLabel);

        Controls.Add(_titleLabel);
        Controls.Add(_statusBadge);
        Controls.Add(_emergencyBadge);
        Controls.Add(_modeCaption);
        Controls.Add(_modeCombo);
        Controls.Add(_fixedPanel);
        Controls.Add(_curvePanel);

        UpdateModePanels();
        ApplyTheme(Theme.Current);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
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

    private void ApplyCurve(FanCurve curve, int btnIndex)
    {
        _channel.Config.Curve = curve;
        _curveInfoLabel.Text = $"Curva: {curve}";
        _engine.SetCustomMode();

        _btnSilent.IsActive = (btnIndex == 0);
        _btnBalanced.IsActive = (btnIndex == 1);
        _btnPerf.IsActive = (btnIndex == 2);
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

        BadgeStyle badgeStyle = _channel.InEmergencyOverride
            ? BadgeStyle.Danger
            : (pwm > 80f ? BadgeStyle.Warning : (_channel.Config.Mode == FanChannelMode.DefaultBios ? BadgeStyle.Neutral : BadgeStyle.Accent));

        _statusBadge.SetBadge($"{pwm:F0}% PWM • {rpmText}", badgeStyle);
        _emergencyBadge.Visible = _channel.InEmergencyOverride;
    }

    public void SyncUiWithConfig()
    {
        _modeCombo.SelectedIndex = (int)_channel.Config.Mode;
        float pwm = _channel.Config.FixedPwm;
        _fixedSlider.Value = (int)(pwm < 0f ? 0f : (pwm > 100f ? 100f : pwm));
        _sliderValLabel.Text = $"{_fixedSlider.Value}%";
        _curveInfoLabel.Text = $"Curva: {_channel.Config.Curve}";
        UpdateModePanels();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        ForeColor = theme.ForegroundColor;

        _titleLabel.ForeColor = theme.ForegroundColor;
        _modeCaption.ForeColor = theme.ForegroundColor;
        _sourceCaption.ForeColor = theme.ForegroundColor;
        _sliderValLabel.ForeColor = theme.AccentColor;
        _curveInfoLabel.ForeColor = theme.ForegroundColor;
        _hystLabel.ForeColor = theme.MutedForegroundColor;

        _modeCombo.ApplyTheme(theme);
        _sourceCombo.ApplyTheme(theme);

        _fixedSlider.BackColor = theme.SurfaceColor;

        _btnSilent.ApplyTheme(theme);
        _btnBalanced.ApplyTheme(theme);
        _btnPerf.ApplyTheme(theme);

        _statusBadge.ApplyTheme(theme);
        _emergencyBadge.ApplyTheme(theme);

        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        e.Graphics.Clear(Parent?.BackColor ?? theme.BackgroundColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = Math.Max(4f, LogicalToDeviceUnits(theme.CornerRadius + 2));
        RectangleF bounds = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        // Hover matches SummaryPanel: surface is HoverBackgroundColor, border stays SurfaceStrokeColor
        Color surfaceColor = _isHovered ? theme.HoverBackgroundColor : theme.SurfaceColor;

        FluentStyle.FillRounded(g, surfaceColor, bounds, radius);
        FluentStyle.DrawRounded(g, theme.SurfaceStrokeColor, bounds, radius);
    }
}
