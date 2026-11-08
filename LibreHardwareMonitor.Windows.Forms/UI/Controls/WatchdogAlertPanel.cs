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
/// Accent-tinted banner shown while a tuning profile is under watchdog test. Children are laid out on
/// one row like a <see cref="ToolbarRow" /> (tag a control with <see cref="PageMetrics.AlignRight" /> or
/// <see cref="PageMetrics.Fill" />).
/// </summary>
public sealed class WatchdogAlertPanel : Panel, IThemedControl
{
    public WatchdogAlertPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Height = LogicalToDeviceUnits(52);
        Padding = new Padding(LogicalToDeviceUnits(PageMetrics.CardPadding), 0, LogicalToDeviceUnits(10), 0);
    }

    private static Color AlertColor(Theme theme) =>
        FluentStyle.Blend(theme.SurfaceColor, theme.AccentColor, theme.IsDark ? 0.20 : 0.12);

    public void ApplyTheme(Theme theme)
    {
        BackColor = AlertColor(theme);
        foreach (Control child in Controls)
        {
            theme.Apply(child);
            if (child is Label label)
            {
                label.BackColor = BackColor;
                label.ForeColor = theme.ForegroundColor;
            }
        }

        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent) => PageMetrics.ArrangeRow(this);

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

        float radius = Math.Max(4f, LogicalToDeviceUnits(theme.CornerRadius + 2));
        RectangleF bounds = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        FluentStyle.FillRounded(g, AlertColor(theme), bounds, radius);
        FluentStyle.DrawRounded(g, theme.AccentColor, bounds, radius, 1f);
    }
}
