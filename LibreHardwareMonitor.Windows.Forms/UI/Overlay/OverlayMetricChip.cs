// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// A reading that is not on the HUD, as a compact "+ name" chip: one click shows it. Custom sensors the user added carry a
/// small x that deletes them from the configuration.
/// </summary>
public sealed class OverlayMetricChip : Control, IThemedControl
{
    private const TextFormatFlags TextFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private bool _hovered;
    private bool _overRemove;

    public OverlayMetricChip(OverlayMetricItem item)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Item = item;
        Cursor = Cursors.Hand;
        Text = "+ " + item.DisplayName;

        int textWidth = TextRenderer.MeasureText(Text, FluentStyle.UiFont, Size.Empty, TextFlags).Width;
        Size = new Size(textWidth + LogicalToDeviceUnits(24 + (item.IsCustom ? 20 : 0)), LogicalToDeviceUnits(28));
    }

    public OverlayMetricItem Item { get; }

    /// <summary>Raised when the chip is clicked: the reading must be shown on the HUD.</summary>
    public event EventHandler Activated;

    /// <summary>Raised when the x of a custom sensor is clicked: the reading must be deleted.</summary>
    public event EventHandler RemoveRequested;

    private Rectangle RemoveBounds => Item.IsCustom ? new Rectangle(Width - LogicalToDeviceUnits(24), 0, LogicalToDeviceUnits(24), Height) : Rectangle.Empty;

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
        _overRemove = false;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool over = RemoveBounds.Contains(e.Location);
        if (over == _overRemove)
            return;

        _overRemove = over;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left)
            return;

        if (RemoveBounds.Contains(e.Location))
            RemoveRequested?.Invoke(this, EventArgs.Empty);
        else
            Activated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? theme.SurfaceColor);
        if (Width <= 4 || Height <= 4)
            return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        RectangleF bounds = new(0.5f, 0.5f, Width - 1f, Height - 1f);
        float radius = LogicalToDeviceUnits(FluentStyle.ControlCornerLogical);
        Color fill = _hovered ? FluentStyle.Blend(theme.SubtleSurfaceColor, theme.AccentColor, 0.14) : theme.SubtleSurfaceColor;
        Color border = _hovered ? FluentStyle.Blend(theme.SurfaceStrokeColor, theme.AccentColor, 0.55) : theme.SurfaceStrokeColor;
        FluentStyle.FillRounded(g, fill, bounds, radius);
        FluentStyle.DrawRounded(g, border, bounds, radius);

        int right = Item.IsCustom ? RemoveBounds.Left : Width - LogicalToDeviceUnits(6);
        Rectangle text = new(LogicalToDeviceUnits(12), 0, Math.Max(1, right - LogicalToDeviceUnits(12)), Height);
        TextRenderer.DrawText(g, Text, FluentStyle.UiFont, text, _hovered ? theme.ForegroundColor : theme.MutedForegroundColor, TextFlags);

        if (!Item.IsCustom)
            return;

        float cx = RemoveBounds.Left + (RemoveBounds.Width / 2f);
        float cy = Height / 2f;
        float r = LogicalToDeviceUnits(3);
        using Pen pen = new(_overRemove ? theme.StateHotColor : theme.MutedForegroundColor, LogicalToDeviceUnits(1) * 1.25f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
        g.DrawLine(pen, cx - r, cy + r, cx + r, cy - r);
    }
}

/// <summary>Fluid wrap of chips: left to right, a new line when the width runs out; it sizes its own height.</summary>
public sealed class OverlayChipFlow : Control, IThemedControl
{
    public OverlayChipFlow()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = LogicalToDeviceUnits(28);
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        foreach (Control chip in Controls)
            chip.Invalidate();
    }

    /// <summary>Removes and disposes every chip.</summary>
    public void Clear()
    {
        SuspendLayout();
        List<Control> old = new(Controls.Count);
        foreach (Control chip in Controls)
            old.Add(chip);

        Controls.Clear();
        foreach (Control chip in old)
            chip.Dispose();

        ResumeLayout(false);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        int gap = LogicalToDeviceUnits(8);
        int x = 0;
        int y = 0;
        int lineHeight = 0;
        foreach (Control chip in Controls)
        {
            if (x > 0 && x + chip.Width > Width)
            {
                x = 0;
                y += lineHeight + gap;
                lineHeight = 0;
            }

            chip.Location = new Point(x, y);
            x += chip.Width + gap;
            lineHeight = Math.Max(lineHeight, chip.Height);
        }

        int height = Math.Max(LogicalToDeviceUnits(28), y + lineHeight);
        if (Height != height)
            Height = height;
    }
}

/// <summary>Quiet caption of a block of the readings list ("ATIVOS NO HUD (10)"), optionally under a hairline divider.</summary>
public sealed class OverlaySectionCaption : Control, IThemedControl
{
    public OverlaySectionCaption()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = LogicalToDeviceUnits(26);
    }

    /// <summary>Draws a 1 px hairline above the text and leaves room for it.</summary>
    public bool Divider
    {
        get => _divider;
        set
        {
            _divider = value;
            Height = LogicalToDeviceUnits(value ? 40 : 26);
            Invalidate();
        }
    }

    private bool _divider;

    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value;
            Invalidate();
        }
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(theme.SurfaceColor);
        if (Width <= 2 || Height <= 2)
            return;

        int top = 0;
        if (_divider)
        {
            using Pen line = new(theme.LineColor, 1f);
            g.DrawLine(line, 0, LogicalToDeviceUnits(6), Width, LogicalToDeviceUnits(6));
            top = LogicalToDeviceUnits(12);
        }

        Rectangle text = new(0, top, Width, Math.Max(1, Height - top));
        TextRenderer.DrawText(g, Text, FluentStyle.UiFontSmallBold, text, theme.MutedForegroundColor,
                              TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
