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

public enum BadgeStyle
{
    Neutral,
    Success,
    Warning,
    Danger,
    Accent
}

/// <summary>
/// Modern Fluent pill badge with crisp, symmetric 1px borders and theme-aware contrast.
/// </summary>
public sealed class FluentBadge : Control, IThemedControl
{
    private string _text = string.Empty;
    private BadgeStyle _style = BadgeStyle.Neutral;

    public FluentBadge()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Height = 22;
        Width = 100;
        Font = FluentStyle.CreateUiFont(FontStyle.Bold, 8f);
    }

    public override string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value ?? string.Empty;
                RecalculateWidth();
                Invalidate();
            }
        }
    }

    public BadgeStyle Style
    {
        get => _style;
        set
        {
            if (_style != value)
            {
                _style = value;
                Invalidate();
            }
        }
    }

    public void SetBadge(string text, BadgeStyle style)
    {
        _text = text ?? string.Empty;
        _style = style;
        RecalculateWidth();
        Invalidate();
    }

    private void RecalculateWidth()
    {
        if (string.IsNullOrEmpty(_text))
            return;

        using Graphics g = CreateGraphics();
        SizeF size = g.MeasureString(_text, Font);
        int pad = LogicalToDeviceUnits(20);
        Width = Math.Max(LogicalToDeviceUnits(40), (int)Math.Ceiling(size.Width) + pad);
    }

    public void ApplyTheme(Theme theme)
    {
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
        g.PixelOffsetMode = PixelOffsetMode.Default;

        // Inset by exactly 1px on all edges so the 1px stroke centered on path never clips at bottom/top
        RectangleF bounds = new(1f, 1f, Width - 2f, Height - 2f);
        float radius = bounds.Height / 2f;

        (Color bg, Color border, Color text) = _style switch
        {
            BadgeStyle.Success => (
                FluentStyle.Blend(theme.SurfaceColor, theme.StateCoolColor, theme.IsDark ? 0.22 : 0.14),
                FluentStyle.Blend(theme.SurfaceStrokeColor, theme.StateCoolColor, 0.55),
                theme.StateCoolColor),
            BadgeStyle.Warning => (
                FluentStyle.Blend(theme.SurfaceColor, theme.StateWarmColor, theme.IsDark ? 0.25 : 0.14),
                FluentStyle.Blend(theme.SurfaceStrokeColor, theme.StateWarmColor, 0.55),
                theme.StateWarmColor),
            BadgeStyle.Danger => (
                FluentStyle.Blend(theme.SurfaceColor, theme.StateHotColor, theme.IsDark ? 0.25 : 0.14),
                FluentStyle.Blend(theme.SurfaceStrokeColor, theme.StateHotColor, 0.55),
                theme.StateHotColor),
            BadgeStyle.Accent => (
                FluentStyle.Blend(theme.SurfaceColor, theme.AccentColor, theme.IsDark ? 0.25 : 0.14),
                theme.AccentColor,
                theme.AccentColor),
            _ => (
                theme.SurfaceColor,
                theme.SurfaceStrokeColor,
                theme.MutedForegroundColor)
        };

        FluentStyle.FillRounded(g, bg, bounds, radius);
        FluentStyle.DrawRounded(g, border, bounds, radius, 1f);

        if (!string.IsNullOrEmpty(_text))
        {
            Rectangle textRect = new(0, 0, Width, Height);
            TextRenderer.DrawText(
                g,
                _text,
                Font,
                textRect,
                text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
