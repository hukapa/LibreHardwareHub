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

public sealed class WatchdogAlertPanel : Panel, IThemedControl
{
    public WatchdogAlertPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    public void ApplyTheme(Theme theme) => Invalidate();

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

        Color alertBg = FluentStyle.Blend(theme.SurfaceColor, theme.AccentColor, theme.IsDark ? 0.20 : 0.12);
        FluentStyle.FillRounded(g, alertBg, bounds, radius);
        FluentStyle.DrawRounded(g, theme.AccentColor, bounds, radius, 1.5f);
    }
}
