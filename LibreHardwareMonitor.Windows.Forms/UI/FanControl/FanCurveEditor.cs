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
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>
/// Temperature to duty curve editor. Drag a point to move it, double-click empty space to add one, right-click a
/// point to remove it. Every edit goes through <see cref="FanCurve.Sanitize" />, so the result always keeps the
/// channel minimum, never drops as the temperature rises, and ends at 100 %. The last point can only move sideways.
/// </summary>
public sealed class FanCurveEditor : Control, IThemedControl
{
    private static Font s_labelFont;

    private FanCurve _curve = FanCurve.CreateBalanced();
    private float _minPwm;
    private float _currentTemp = float.NaN;
    private float _targetPwm;
    private int _hover = -1;
    private int _drag = -1;

    public FanCurveEditor()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = LogicalToDeviceUnits(124);
    }

    /// <summary>Raised on every change while dragging (the curve is already sanitized).</summary>
    public event Action<FanCurve> CurveChanged;

    /// <summary>Raised when a drag, an add or a remove is finished: the moment to persist.</summary>
    public event EventHandler CurveCommitted;

    private static Font LabelFont => s_labelFont ??= FluentStyle.CreateUiFont(FontStyle.Regular, 7.5f);

    public FanCurve Curve
    {
        get => _curve;
        set
        {
            _curve = value ?? FanCurve.CreateBalanced();
            Invalidate();
        }
    }

    /// <summary>Lowest duty allowed for this channel (role minimum, raised by a calibration).</summary>
    public float MinPwm
    {
        get => _minPwm;
        set
        {
            if (_minPwm != value)
            {
                _minPwm = value;
                Invalidate();
            }
        }
    }

    /// <summary>Shows where the channel is operating right now.</summary>
    public void SetOperatingPoint(float temperature, float pwm)
    {
        if (Math.Abs(_currentTemp - temperature) < 0.05f && Math.Abs(_targetPwm - pwm) < 0.05f)
            return;

        _currentTemp = temperature;
        _targetPwm = pwm;
        Invalidate();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        Invalidate();
    }

    // ------------------------------------------------------------------ geometry

    private Rectangle Plot
    {
        get
        {
            int left = LogicalToDeviceUnits(32);
            int top = LogicalToDeviceUnits(8);
            int right = LogicalToDeviceUnits(10);
            int bottom = LogicalToDeviceUnits(18);
            return new Rectangle(left, top, Math.Max(10, Width - left - right), Math.Max(10, Height - top - bottom));
        }
    }

    private float XFor(float temperature, Rectangle plot) =>
        plot.X + (plot.Width * (Math.Max(FanCurve.MinTemperature, Math.Min(FanCurve.MaxTemperature, temperature)) - FanCurve.MinTemperature) / (FanCurve.MaxTemperature - FanCurve.MinTemperature));

    private float YFor(float pwm, Rectangle plot) =>
        plot.Bottom - (plot.Height * Math.Max(0f, Math.Min(100f, pwm)) / 100f);

    private float TemperatureAt(float x, Rectangle plot) =>
        FanCurve.MinTemperature + ((x - plot.X) / plot.Width * (FanCurve.MaxTemperature - FanCurve.MinTemperature));

    private float PwmAt(float y, Rectangle plot) => (plot.Bottom - y) / plot.Height * 100f;

    private int HitTest(Point p)
    {
        Rectangle plot = Plot;
        float radius = LogicalToDeviceUnits(10);
        List<FanCurvePoint> points = _curve.Points;
        for (int i = 0; i < points.Count; i++)
        {
            float dx = XFor(points[i].Temperature, plot) - p.X;
            float dy = YFor(points[i].PwmPercent, plot) - p.Y;
            if ((dx * dx) + (dy * dy) <= radius * radius)
                return i;
        }

        return -1;
    }

    // ------------------------------------------------------------------ editing

    private void Apply(List<FanCurvePoint> points)
    {
        _curve = FanCurve.Sanitize(points, _minPwm);
        CurveChanged?.Invoke(_curve);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int hit = HitTest(e.Location);

        if (e.Button == MouseButtons.Left && hit >= 0)
        {
            _drag = hit;
            Capture = true;
        }
        else if (e.Button == MouseButtons.Right && hit >= 0 && hit < _curve.Points.Count - 1 && _curve.Points.Count > 2)
        {
            List<FanCurvePoint> points = _curve.Points.ToList();
            points.RemoveAt(hit);
            _hover = -1;
            Apply(points);
            CurveCommitted?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left || HitTest(e.Location) >= 0 || _curve.Points.Count >= FanCurve.MaxPoints)
            return;

        Rectangle plot = Plot;
        if (!plot.Contains(e.Location))
            return;

        // The new point sits on the existing curve at that temperature, so adding it does not change the curve.
        float temperature = (float)Math.Round(Math.Min(FanCurve.FullSpeedByTemperature - 1f, TemperatureAt(e.X, plot)));
        float pwm = (float)Math.Round(Math.Max(_minPwm, _curve.Evaluate(temperature)));
        List<FanCurvePoint> points = _curve.Points.ToList();
        points.Add(new FanCurvePoint(temperature, pwm));
        Apply(points);
        CurveCommitted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_drag >= 0 && _drag < _curve.Points.Count)
        {
            DragPoint(e.Location);
            return;
        }

        int hit = HitTest(e.Location);
        if (hit != _hover)
        {
            _hover = hit;
            Cursor = hit < 0 ? Cursors.Default : (hit == _curve.Points.Count - 1 ? Cursors.SizeWE : Cursors.SizeAll);
            Invalidate();
        }
    }

    private void DragPoint(Point location)
    {
        Rectangle plot = Plot;
        List<FanCurvePoint> points = _curve.Points.ToList();
        int i = _drag;
        bool last = i == points.Count - 1;

        float minTemp = i == 0 ? FanCurve.MinTemperature : points[i - 1].Temperature + 1f;
        float maxTemp = last ? FanCurve.FullSpeedByTemperature : points[i + 1].Temperature - 1f;
        float temperature = (float)Math.Round(Math.Max(minTemp, Math.Min(maxTemp, TemperatureAt(location.X, plot))));

        // The last point is always 100 %; the others stay between their neighbours and above the channel minimum.
        float minDuty = Math.Max(_minPwm, i == 0 ? 0f : points[i - 1].PwmPercent);
        float maxDuty = last ? 100f : points[i + 1].PwmPercent;
        float pwm = last ? 100f : (float)Math.Round(Math.Max(minDuty, Math.Min(maxDuty, PwmAt(location.Y, plot))));

        if (points[i].Temperature == temperature && points[i].PwmPercent == pwm)
            return;

        points[i] = new FanCurvePoint(temperature, pwm);
        Apply(points);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_drag >= 0)
        {
            _drag = -1;
            Capture = false;
            CurveCommitted?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_drag < 0 && _hover != -1)
        {
            _hover = -1;
            Invalidate();
        }
    }

    // ------------------------------------------------------------------ painting

    protected override void OnPaintBackground(PaintEventArgs e) => e.Graphics.Clear(Parent?.BackColor ?? Theme.Current.SurfaceColor);

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle plot = Plot;
        TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

        Color plotBack = FluentStyle.Blend(theme.SurfaceColor, theme.ForegroundColor, theme.IsDark ? 0.04 : 0.03);
        FluentStyle.FillRounded(g, plotBack, plot, LogicalToDeviceUnits(4));

        // Grid and axis labels.
        using (Pen grid = new(theme.LineColor) { DashStyle = DashStyle.Dot })
        {
            foreach (int pwm in new[] { 0, 50, 100 })
            {
                float y = YFor(pwm, plot);
                if (pwm != 0)
                    g.DrawLine(grid, plot.X, y, plot.Right, y);

                TextRenderer.DrawText(g, $"{pwm} %", LabelFont, new Rectangle(0, (int)y - LogicalToDeviceUnits(7), plot.X - LogicalToDeviceUnits(4), LogicalToDeviceUnits(14)),
                                      theme.MutedForegroundColor, flags | TextFormatFlags.Right);
            }

            foreach (int temp in new[] { 20, 40, 60, 80, 100 })
            {
                float x = XFor(temp, plot);
                g.DrawLine(grid, x, plot.Y, x, plot.Bottom);
                TextRenderer.DrawText(g, $"{temp}°", LabelFont, new Rectangle((int)x - LogicalToDeviceUnits(14), plot.Bottom + LogicalToDeviceUnits(2), LogicalToDeviceUnits(28), LogicalToDeviceUnits(14)),
                                      theme.MutedForegroundColor, flags | TextFormatFlags.HorizontalCenter);
            }
        }

        // Channel minimum: the duty the engine never goes below.
        if (_minPwm > 0f)
        {
            float y = YFor(_minPwm, plot);
            using Pen floor = new(Color.FromArgb(150, theme.StateWarmColor)) { DashStyle = DashStyle.Dash };
            g.DrawLine(floor, plot.X, y, plot.Right, y);
        }

        List<FanCurvePoint> points = _curve.Points;
        if (points.Count == 0)
            return;

        List<PointF> line = new() { new PointF(plot.X, YFor(points[0].PwmPercent, plot)) };
        line.AddRange(points.Select(p => new PointF(XFor(p.Temperature, plot), YFor(p.PwmPercent, plot))));
        line.Add(new PointF(plot.Right, YFor(points[points.Count - 1].PwmPercent, plot)));

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

        // Operating point.
        if (!float.IsNaN(_currentTemp))
        {
            float cx = XFor(_currentTemp, plot);
            using (Pen marker = new(theme.MutedForegroundColor) { DashStyle = DashStyle.Dash })
                g.DrawLine(marker, cx, plot.Y, cx, plot.Bottom);

            float dot = LogicalToDeviceUnits(7);
            float cy = YFor(_targetPwm, plot);
            using SolidBrush ring = new(theme.SurfaceColor);
            using SolidBrush center = new(theme.StateCoolColor);
            g.FillEllipse(ring, cx - (dot / 2) - 1, cy - (dot / 2) - 1, dot + 2, dot + 2);
            g.FillEllipse(center, cx - (dot / 2), cy - (dot / 2), dot, dot);
        }

        // Editable points.
        for (int i = 0; i < points.Count; i++)
        {
            bool active = i == _hover || i == _drag;
            float radius = LogicalToDeviceUnits(active ? 6 : 5) - (active ? 0f : 0.5f);
            float px = XFor(points[i].Temperature, plot);
            float py = YFor(points[i].PwmPercent, plot);
            using SolidBrush border = new(theme.SurfaceColor);
            using SolidBrush body = new(active ? theme.ForegroundColor : theme.AccentColor);
            g.FillEllipse(border, px - radius - 1.5f, py - radius - 1.5f, (radius * 2) + 3, (radius * 2) + 3);
            g.FillEllipse(body, px - radius, py - radius, radius * 2, radius * 2);
        }

        // Value of the point being hovered or dragged.
        int shown = _drag >= 0 ? _drag : _hover;
        if (shown >= 0 && shown < points.Count)
        {
            string text = string.Format(CultureInfo.CurrentCulture, "{0:F0} °C → {1:F0} %", points[shown].Temperature, points[shown].PwmPercent);
            Size size = TextRenderer.MeasureText(g, text, LabelFont, Size.Empty, flags);
            float px = XFor(points[shown].Temperature, plot);
            float py = YFor(points[shown].PwmPercent, plot);
            RectangleF tip = new(
                Math.Max(plot.X, Math.Min(plot.Right - size.Width - LogicalToDeviceUnits(12), px - (size.Width / 2f) - LogicalToDeviceUnits(6))),
                py > plot.Y + LogicalToDeviceUnits(30) ? py - size.Height - LogicalToDeviceUnits(16) : py + LogicalToDeviceUnits(10),
                size.Width + LogicalToDeviceUnits(12), size.Height + LogicalToDeviceUnits(6));
            FluentStyle.FillRounded(g, FluentStyle.Blend(theme.SurfaceColor, theme.ForegroundColor, 0.16), tip, LogicalToDeviceUnits(4));
            TextRenderer.DrawText(g, text, LabelFont, Rectangle.Round(tip), theme.ForegroundColor, flags | TextFormatFlags.HorizontalCenter);
        }
    }
}
