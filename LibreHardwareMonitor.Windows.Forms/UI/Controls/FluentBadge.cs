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
        Font = FluentStyle.UiFontSmallBold; // the shared 8 pt bold: a badge owns no font of its own
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

    private static (Color Fill, Color Border, Color Text) SoftPill(Theme theme, Color state, double fill, double hairline) =>
        (FluentStyle.Blend(theme.SurfaceColor, state, fill), FluentStyle.Blend(theme.SurfaceColor, state, hairline), state);

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

        // Soft pill: translucent state color over the surface (about 14%), hairline border at 25%.
        const double fill = 0.14;
        const double hairline = 0.25;
        (Color bg, Color border, Color text) = _style switch
        {
            BadgeStyle.Success => SoftPill(theme, theme.StateCoolColor, fill, hairline),
            BadgeStyle.Warning => SoftPill(theme, theme.StateWarmColor, fill, hairline),
            BadgeStyle.Danger => SoftPill(theme, theme.StateHotColor, fill, hairline),
            BadgeStyle.Accent => SoftPill(theme, theme.AccentColor, fill, hairline),
            _ => (
                theme.SubtleSurfaceColor,
                FluentStyle.Blend(theme.SubtleSurfaceColor, theme.SurfaceStrokeColor, 0.5),
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
