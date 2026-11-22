// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

public enum OverlayGlyph
{
    Close,
    Up,
    Down
}

/// <summary>
/// Small square button drawn with a 1.25 px vector glyph (X, up or down chevron): discreet until the mouse is over it,
/// dimmed when disabled. Used for hide and move in the HUD readings list.
/// </summary>
public sealed class OverlayIconButton : Control, IThemedControl
{
    private readonly OverlayGlyph _glyph;
    private bool _hovered;

    public OverlayIconButton(OverlayGlyph glyph)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        _glyph = glyph;
        Cursor = Cursors.Hand;
        Size = new Size(LogicalToDeviceUnits(26), LogicalToDeviceUnits(28));
    }

    public void ApplyTheme(Theme theme) => Invalidate();

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _hovered = false;
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? theme.SurfaceColor);
        if (Width <= 2 || Height <= 2)
            return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (_hovered && Enabled)
            FluentStyle.FillRounded(g, FluentStyle.Blend(Parent?.BackColor ?? theme.SurfaceColor, _glyph == OverlayGlyph.Close ? theme.StateHotColor : theme.ForegroundColor, 0.14), new RectangleF(1, 1, Width - 2, Height - 2), LogicalToDeviceUnits(6));

        Color tone = !Enabled ? FluentStyle.Blend(Parent?.BackColor ?? theme.SurfaceColor, theme.MutedForegroundColor, 0.35)
            : _hovered ? (_glyph == OverlayGlyph.Close ? theme.StateHotColor : theme.ForegroundColor)
            : theme.MutedForegroundColor;

        float cx = Width / 2f;
        float cy = Height / 2f;
        float r = LogicalToDeviceUnits(4);
        using Pen pen = new(tone, LogicalToDeviceUnits(1) * 1.25f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (_glyph)
        {
            case OverlayGlyph.Close:
                g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                g.DrawLine(pen, cx - r, cy + r, cx + r, cy - r);
                break;
            case OverlayGlyph.Up:
                g.DrawLines(pen, new[] { new PointF(cx - r, cy + (r / 2)), new PointF(cx, cy - (r / 2)), new PointF(cx + r, cy + (r / 2)) });
                break;
            default:
                g.DrawLines(pen, new[] { new PointF(cx - r, cy - (r / 2)), new PointF(cx, cy + (r / 2)), new PointF(cx + r, cy - (r / 2)) });
                break;
        }
    }
}

/// <summary>The 2 x 3 dot grip at the left of a reading: it shows where to take the row to reorder it.</summary>
public sealed class OverlayDragHandle : Control, IThemedControl
{
    private bool _hovered;

    public OverlayDragHandle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Cursor = Cursors.SizeAll;
        Size = new Size(LogicalToDeviceUnits(18), LogicalToDeviceUnits(28));
    }

    public void ApplyTheme(Theme theme) => Invalidate();

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? theme.SurfaceColor);
        if (Width <= 2 || Height <= 2)
            return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color tone = _hovered ? theme.ForegroundColor : FluentStyle.Blend(Parent?.BackColor ?? theme.SurfaceColor, theme.MutedForegroundColor, 0.7);
        float scale = DeviceDpi / 96f;
        float dot = Math.Max(2f, 2.4f * scale);
        float gapX = 5f * scale;
        float gapY = 5.5f * scale;
        float left = (Width - gapX - dot) / 2f;
        float top = (Height - (gapY * 2) - dot) / 2f;
        using SolidBrush brush = new(tone);
        for (int column = 0; column < 2; column++)
        {
            for (int row = 0; row < 3; row++)
                g.FillEllipse(brush, left + (column * gapX), top + (row * gapY), dot, dot);
        }
    }
}
