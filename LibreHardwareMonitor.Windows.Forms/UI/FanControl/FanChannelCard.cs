// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>
/// Compact card for one fan channel, built on the dashboard KPI language: icon + name, headline PWM value,
/// detail line and progress bar, followed by the mode editor (fixed slider or curve presets with a preview).
/// </summary>
public sealed class FanChannelCard : Panel, IThemedControl
{
    private const float ChartMinTemp = 20f;
    private const float ChartMaxTemp = 100f;

    private static Font s_valueFont;
    private static Font s_detailFont;

    private readonly FanChannelRuntime _channel;
    private readonly FanControlEngine _engine;

    private readonly FluentBadge _stateBadge;
    private readonly FluentComboBox _modeCombo;
    private readonly FluentComboBox _sourceCombo;
    private readonly FluentSlider _fixedSlider;
    private readonly FluentButton _btnSilent;
    private readonly FluentButton _btnBalanced;
    private readonly FluentButton _btnPerf;

    private float _pwm;
    private string _rpmText = string.Empty;
    private Rectangle _valueArea;
    private Rectangle _sliderValueArea;
    private Rectangle _chartArea;

    public FanChannelRuntime Runtime => _channel;

    public FanChannelCard(FanChannelRuntime channel, FanControlEngine engine)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        _stateBadge = new FluentBadge { Height = LogicalToDeviceUnits(22) };

        _modeCombo = new FluentComboBox();
        _modeCombo.Items.AddRange(new object[] { "Automático (BIOS)", "Velocidade fixa", "Curva de temperatura" });
        _modeCombo.SelectedIndex = (int)_channel.Config.Mode;
        _modeCombo.SelectedIndexChanged += ModeCombo_SelectedIndexChanged;

        _sourceCombo = new FluentComboBox();
        _sourceCombo.Items.AddRange(new object[] { "Máx. CPU / GPU", "CPU Package", "GPU Core" });
        _sourceCombo.SelectedIndex = _channel.Config.TemperatureSource switch
        {
            FanChannelConfig.SourceCpu => 1,
            FanChannelConfig.SourceGpu => 2,
            _ => 0
        };
        _sourceCombo.SelectedIndexChanged += SourceCombo_SelectedIndexChanged;

        _fixedSlider = new FluentSlider { Minimum = 0, Maximum = 100, Value = ClampPercent(_channel.Config.FixedPwm), SmallChange = 1, LargeChange = 5 };
        _fixedSlider.Scroll += (_, _) =>
        {
            _channel.Config.FixedPwm = _fixedSlider.Value;
            _engine.SetCustomMode();
            Invalidate(_sliderValueArea);
        };

        _btnSilent = new FluentButton { Text = "Silenciosa" };
        _btnSilent.Click += (_, _) => ApplyCurve(FanCurve.CreateSilent());

        _btnBalanced = new FluentButton { Text = "Equilibrada" };
        _btnBalanced.Click += (_, _) => ApplyCurve(FanCurve.CreateBalanced());

        _btnPerf = new FluentButton { Text = "Desempenho" };
        _btnPerf.Click += (_, _) => ApplyCurve(FanCurve.CreatePerformance());

        Controls.AddRange(new Control[] { _stateBadge, _modeCombo, _sourceCombo, _fixedSlider, _btnSilent, _btnBalanced, _btnPerf });

        UpdateModeControls();
        UpdateCurveButtons();
        UpdateTelemetry();
        ApplyTheme(Theme.Current);
    }

    private static Font ValueFont => s_valueFont ??= FluentStyle.CreateDisplayFont(17f);

    private static Font DetailFont => s_detailFont ??= FluentStyle.CreateUiFont(FontStyle.Regular, 8.25f);

    private static int ClampPercent(float value) => (int)Math.Max(0f, Math.Min(100f, value));

    private bool IsFixed => _channel.Config.Mode == FanChannelMode.Fixed;

    private bool IsCurve => _channel.Config.Mode == FanChannelMode.Curve;

    private void ModeCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        _channel.Config.Mode = (FanChannelMode)_modeCombo.SelectedIndex;
        if (_channel.Config.Mode == FanChannelMode.DefaultBios)
            _channel.ResetToDefault();

        UpdateModeControls();
        _engine.SetCustomMode();
        UpdateTelemetry();
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
        _engine.SetCustomMode();
        UpdateCurveButtons();
        Invalidate();
    }

    private void UpdateCurveButtons()
    {
        List<FanCurvePoint> points = _channel.Config.Curve?.Points ?? new List<FanCurvePoint>();
        SetPresetState(_btnSilent, points.SequenceEqual(FanCurve.CreateSilent().Points));
        SetPresetState(_btnBalanced, points.SequenceEqual(FanCurve.CreateBalanced().Points));
        SetPresetState(_btnPerf, points.SequenceEqual(FanCurve.CreatePerformance().Points));
    }

    private static void SetPresetState(FluentButton button, bool active)
    {
        button.IsActive = active;
        button.Variant = FluentButtonVariant.Standard;
    }

    private void UpdateModeControls()
    {
        _fixedSlider.Visible = IsFixed;
        _sourceCombo.Visible = IsCurve;
        _btnSilent.Visible = IsCurve;
        _btnBalanced.Visible = IsCurve;
        _btnPerf.Visible = IsCurve;
        PerformLayout();
        Invalidate();
    }

    public void UpdateTelemetry()
    {
        float pwm = _channel.Control.ControlMode == ControlMode.Software
            ? _channel.LastAppliedPwm
            : (_channel.Control.Sensor?.Value ?? 0f);

        string rpm = _channel.RpmSensor?.Value != null
            ? string.Format(CultureInfo.CurrentCulture, "{0:N0} RPM", _channel.RpmSensor.Value.Value)
            : "Sem sensor de RPM";

        (string text, BadgeStyle style) = _channel.InEmergencyOverride
            ? ("Emergência térmica", BadgeStyle.Danger)
            : _channel.Config.Mode switch
            {
                FanChannelMode.Fixed => ("Manual", BadgeStyle.Accent),
                FanChannelMode.Curve => ("Curva", BadgeStyle.Accent),
                _ => ("BIOS", BadgeStyle.Neutral)
            };

        if (_stateBadge.Text != text || _stateBadge.Style != style)
            _stateBadge.SetBadge(text, style);

        if (Math.Abs(pwm - _pwm) > 0.05f || rpm != _rpmText)
        {
            _pwm = pwm;
            _rpmText = rpm;
            Invalidate(_valueArea);
            if (IsCurve)
                Invalidate(_chartArea);
        }
    }

    public void SyncUiWithConfig()
    {
        _modeCombo.SelectedIndex = (int)_channel.Config.Mode;
        _fixedSlider.Value = ClampPercent(_channel.Config.FixedPwm);
        UpdateModeControls();
        UpdateCurveButtons();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        int pad = LogicalToDeviceUnits(PageMetrics.CardPadding);
        int gap = LogicalToDeviceUnits(8);
        int inner = Math.Max(0, Width - (pad * 2));
        int headerHeight = LogicalToDeviceUnits(22);

        _stateBadge.Location = new Point(Width - pad - _stateBadge.Width, pad + ((headerHeight - _stateBadge.Height) / 2));

        // Headline value + detail + progress bar (painted).
        int y = pad + headerHeight + LogicalToDeviceUnits(6);
        _valueArea = new Rectangle(pad, y, inner, ValueFont.Height + LogicalToDeviceUnits(12));
        y = _valueArea.Bottom + LogicalToDeviceUnits(14);

        // Mode (and source when following a curve) side by side.
        int half = (inner - gap) / 2;
        if (IsCurve)
        {
            _modeCombo.SetBounds(pad, y, half, _modeCombo.Height);
            _sourceCombo.SetBounds(pad + half + gap, y, inner - half - gap, _sourceCombo.Height);
        }
        else
        {
            _modeCombo.SetBounds(pad, y, inner, _modeCombo.Height);
        }

        y += _modeCombo.Height;

        if (IsFixed)
        {
            y += gap;
            int valueWidth = LogicalToDeviceUnits(44);
            _fixedSlider.SetBounds(pad, y, inner - valueWidth - gap, _fixedSlider.Height);
            _sliderValueArea = new Rectangle(pad + inner - valueWidth, y, valueWidth, _fixedSlider.Height);
            y += _fixedSlider.Height;
        }

        if (IsCurve)
        {
            y += gap;
            int buttonHeight = LogicalToDeviceUnits(30);
            int third = (inner - (gap * 2)) / 3;
            _btnSilent.SetBounds(pad, y, third, buttonHeight);
            _btnBalanced.SetBounds(pad + third + gap, y, third, buttonHeight);
            _btnPerf.SetBounds(pad + ((third + gap) * 2), y, inner - ((third + gap) * 2), buttonHeight);
            y += buttonHeight + LogicalToDeviceUnits(12);

            _chartArea = new Rectangle(pad, y, inner, LogicalToDeviceUnits(72));
            y = _chartArea.Bottom;
        }
        else
        {
            _chartArea = Rectangle.Empty;
        }

        int height = y + pad;
        if (Height != height)
            Height = height;
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        ForeColor = theme.ForegroundColor;
        foreach (Control child in Controls)
        {
            if (child is IThemedControl themed)
                themed.ApplyTheme(theme);
        }

        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Current.BackgroundColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = LogicalToDeviceUnits(theme.CornerRadius + 2);
        RectangleF card = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        FluentStyle.FillRounded(g, theme.SurfaceColor, card, radius);
        FluentStyle.DrawRounded(g, theme.SurfaceStrokeColor, card, radius);

        int pad = LogicalToDeviceUnits(PageMetrics.CardPadding);
        int icon = LogicalToDeviceUnits(16);
        int headerHeight = LogicalToDeviceUnits(22);
        TextFormatFlags left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

        // Header: icon + channel name.
        g.DrawImage(FluentIcons.ForSensorType(SensorType.Fan), new Rectangle(pad, pad + ((headerHeight - icon) / 2), icon, icon));
        int titleX = pad + icon + LogicalToDeviceUnits(8);
        int titleWidth = Math.Max(0, _stateBadge.Left - titleX - LogicalToDeviceUnits(8));
        TextRenderer.DrawText(g, _channel.ChannelName, FluentStyle.UiFontSemibold, new Rectangle(titleX, pad, titleWidth, headerHeight), theme.ForegroundColor, left);

        DrawValue(g, theme);

        if (IsFixed)
        {
            TextRenderer.DrawText(g, $"{_fixedSlider.Value} %",FluentStyle.UiFontSemibold, _sliderValueArea, theme.ForegroundColor,
                                  TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        if (IsCurve && !_chartArea.IsEmpty)
            DrawCurve(g, theme);
    }

    private void DrawValue(Graphics g, Theme theme)
    {
        Rectangle area = _valueArea;
        Color valueColor = _channel.InEmergencyOverride ? theme.StateHotColor : theme.ForegroundColor;
        string value = string.Format(CultureInfo.CurrentCulture, "{0:F0} %", _pwm);

        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        Size valueSize = TextRenderer.MeasureText(g, value, ValueFont, Size.Empty, flags);
        TextRenderer.DrawText(g, value, ValueFont, new Point(area.X, area.Y), valueColor, flags);

        int detailX = area.X + valueSize.Width + LogicalToDeviceUnits(8);
        int detailY = area.Y + valueSize.Height - DetailFont.Height - LogicalToDeviceUnits(3);
        TextRenderer.DrawText(g, "PWM  ·  " + _rpmText, DetailFont, new Rectangle(detailX, detailY, Math.Max(0, area.Right - detailX), DetailFont.Height + 2),
                              theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        float barHeight = Math.Max(3f, LogicalToDeviceUnits(4));
        RectangleF track = new(area.X, area.Bottom - barHeight, area.Width, barHeight);
        FluentStyle.FillRounded(g, FluentStyle.Blend(theme.SurfaceColor, theme.ForegroundColor, 0.1), track, barHeight / 2);

        float ratio = Math.Max(0f, Math.Min(1f, _pwm / 100f));
        if (ratio > 0f)
        {
            Color bar = _channel.InEmergencyOverride ? theme.StateHotColor : (_pwm >= 85f ? theme.StateWarmColor : theme.AccentColor);
            FluentStyle.FillRounded(g, bar, new RectangleF(track.X, track.Y, Math.Max(barHeight, track.Width * ratio), barHeight), barHeight / 2);
        }
    }

    private void DrawCurve(Graphics g, Theme theme)
    {
        List<FanCurvePoint> points = _channel.Config.Curve?.Points;
        if (points == null || points.Count == 0)
            return;

        int labelHeight = DetailFont.Height;
        Rectangle plot = new(_chartArea.X, _chartArea.Y, _chartArea.Width, _chartArea.Height - labelHeight - LogicalToDeviceUnits(4));

        float X(float temp) => plot.X + (plot.Width * (Math.Max(ChartMinTemp, Math.Min(ChartMaxTemp, temp)) - ChartMinTemp) / (ChartMaxTemp - ChartMinTemp));
        float Y(float pwm) => plot.Bottom - (plot.Height * Math.Max(0f, Math.Min(100f, pwm)) / 100f);

        // Plot frame: subtle background and a mid grid line.
        Color plotBack = FluentStyle.Blend(theme.SurfaceColor, theme.ForegroundColor, theme.IsDark ? 0.04 : 0.03);
        FluentStyle.FillRounded(g, plotBack, plot, LogicalToDeviceUnits(4));
        using (Pen grid = new(theme.LineColor) { DashStyle = DashStyle.Dot })
            g.DrawLine(grid, plot.X, Y(50f), plot.Right, Y(50f));

        List<PointF> line = new() { new PointF(plot.X, Y(points[0].PwmPercent)) };
        line.AddRange(points.Select(p => new PointF(X(p.Temperature), Y(p.PwmPercent))));
        line.Add(new PointF(plot.Right, Y(points[points.Count - 1].PwmPercent)));

        using (GraphicsPath area = new())
        {
            area.AddLines(line.ToArray());
            area.AddLine(plot.Right, plot.Bottom, plot.X, plot.Bottom);
            area.CloseFigure();
            using SolidBrush fill = new(Color.FromArgb(theme.IsDark ? 48 : 36, theme.AccentColor));
            g.FillPath(fill, area);
        }

        using (Pen pen = new(theme.AccentColor, Math.Max(1.5f, LogicalToDeviceUnits(2))) { LineJoin = LineJoin.Round })
            g.DrawLines(pen, line.ToArray());

        // Current operating point.
        float temp = _channel.LastEffectiveTemp;
        float target = _channel.Config.Curve.Evaluate(temp);
        float cx = X(temp);
        float cy = Y(target);
        using (Pen marker = new(theme.MutedForegroundColor) { DashStyle = DashStyle.Dash })
            g.DrawLine(marker, cx, plot.Y, cx, plot.Bottom);

        float dot = LogicalToDeviceUnits(8);
        using (SolidBrush ring = new(theme.SurfaceColor))
            g.FillEllipse(ring, cx - (dot / 2) - 1, cy - (dot / 2) - 1, dot + 2, dot + 2);
        using (SolidBrush center = new(theme.AccentColor))
            g.FillEllipse(center, cx - (dot / 2), cy - (dot / 2), dot, dot);

        // Axis labels.
        Rectangle labels = new(_chartArea.X, _chartArea.Bottom - labelHeight, _chartArea.Width, labelHeight);
        TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(g, $"{ChartMinTemp:F0} °C", DetailFont, labels, theme.MutedForegroundColor, flags | TextFormatFlags.Left);
        TextRenderer.DrawText(g, string.Format(CultureInfo.CurrentCulture, "Agora: {0:F0} °C → {1:F0} %", temp, target), DetailFont, labels, theme.MutedForegroundColor, flags | TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(g, $"{ChartMaxTemp:F0} °C", DetailFont, labels, theme.MutedForegroundColor, flags | TextFormatFlags.Right);
    }
}
