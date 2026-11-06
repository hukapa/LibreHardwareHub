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
/// Fluent container card with rounded corners, seamless parent background clearing,
/// subtle hairline border, title header and theme-aware surface.
/// </summary>
public class FluentSectionCard : Panel, IThemedControl
{
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private Font _titleFont;
    private Font _subtitleFont;
    private int _headerHeight = 0;

    public FluentSectionCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Padding = new Padding(LogicalToDeviceUnits(16));
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value ?? string.Empty;
                UpdateHeaderPadding();
                Invalidate();
            }
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        set
        {
            if (_subtitle != value)
            {
                _subtitle = value ?? string.Empty;
                UpdateHeaderPadding();
                Invalidate();
            }
        }
    }

    private void UpdateHeaderPadding()
    {
        if (string.IsNullOrEmpty(_title))
        {
            _headerHeight = 0;
            Padding = new Padding(LogicalToDeviceUnits(16));
        }
        else
        {
            _headerHeight = string.IsNullOrEmpty(_subtitle)
                ? LogicalToDeviceUnits(36)
                : LogicalToDeviceUnits(52);

            Padding = new Padding(
                LogicalToDeviceUnits(16),
                _headerHeight + LogicalToDeviceUnits(8),
                LogicalToDeviceUnits(16),
                LogicalToDeviceUnits(16));
        }
    }

    public virtual void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        foreach (Control child in Controls)
            theme.Apply(child);

        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        // Clears the parent background so the rounded corners never show square artifacts or background bleeding
        e.Graphics.Clear(Parent?.BackColor ?? theme.BackgroundColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = Math.Max(4f, LogicalToDeviceUnits(theme.CornerRadius + 2));
        RectangleF bounds = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        FluentStyle.FillRounded(e.Graphics, theme.SurfaceColor, bounds, radius);
        FluentStyle.DrawRounded(e.Graphics, theme.SurfaceStrokeColor, bounds, radius);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (string.IsNullOrEmpty(_title))
            return;

        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        _titleFont ??= FluentStyle.CreateUiFont(FontStyle.Bold, 10f);
        _subtitleFont ??= FluentStyle.CreateUiFont(FontStyle.Regular, 8.5f);

        int padX = LogicalToDeviceUnits(16);
        int startY = LogicalToDeviceUnits(12);

        Rectangle titleRect = new(padX, startY, Width - (padX * 2), LogicalToDeviceUnits(18));
        TextRenderer.DrawText(g, _title, _titleFont, titleRect, theme.ForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (!string.IsNullOrEmpty(_subtitle))
        {
            Rectangle subRect = new(padX, startY + LogicalToDeviceUnits(20), Width - (padX * 2), LogicalToDeviceUnits(16));
            TextRenderer.DrawText(g, _subtitle, _subtitleFont, subRect, theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // Hairline subtle divider below header
        using Pen dividerPen = new(theme.LineColor, 1f);
        float dividerY = _headerHeight;
        g.DrawLine(dividerPen, padX, dividerY, Width - padX, dividerY);
    }
}
