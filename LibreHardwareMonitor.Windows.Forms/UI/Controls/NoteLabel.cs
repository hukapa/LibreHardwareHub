// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls;

/// <summary>
/// Quiet, word-wrapped note (muted color) that sizes its own height from the width it is given, so it can
/// sit in a <see cref="StackPanel" /> inside a card.
/// </summary>
public sealed class NoteLabel : Control, IThemedControl
{
    private const TextFormatFlags Flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;

    public NoteLabel(string text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Text = text ?? string.Empty;
        Height = LogicalToDeviceUnits(48);
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        FitHeight();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        FitHeight();
        Invalidate();
    }

    private Rectangle TextBounds => new(0, LogicalToDeviceUnits(10), Math.Max(0, Width), Math.Max(0, Height - LogicalToDeviceUnits(10)));

    private void FitHeight()
    {
        if (Width <= 0)
            return;

        int text = TextRenderer.MeasureText(Text, FluentStyle.UiFont, new Size(Width, 0), Flags).Height;
        int height = text + LogicalToDeviceUnits(10) + LogicalToDeviceUnits(14);
        if (Height != height)
            Height = height;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        e.Graphics.Clear(theme.SurfaceColor);

        Rectangle bounds = TextBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        TextRenderer.DrawText(e.Graphics, Text, FluentStyle.UiFont, bounds, theme.MutedForegroundColor, Flags);
    }
}
