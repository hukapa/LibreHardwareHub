// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls;

/// <summary>
/// Modern Windows 11 Fluent slider control with anti-aliased rounded track,
/// colored progress fill, smooth interactive thumb and full dark/light theme support.
/// Eliminates native WinForms TrackBar rendering glitches.
/// </summary>
public sealed class FluentSlider : Control, IThemedControl
{
    private int _minimum = 0;
    private int _maximum = 100;
    private int _value = 50;
    private int _smallChange = 1;
    private int _largeChange = 5;
    private int _tickFrequency = 10;

    private bool _isHovered;
    private bool _isDragging;

    public event EventHandler Scroll;
    public event EventHandler ValueChanged;

    public FluentSlider()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);

        Height = LogicalToDeviceUnits(28);
        Width = LogicalToDeviceUnits(260);
        Cursor = Cursors.Hand;
    }

    public int Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum != value)
            {
                _minimum = value;
                if (_maximum < _minimum)
                    _maximum = _minimum;
                if (_value < _minimum)
                    Value = _minimum;
                Invalidate();
            }
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum != value)
            {
                _maximum = value;
                if (_minimum > _maximum)
                    _minimum = _maximum;
                if (_value > _maximum)
                    Value = _maximum;
                Invalidate();
            }
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
            if (_value != clamped)
            {
                _value = clamped;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                Scroll?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public int SmallChange
    {
        get => _smallChange;
        set => _smallChange = Math.Max(1, value);
    }

    public int LargeChange
    {
        get => _largeChange;
        set => _largeChange = Math.Max(1, value);
    }

    public int TickFrequency
    {
        get => _tickFrequency;
        set => _tickFrequency = Math.Max(1, value);
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = Parent?.BackColor ?? theme.SurfaceColor;
        Invalidate();
    }

    private float ValueRatio
    {
        get
        {
            if (_maximum <= _minimum)
                return 0f;
            return (float)(_value - _minimum) / (_maximum - _minimum);
        }
    }

    private void UpdateValueFromX(int mouseX)
    {
        int thumbRadius = LogicalToDeviceUnits(8);
        int trackLeft = thumbRadius + LogicalToDeviceUnits(4);
        int trackWidth = Width - (trackLeft * 2);

        if (trackWidth <= 0)
            return;

        float ratio = Math.Max(0f, Math.Min(1f, (float)(mouseX - trackLeft) / trackWidth));
        int newVal = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
        Value = newVal;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Enabled)
        {
            _isDragging = true;
            Focus();
            UpdateValueFromX(e.X);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging && Enabled)
        {
            UpdateValueFromX(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            Invalidate();
        }
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!Enabled)
            return;

        switch (e.KeyCode)
        {
            case Keys.Left:
            case Keys.Down:
                Value -= _smallChange;
                e.Handled = true;
                break;
            case Keys.Right:
            case Keys.Up:
                Value += _smallChange;
                e.Handled = true;
                break;
            case Keys.PageDown:
                Value -= _largeChange;
                e.Handled = true;
                break;
            case Keys.PageUp:
                Value += _largeChange;
                e.Handled = true;
                break;
            case Keys.Home:
                Value = _minimum;
                e.Handled = true;
                break;
            case Keys.End:
                Value = _maximum;
                e.Handled = true;
                break;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        Theme theme = Theme.Current;
        pevent.Graphics.Clear(Parent?.BackColor ?? theme.SurfaceColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int thumbRadius = LogicalToDeviceUnits(_isDragging ? 9 : (_isHovered ? 8 : 7));
        int trackHeight = LogicalToDeviceUnits(4);
        int trackLeft = LogicalToDeviceUnits(12);
        int trackWidth = Width - (trackLeft * 2);
        int centerY = Height / 2;

        if (trackWidth <= 0)
            return;

        float ratio = ValueRatio;
        float thumbCenterX = trackLeft + (trackWidth * ratio);

        // 1. Inactive Track (Right side)
        RectangleF trackRect = new(trackLeft, centerY - (trackHeight / 2f), trackWidth, trackHeight);
        Color trackBg = Enabled
            ? FluentStyle.Blend(theme.SurfaceColor, theme.ForegroundColor, theme.IsDark ? 0.20 : 0.15)
            : FluentStyle.Blend(theme.SurfaceColor, theme.ForegroundColor, 0.08);

        FluentStyle.FillRounded(g, trackBg, trackRect, trackHeight / 2f);

        // 2. Active Track (Left side, filled with Accent)
        if (ratio > 0f)
        {
            float activeWidth = thumbCenterX - trackLeft;
            if (activeWidth > 0f)
            {
                RectangleF activeRect = new(trackLeft, centerY - (trackHeight / 2f), activeWidth, trackHeight);
                Color activeColor = Enabled ? theme.AccentColor : theme.MutedForegroundColor;
                FluentStyle.FillRounded(g, activeColor, activeRect, trackHeight / 2f);
            }
        }

        // 3. Thumb
        RectangleF thumbRect = new(thumbCenterX - thumbRadius, centerY - thumbRadius, thumbRadius * 2, thumbRadius * 2);
        Color thumbFill = Enabled
            ? (_isDragging ? FluentStyle.Blend(theme.AccentColor, Color.White, 0.25) : theme.AccentColor)
            : theme.MutedForegroundColor;

        using (Brush brush = new SolidBrush(thumbFill))
        {
            g.FillEllipse(brush, thumbRect);
        }

        // Inner white/light dot in thumb for clarity
        if (Enabled)
        {
            float innerRadius = thumbRadius * 0.40f;
            RectangleF innerRect = new(thumbCenterX - innerRadius, centerY - innerRadius, innerRadius * 2, innerRadius * 2);
            using Brush innerBrush = new SolidBrush(Color.White);
            g.FillEllipse(innerBrush, innerRect);
        }
    }
}
