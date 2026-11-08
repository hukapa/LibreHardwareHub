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

public enum FluentButtonVariant
{
    Standard,
    Accent,
    Success,
    Danger
}

/// <summary>
/// Fluent Windows 11 rounded button supporting Accent, Standard, Success and Danger variants
/// with anti-aliased borders, hover animations and cohesive theming.
/// </summary>
public class FluentButton : ButtonBase, IThemedControl
{
    private bool _isHovered;
    private bool _isPressed;
    private bool _isActive;
    private FluentButtonVariant _variant = FluentButtonVariant.Standard;

    public FluentButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Height = LogicalToDeviceUnits(32);
        Font = FluentStyle.UiFont;
        Cursor = Cursors.Hand;
    }

    public FluentButtonVariant Variant
    {
        get => _variant;
        set
        {
            if (_variant != value)
            {
                _variant = value;
                Invalidate();
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                Invalidate();
            }
        }
    }

    public void ApplyTheme(Theme theme) => Invalidate();

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
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _isPressed = false;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // The corners outside the rounded rectangle show the parent, never a system button color.
        pevent.Graphics.Clear(Parent?.BackColor ?? Theme.Current.BackgroundColor);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        Theme theme = Theme.Current;
        Graphics g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle client = ClientRectangle;
        if (client.Width <= 1 || client.Height <= 1)
            return;

        float radius = Math.Max(3f, LogicalToDeviceUnits(theme.CornerRadius));
        // Crisp 1px border on pixel centers, covering the whole control (same geometry as the combo box beside it).
        RectangleF bounds = new(0.5f, 0.5f, client.Width - 1f, client.Height - 1f);

        // Hover/pressed are checked against the real pointer, not only the enter/leave events: a modal dialog opened by
        // this button swallows the leave event and the button would stay highlighted afterwards.
        bool hovered = _isHovered && Enabled && client.Contains(PointToClient(Cursor.Position));
        bool pressed = _isPressed && hovered && (Control.MouseButtons & MouseButtons.Left) != 0;

        Color bg;
        Color border;
        Color text;

        switch (_variant)
        {
            case FluentButtonVariant.Accent:
                bg = pressed
                    ? FluentStyle.Blend(theme.AccentColor, Color.Black, 0.20)
                    : (hovered ? FluentStyle.Blend(theme.AccentColor, Color.White, 0.15) : theme.AccentColor);
                border = bg;
                text = theme.AccentForegroundColor;
                break;

            case FluentButtonVariant.Success:
                Color successBg = Color.FromArgb(16, 124, 65);
                bg = pressed
                    ? FluentStyle.Blend(successBg, Color.Black, 0.20)
                    : (hovered ? FluentStyle.Blend(successBg, Color.White, 0.15) : successBg);
                border = bg;
                text = Color.White;
                break;

            case FluentButtonVariant.Danger:
                Color dangerBg = Color.FromArgb(196, 43, 28);
                bg = pressed
                    ? FluentStyle.Blend(dangerBg, Color.Black, 0.20)
                    : (hovered ? FluentStyle.Blend(dangerBg, Color.White, 0.15) : dangerBg);
                border = bg;
                text = Color.White;
                break;

            case FluentButtonVariant.Standard:
            default:
                if (_isActive)
                {
                    bg = FluentStyle.Blend(theme.SurfaceColor, theme.AccentColor, theme.IsDark ? 0.25 : 0.15);
                    border = theme.AccentColor;
                    text = theme.ForegroundColor;
                }
                else
                {
                    bg = pressed
                        ? theme.SelectedBackgroundColor
                        : (hovered ? theme.HoverBackgroundColor : theme.SurfaceColor);
                    border = hovered ? theme.AccentColor : theme.SurfaceStrokeColor;
                    text = theme.ForegroundColor;
                }
                break;
        }

        FluentStyle.FillRounded(g, bg, bounds, radius);
        FluentStyle.DrawRounded(g, border, bounds, radius, _isActive ? 1.5f : 1f);

        // Text
        Font font = (_variant == FluentButtonVariant.Accent || _isActive) ? FluentStyle.UiFontSemibold : Font;
        TextRenderer.DrawText(
            g,
            Text,
            font,
            client,
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
