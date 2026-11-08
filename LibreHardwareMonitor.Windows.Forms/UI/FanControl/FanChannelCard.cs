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
/// Card for one fan channel. The role chosen by the user ("what is plugged in here") decides what can be done:
/// an unidentified channel can only be identified, a pump never follows a curve, an unused channel is never
/// written. Live PWM/RPM are always shown so the effect of "Identify" can be seen.
/// </summary>
public sealed class FanChannelCard : Panel, IThemedControl
{
    private static Font s_valueFont;
    private static Font s_detailFont;

    private readonly FanChannelRuntime _channel;
    private readonly FanControlEngine _engine;

    private readonly FluentBadge _stateBadge;
    private readonly FluentComboBox _roleCombo;
    private readonly FluentButton _btnIdentify;
    private readonly FluentButton _btnEdit;
    private readonly FluentComboBox _modeCombo;
    private readonly FluentComboBox _sourceCombo;
    private readonly FluentSlider _fixedSlider;
    private readonly FluentButton _btnSilent;
    private readonly FluentButton _btnBalanced;
    private readonly FluentButton _btnPerf;
    private readonly FanCurveEditor _editor;

    private readonly List<FanChannelMode> _modes = new();
    private bool _loading;
    private float _pwm;
    private string _rpmText = string.Empty;
    private string _hintText = string.Empty;
    private bool _hintIsAlert;
    private Rectangle _valueArea;
    private Rectangle _hintArea;
    private Rectangle _sliderValueArea;

    public FanChannelRuntime Runtime => _channel;

    /// <summary>Raised after the role changed, so the page can regroup the cards.</summary>
    public event EventHandler RoleChanged;

    public FanChannelCard(FanChannelRuntime channel, FanControlEngine engine)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        _stateBadge = new FluentBadge { Height = LogicalToDeviceUnits(22) };

        _roleCombo = new FluentComboBox();
        foreach (FanChannelRole role in Enum.GetValues(typeof(FanChannelRole)))
            _roleCombo.Items.Add(FanRolePolicy.DisplayName(role));
        _roleCombo.SelectedIndexChanged += RoleCombo_SelectedIndexChanged;

        _btnIdentify = new FluentButton { Text = "Identificar" };
        _btnIdentify.Width = Math.Max(TextRenderer.MeasureText("Identificar", FluentStyle.UiFontSemibold).Width,
                                      TextRenderer.MeasureText("Parar (8 s)", FluentStyle.UiFontSemibold).Width) + LogicalToDeviceUnits(36);
        _btnIdentify.Click += BtnIdentify_Click;

        _btnEdit = PageMetrics.FitButton(new FluentButton { Text = "Editar" });
        _btnEdit.Click += BtnEdit_Click;

        _modeCombo = new FluentComboBox();
        _modeCombo.SelectedIndexChanged += ModeCombo_SelectedIndexChanged;

        _sourceCombo = new FluentComboBox();
        _sourceCombo.Items.AddRange(new object[] { "Máx. CPU / GPU", "CPU Package", "GPU Core" });
        _sourceCombo.SelectedIndexChanged += SourceCombo_SelectedIndexChanged;

        _fixedSlider = new FluentSlider { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 5 };
        _fixedSlider.Scroll += (_, _) =>
        {
            _engine.Modify(() => _channel.Config.FixedPwm = _fixedSlider.Value);
            _engine.SetCustomMode();
            Invalidate(_sliderValueArea);
        };

        _btnSilent = new FluentButton { Text = "Silenciosa" };
        _btnSilent.Click += (_, _) => ApplyCurve(FanCurve.CreateSilent());
        _btnBalanced = new FluentButton { Text = "Equilibrada" };
        _btnBalanced.Click += (_, _) => ApplyCurve(FanCurve.CreateBalanced());
        _btnPerf = new FluentButton { Text = "Desempenho" };
        _btnPerf.Click += (_, _) => ApplyCurve(FanCurve.CreatePerformance());

        _editor = new FanCurveEditor();
        _editor.CurveChanged += curve =>
        {
            _engine.Modify(() => _channel.Config.Curve = curve);
            UpdateCurveButtons();
        };
        _editor.CurveCommitted += (_, _) => _engine.SetCustomMode();

        Controls.AddRange(new Control[] { _stateBadge, _roleCombo, _btnIdentify, _btnEdit, _modeCombo, _sourceCombo, _fixedSlider, _btnSilent, _btnBalanced, _btnPerf, _editor });

        LoadFromConfig();
        UpdateTelemetry();
        ApplyTheme(Theme.Current);
    }

    private static Font ValueFont => s_valueFont ??= FluentStyle.CreateDisplayFont(17f);

    private static Font DetailFont => s_detailFont ??= FluentStyle.CreateUiFont(FontStyle.Regular, 8.25f);

    private FanChannelRole Role => _channel.Config.Role;

    private bool ShowsModeEditor => FanRolePolicy.AllowsFixed(Role);

    private bool IsFixed => _channel.Config.Mode == FanChannelMode.Fixed && FanRolePolicy.AllowsFixed(Role);

    private bool IsCurve => _channel.Config.Mode == FanChannelMode.Curve && FanRolePolicy.AllowsCurve(Role);

    private static int ClampPercent(float value) => (int)Math.Max(0f, Math.Min(100f, value));

    // ------------------------------------------------------------------ config -> controls

    /// <summary>Fills every editor from the channel configuration without raising change handlers.</summary>
    private void LoadFromConfig()
    {
        _loading = true;
        try
        {
            _roleCombo.SelectedIndex = (int)Role;

            _modes.Clear();
            _modeCombo.Items.Clear();
            _modes.Add(FanChannelMode.DefaultBios);
            _modeCombo.Items.Add("Automático (BIOS)");
            if (FanRolePolicy.AllowsFixed(Role))
            {
                _modes.Add(FanChannelMode.Fixed);
                _modeCombo.Items.Add("Velocidade fixa");
            }

            if (FanRolePolicy.AllowsCurve(Role))
            {
                _modes.Add(FanChannelMode.Curve);
                _modeCombo.Items.Add("Curva de temperatura");
            }

            int index = _modes.IndexOf(_channel.Config.Mode);
            _modeCombo.SelectedIndex = index < 0 ? 0 : index;

            _sourceCombo.SelectedIndex = _channel.Config.TemperatureSource switch
            {
                FanChannelConfig.SourceCpu => 1,
                FanChannelConfig.SourceGpu => 2,
                _ => 0
            };

            int floor = (int)Math.Ceiling(_channel.MinimumPwm);
            _fixedSlider.Minimum = floor;
            _fixedSlider.Maximum = 100;
            _fixedSlider.Value = Math.Max(floor, ClampPercent(_channel.Config.FixedPwm));

            _editor.MinPwm = _channel.MinimumPwm;
            _editor.Curve = _channel.Config.Curve;
        }
        finally
        {
            _loading = false;
        }

        UpdateCurveButtons();
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        bool modes = ShowsModeEditor;
        _modeCombo.Visible = modes;
        _fixedSlider.Visible = modes && IsFixed;
        _sourceCombo.Visible = modes && IsCurve;
        _btnSilent.Visible = modes && IsCurve;
        _btnBalanced.Visible = modes && IsCurve;
        _btnPerf.Visible = modes && IsCurve;
        _editor.Visible = modes && IsCurve;
        PerformLayout();
        Invalidate();
    }

    // ------------------------------------------------------------------ handlers

    private void RoleCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_loading || _roleCombo.SelectedIndex < 0)
            return;

        _engine.SetChannelRole(_channel, (FanChannelRole)_roleCombo.SelectedIndex);
        LoadFromConfig();
        UpdateTelemetry();
        RoleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ModeCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_loading || _modeCombo.SelectedIndex < 0 || _modeCombo.SelectedIndex >= _modes.Count)
            return;

        FanChannelMode mode = _modes[_modeCombo.SelectedIndex];
        _engine.Modify(() =>
        {
            _channel.Config.Mode = mode;
            if (mode == FanChannelMode.DefaultBios)
                _channel.ResetToDefault();
        });

        _engine.SetCustomMode();
        UpdateVisibility();
        UpdateTelemetry();
    }

    private void SourceCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_loading)
            return;

        string source = _sourceCombo.SelectedIndex switch
        {
            1 => FanChannelConfig.SourceCpu,
            2 => FanChannelConfig.SourceGpu,
            _ => FanChannelConfig.SourceMaxCpuGpu
        };
        _engine.Modify(() => _channel.Config.TemperatureSource = source);
        _engine.SetCustomMode();
    }

    private void ApplyCurve(FanCurve curve)
    {
        FanCurve safe = FanCurve.Sanitize(curve.Points, _channel.MinimumPwm);
        _engine.Modify(() => _channel.Config.Curve = safe);
        _editor.Curve = safe;
        _engine.SetCustomMode();
        UpdateCurveButtons();
        Invalidate();
    }

    private void BtnIdentify_Click(object sender, EventArgs e)
    {
        if (_channel.IsIdentifying)
            _engine.CancelIdentify(_channel);
        else
            _engine.Identify(_channel);

        UpdateTelemetry();
    }

    private void BtnEdit_Click(object sender, EventArgs e)
    {
        using ChannelEditDialog dialog = new(_channel.HardwareName, _channel.ChannelName, _channel.Config.Name, _channel.Config.FanCount);
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        _engine.Modify(() =>
        {
            _channel.Config.Name = dialog.ChannelLabel;
            _channel.Config.FanCount = dialog.FanCount;
        });
        _engine.SaveSettings();
        PerformLayout();
        Invalidate();
    }

    private void UpdateCurveButtons()
    {
        List<FanCurvePoint> points = _channel.Config.Curve?.Points ?? new List<FanCurvePoint>();
        SetPresetState(_btnSilent, points.SequenceEqual(FanCurve.Sanitize(FanCurve.CreateSilent().Points, _channel.MinimumPwm).Points));
        SetPresetState(_btnBalanced, points.SequenceEqual(FanCurve.Sanitize(FanCurve.CreateBalanced().Points, _channel.MinimumPwm).Points));
        SetPresetState(_btnPerf, points.SequenceEqual(FanCurve.Sanitize(FanCurve.CreatePerformance().Points, _channel.MinimumPwm).Points));
    }

    private static void SetPresetState(FluentButton button, bool active)
    {
        button.IsActive = active;
        button.Variant = FluentButtonVariant.Standard;
    }

    /// <summary>Called by the page after the configuration changed behind the card's back (preset, wizard, calibration).</summary>
    public void SyncUiWithConfig() => LoadFromConfig();

    // ------------------------------------------------------------------ live data

    public void UpdateTelemetry()
    {
        long now = Environment.TickCount64;

        float pwm = _channel.Control.ControlMode == ControlMode.Software
            ? _channel.LastAppliedPwm
            : (_channel.Control.Sensor?.Value ?? 0f);

        string rpm;
        float? rpmValue = _channel.RpmSensor?.Value;
        if (!rpmValue.HasValue)
            rpm = "Sem leitura de RPM";
        else if (rpmValue.Value < 1f && !_channel.HasSeenRpm)
            rpm = "Sem rotação";
        else
            rpm = string.Format(CultureInfo.CurrentCulture, "{0:N0} RPM", rpmValue.Value);

        (string text, BadgeStyle style) = _channel.InEmergencyOverride ? ("Emergência térmica", BadgeStyle.Danger)
            : _channel.Stalled ? ("Parou de rodar", BadgeStyle.Danger)
            : _channel.SensorLost && _channel.IsManaged ? ("Sem sensor", BadgeStyle.Danger)
            : _channel.IsCalibrating ? ("A calibrar", BadgeStyle.Warning)
            : _channel.IsIdentifying ? ("A identificar", BadgeStyle.Warning)
            : Role == FanChannelRole.Unassigned ? ("Por identificar", BadgeStyle.Warning)
            : Role == FanChannelRole.NotConnected ? ("Não usado", BadgeStyle.Neutral)
            : _channel.IsManaged ? (_channel.Config.Mode == FanChannelMode.Curve ? "Curva" : "Manual", BadgeStyle.Accent)
            : ("BIOS", BadgeStyle.Neutral);

        if (_stateBadge.Text != text || _stateBadge.Style != style)
        {
            _stateBadge.SetBadge(text, style);
            PerformLayout();
            Invalidate();
        }

        string identifyText = _channel.IsIdentifying
            ? $"Parar ({Math.Ceiling(_channel.IdentifyRemainingMs(now) / 1000.0):0} s)"
            : "Identificar";
        if (_btnIdentify.Text != identifyText)
            _btnIdentify.Text = identifyText;

        _btnIdentify.IsActive = _channel.IsIdentifying;

        // While a calibration drives the channel nothing else may change it.
        bool calibrating = _channel.IsCalibrating;
        if (_roleCombo.Enabled == calibrating)
        {
            _roleCombo.Enabled = !calibrating;
            _modeCombo.Enabled = !calibrating;
            _sourceCombo.Enabled = !calibrating;
            _fixedSlider.Enabled = !calibrating;
            _btnIdentify.Enabled = !calibrating;
            _btnEdit.Enabled = !calibrating;
            _editor.Enabled = !calibrating;

            // A calibration just finished: pick up the new floor.
            if (!calibrating)
                LoadFromConfig();
        }

        string hint = HintText(out bool isAlert);
        if (hint != _hintText || isAlert != _hintIsAlert)
        {
            _hintText = hint;
            _hintIsAlert = isAlert;
            PerformLayout(); // the hint line appears/disappears/changes height
            Invalidate(_hintArea);
        }

        if (Math.Abs(pwm - _pwm) > 0.05f || rpm != _rpmText)
        {
            _pwm = pwm;
            _rpmText = rpm;
            Invalidate(_valueArea);
        }

        if (IsCurve)
            _editor.SetOperatingPoint(_channel.LastEffectiveTemp, Math.Max(_channel.MinimumPwm, _channel.Config.Curve.Evaluate(_channel.LastEffectiveTemp)));
    }

    // ------------------------------------------------------------------ layout

    private string HintText(out bool isAlert)
    {
        isAlert = false;
        string alert = _channel.Alert;
        if (alert != null)
        {
            isAlert = true;
            return alert;
        }

        if (_channel.IsCalibrating)
        {
            return string.Format(CultureInfo.CurrentCulture, "Calibração em curso: {0}. O canal volta ao estado anterior no fim.", _channel.CalibrationStatus);
        }

        List<string> parts = new();
        switch (Role)
        {
            case FanChannelRole.Unassigned:
                string hint = FanControlEngine.RoleHint(_channel.Control);
                parts.Add("Use «Identificar» e escolha a função para poder controlar este canal." + (hint.Length > 0 ? " " + hint + "." : string.Empty));
                break;
            case FanChannelRole.NotConnected:
                parts.Add("A app nunca escreve neste canal.");
                break;
            case FanChannelRole.Pump:
                parts.Add("A bomba fica na BIOS por defeito. Em velocidade fixa nunca baixa de 70 %.");
                break;
        }

        FanCalibrationResult cal = _channel.Config.Calibration;
        if (cal != null)
        {
            parts.Add(cal.Responsive
                ? string.Format(CultureInfo.CurrentCulture, "Calibrado: roda a partir de {0:F0} %, máx. {1:N0} RPM (mínimo seguro {2:F0} %).", cal.MinSpinPwm, cal.MaxRpm, cal.SafeFloorPwm)
                : "Calibrado: a velocidade não acompanha o PWM (comum numa bomba ou ventoinha DC).");
        }

        string abort = _channel.CalibrationAbortReason;
        if (abort != null)
            parts.Add("Última calibração interrompida: " + abort + ".");

        return string.Join(" ", parts);
    }

    private string Subtitle()
    {
        bool named = !string.IsNullOrWhiteSpace(_channel.Config.Name);
        string text = named ? $"{_channel.HardwareName} · {_channel.ChannelName}" : _channel.HardwareName;
        if (_channel.Config.FanCount > 1)
            text += $" · {_channel.Config.FanCount} ventoinhas";

        return text;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        int pad = LogicalToDeviceUnits(PageMetrics.CardPadding);
        int gap = LogicalToDeviceUnits(8);
        int inner = Math.Max(0, Width - (pad * 2));
        int headerHeight = LogicalToDeviceUnits(22);

        _stateBadge.Location = new Point(Width - pad - _stateBadge.Width, pad + ((headerHeight - _stateBadge.Height) / 2));

        // Header (painted: icon + name), subtitle (painted), headline value + bar (painted).
        int y = pad + headerHeight + DetailFont.Height + LogicalToDeviceUnits(10);
        _valueArea = new Rectangle(pad, y, inner, ValueFont.Height + LogicalToDeviceUnits(12));
        y = _valueArea.Bottom + LogicalToDeviceUnits(14);

        // Role + Identify + Edit on one row.
        int controlHeight = _roleCombo.Height;
        _btnIdentify.Height = controlHeight;
        _btnEdit.Height = controlHeight;
        int buttons = _btnIdentify.Width + _btnEdit.Width + (gap * 2);
        _roleCombo.SetBounds(pad, y, Math.Max(LogicalToDeviceUnits(100), inner - buttons), controlHeight);
        _btnIdentify.Location = new Point(_roleCombo.Right + gap, y);
        _btnEdit.Location = new Point(_btnIdentify.Right + gap, y);
        y += controlHeight;

        // Hint line (alert, calibration, or why the channel cannot be controlled yet).
        string hintText = HintText(out _);
        if (hintText.Length > 0)
        {
            int lines = TextRenderer.MeasureText(hintText, DetailFont, new Size(inner, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            y += gap;
            _hintArea = new Rectangle(pad, y, inner, lines);
            y = _hintArea.Bottom;
        }
        else
        {
            _hintArea = Rectangle.Empty;
        }

        if (ShowsModeEditor)
        {
            y += gap;
            int half = (inner - gap) / 2;
            if (IsCurve)
            {
                _modeCombo.SetBounds(pad, y, half, controlHeight);
                _sourceCombo.SetBounds(pad + half + gap, y, inner - half - gap, controlHeight);
            }
            else
            {
                _modeCombo.SetBounds(pad, y, inner, controlHeight);
            }

            y += controlHeight;

            if (IsFixed)
            {
                y += gap;
                int valueWidth = LogicalToDeviceUnits(48);
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
                y += buttonHeight + gap;

                _editor.SetBounds(pad, y, inner, LogicalToDeviceUnits(124));
                y = _editor.Bottom;
            }
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

    // ------------------------------------------------------------------ painting

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

        // Header: icon + name, then hardware / sensor line.
        g.DrawImage(IconForRole(), new Rectangle(pad, pad + ((headerHeight - icon) / 2), icon, icon));
        int titleX = pad + icon + LogicalToDeviceUnits(8);
        int titleWidth = Math.Max(0, _stateBadge.Left - titleX - LogicalToDeviceUnits(8));
        TextRenderer.DrawText(g, _channel.DisplayName, FluentStyle.UiFontSemibold, new Rectangle(titleX, pad, titleWidth, headerHeight), theme.ForegroundColor, left);
        TextRenderer.DrawText(g, Subtitle(), DetailFont, new Rectangle(pad, pad + headerHeight, Width - (pad * 2), DetailFont.Height + 2), theme.MutedForegroundColor, left);

        DrawValue(g, theme);

        if (!_hintArea.IsEmpty)
        {
            TextRenderer.DrawText(g, HintText(out bool isAlert), DetailFont, _hintArea, isAlert ? theme.StateHotColor : theme.MutedForegroundColor,
                                  TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        if (IsFixed)
        {
            TextRenderer.DrawText(g, $"{_fixedSlider.Value} %", FluentStyle.UiFontSemibold, _sliderValueArea, theme.ForegroundColor,
                                  TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    private Image IconForRole()
    {
        IHardware hardware = _channel.Control.Sensor?.Hardware;
        return Role switch
        {
            FanChannelRole.CpuCooler => FluentIcons.ForHardware(HardwareType.Cpu),
            FanChannelRole.Gpu => FluentIcons.ForHardware(hardware?.HardwareType is HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia ? hardware.HardwareType : HardwareType.GpuNvidia),
            FanChannelRole.Pump => FluentIcons.ForSensorType(SensorType.Flow),
            _ => FluentIcons.ForSensorType(SensorType.Fan)
        };
    }

    private void DrawValue(Graphics g, Theme theme)
    {
        Rectangle area = _valueArea;
        bool critical = _channel.InEmergencyOverride || _channel.Stalled;
        Color valueColor = critical ? theme.StateHotColor : (Role == FanChannelRole.NotConnected ? theme.MutedForegroundColor : theme.ForegroundColor);
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
            Color bar = critical ? theme.StateHotColor : (_pwm >= 85f ? theme.StateWarmColor : theme.AccentColor);
            FluentStyle.FillRounded(g, bar, new RectangleF(track.X, track.Y, Math.Max(barHeight, track.Width * ratio), barHeight), barHeight / 2);
        }
    }
}
