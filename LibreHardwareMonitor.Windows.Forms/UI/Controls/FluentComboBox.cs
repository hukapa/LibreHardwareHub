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
/// Fluent styled ComboBox matching Windows 11 aesthetics:
/// rounded surface, theme-aware border and hover states, custom chevron arrow and owner-drawn items.
/// </summary>
public class FluentComboBox : ComboBox, IThemedControl
{
    private const int WM_PAINT = 0x000F;
    private bool _isHovered;

    public FluentComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        ItemHeight = LogicalToDeviceUnits(24);
        Font = FluentStyle.UiFont;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        ForeColor = theme.ForegroundColor;
        Invalidate();
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

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;

        if (e.Index < 0 || e.Index >= Items.Count)
            return;

        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color bg = isSelected ? theme.HoverBackgroundColor : theme.SurfaceColor;
        Color text = isSelected && !theme.IsDark ? theme.AccentColor : theme.ForegroundColor;

        using (SolidBrush brush = new(bg))
            g.FillRectangle(brush, e.Bounds);

        Rectangle textBounds = new(
            e.Bounds.X + LogicalToDeviceUnits(10),
            e.Bounds.Y,
            e.Bounds.Width - LogicalToDeviceUnits(20),
            e.Bounds.Height);

        string itemText = Items[e.Index]?.ToString() ?? string.Empty;
        TextRenderer.DrawText(
            g,
            itemText,
            isSelected ? FluentStyle.UiFontSemibold : Font,
            textBounds,
            text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (isSelected)
        {
            using Pen focusPen = new(theme.AccentColor, 1f);
            g.DrawRectangle(focusPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT && DropDownStyle == ComboBoxStyle.DropDownList)
        {
            PaintControlSurface();
        }
    }

    private void PaintControlSurface()
    {
        if (Width <= 0 || Height <= 0)
            return;

        using Graphics g = Graphics.FromHwnd(Handle);
        Theme theme = Theme.Current;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle client = ClientRectangle;
        float radius = Math.Max(3f, LogicalToDeviceUnits(theme.CornerRadius));
        RectangleF bounds = new(1f, 1f, client.Width - 2f, client.Height - 2f);

        Color surface = _isHovered ? theme.HoverBackgroundColor : theme.SurfaceColor;
        Color border = Focused ? theme.AccentColor : (_isHovered ? theme.AccentColor : theme.SurfaceStrokeColor);

        FluentStyle.FillRounded(g, surface, bounds, radius);
        FluentStyle.DrawRounded(g, border, bounds, radius, Focused ? 1.5f : 1f);

        // Draw selected text
        string selectedText = SelectedIndex >= 0 && SelectedIndex < Items.Count
            ? Items[SelectedIndex]?.ToString() ?? string.Empty
            : Text;

        int textPadX = LogicalToDeviceUnits(10);
        int arrowWidth = LogicalToDeviceUnits(28);
        Rectangle textRect = new(textPadX, 0, Math.Max(10, client.Width - textPadX - arrowWidth), client.Height);
        TextRenderer.DrawText(
            g,
            selectedText,
            Font,
            textRect,
            theme.ForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // Draw Fluent chevron down arrow
        float chevronX = client.Width - LogicalToDeviceUnits(16);
        float chevronY = (client.Height / 2f) - LogicalToDeviceUnits(2);
        float arrowSize = LogicalToDeviceUnits(4);
        using Pen arrowPen = new(theme.MutedForegroundColor, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(arrowPen, chevronX - arrowSize, chevronY, chevronX, chevronY + arrowSize);
        g.DrawLine(arrowPen, chevronX, chevronY + arrowSize, chevronX + arrowSize, chevronY);
    }
}
