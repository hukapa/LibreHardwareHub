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
    private readonly FluentSlider _fixedSlider;
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

        Height = 150;
        Padding = new Padding(18);
        Dock = DockStyle.Top;

        // 1. Linha de Título e Badges
        _titleLabel = new Label
        {
            Text = $"{_channel.HardwareName} — {_channel.ChannelName}",
            Font = FluentStyle.CreateUiFont(FontStyle.Bold, 11f),
            Location = new Point(18, 14),
            AutoSize = true
        };

        _statusBadge = new FluentBadge
        {
            Location = new Point(380, 12),
            Height = 24,
            Style = BadgeStyle.Neutral
        };
        _statusBadge.SetBadge("PWM: -- • RPM: --", BadgeStyle.Neutral);

        _emergencyBadge = new FluentBadge
        {
            Location = new Point(570, 12),
            Height = 24,
            Style = BadgeStyle.Danger,
            Visible = false
        };
        _emergencyBadge.SetBadge("Emergência Térmica >90 °C (100% Forçado)", BadgeStyle.Danger);

        // 2. Linha do Seletor de Modo
        _modeCaption = new Label
        {
            Text = "Modo:",
            Location = new Point(18, 52),
            AutoSize = true,
            Font = FluentStyle.UiFontSemibold
        };

        _modeCombo = new FluentComboBox
        {
            Location = new Point(72, 48),
            Width = 175
        };
        _modeCombo.Items.AddRange(new object[] { "Auto (BIOS Padrão)", "Fixo (% Manual)", "Curva Dinâmica" });
        _modeCombo.SelectedIndex = (int)_channel.Config.Mode;
        _modeCombo.SelectedIndexChanged += ModeCombo_SelectedIndexChanged;

        // 3. Painel de Modo Fixo
        _fixedPanel = new Panel { Location = new Point(265, 42), Size = new Size(500, 48), Visible = false };
        _fixedSlider = new FluentSlider { Minimum = 0, Maximum = 100, Value = (int)_channel.Config.FixedPwm, SmallChange = 1, LargeChange = 5, Location = new Point(0, 8), Width = 320 };
        _sliderValLabel = new Label { Text = $"{(int)_channel.Config.FixedPwm}%", Location = new Point(332, 11), AutoSize = true, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 10f) };
        _fixedSlider.Scroll += (_, _) =>
        {
            _channel.Config.FixedPwm = _fixedSlider.Value;
            _sliderValLabel.Text = $"{_fixedSlider.Value}%";
            _engine.SetCustomMode();
        };
        _fixedPanel.Controls.Add(_fixedSlider);
        _fixedPanel.Controls.Add(_sliderValLabel);

        // 4. Painel de Curva Dinâmica
        _curvePanel = new Panel { Location = new Point(265, 42), Size = new Size(580, 94), Visible = false };
        _sourceCaption = new Label { Text = "Sensor Alvo:", Location = new Point(0, 8), AutoSize = true, Font = FluentStyle.UiFontSemibold };
        _sourceCombo = new FluentComboBox
        {
            Location = new Point(90, 4),
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

        _btnSilent = new FluentButton { Text = "Silencioso", Location = new Point(252, 4), Width = 96, Height = 30 };
        _btnSilent.Click += (_, _) => ApplyCurve(FanCurve.CreateSilent(), 0);

        _btnBalanced = new FluentButton { Text = "Equilibrado", Location = new Point(356, 4), Width = 102, Height = 30 };
        _btnBalanced.Click += (_, _) => ApplyCurve(FanCurve.CreateBalanced(), 1);

        _btnPerf = new FluentButton { Text = "Desempenho", Location = new Point(466, 4), Width = 108, Height = 30 };
        _btnPerf.Click += (_, _) => ApplyCurve(FanCurve.CreatePerformance(), 2);

        _curveInfoLabel = new Label
        {
            Text = $"Curva: {_channel.Config.Curve}",
            Location = new Point(0, 44),
            Size = new Size(560, 22),
            Font = FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f)
        };

        _hystLabel = new Label
        {
            Text = $"Histerese: +{_channel.Config.HysteresisUpC:F1} °C / -{_channel.Config.HysteresisDownC:F1} °C  •  Suavização: {_channel.Config.ResponseTimeSeconds:F0}s",
            Location = new Point(0, 68),
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
        _btnSilent.Variant = btnIndex == 0 ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;

        _btnBalanced.IsActive = (btnIndex == 1);
        _btnBalanced.Variant = btnIndex == 1 ? FluentButtonVariant.Success : FluentButtonVariant.Standard;

        _btnPerf.IsActive = (btnIndex == 2);
        _btnPerf.Variant = btnIndex == 2 ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;
    }

    private void UpdateModePanels()
    {
        bool isFixed = _channel.Config.Mode == FanChannelMode.Fixed;
        bool isCurve = _channel.Config.Mode == FanChannelMode.Curve;

        _fixedPanel.Visible = isFixed;
        _curvePanel.Visible = isCurve;

        Height = isCurve ? 154 : (isFixed ? 112 : 98);
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
        _fixedSlider.ApplyTheme(theme);

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

        Color surfaceColor = _isHovered ? theme.HoverBackgroundColor : theme.SurfaceColor;
        FluentStyle.FillRounded(g, surfaceColor, bounds, radius);
        FluentStyle.DrawRounded(g, theme.SurfaceStrokeColor, bounds, radius);
    }
}
