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
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Updates;

/// <summary>
/// Draws release notes (version, date, headings and bullets) as plain themed text. It sizes its own height from
/// the width it is given, so a card or a scrolling host can place it without a nested scrollbar.
/// </summary>
public sealed class ReleaseNotesView : Control, IThemedControl
{
    private const TextFormatFlags WrapFlags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;

    private enum RowKind
    {
        Version,
        Heading,
        Bullet,
        Message
    }

    private readonly record struct Row(RowKind Kind, string Text, string Detail, int Y, int Height);

    private readonly List<Row> _rows = new();
    private List<ReleaseNoteEntry> _entries = new();
    private string _emptyText = "Sem notas de versão disponíveis.";

    public ReleaseNotesView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = LogicalToDeviceUnits(40);
    }

    /// <summary>True inside a card (surface color), false directly on the page background.</summary>
    public bool OnSurface { get; set; } = true;

    public void SetEntries(IEnumerable<ReleaseNoteEntry> entries, string emptyText = null)
    {
        _entries = new List<ReleaseNoteEntry>(entries ?? Array.Empty<ReleaseNoteEntry>());
        if (emptyText != null)
            _emptyText = emptyText;

        Relayout();
        Invalidate();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = OnSurface ? theme.SurfaceColor : theme.BackgroundColor;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    private int Measure(string text, Font font, int width) =>
        TextRenderer.MeasureText(text, font, new Size(Math.Max(1, width), 0), WrapFlags).Height;

    private void Relayout()
    {
        _rows.Clear();
        if (Width <= 0)
            return;

        int indent = LogicalToDeviceUnits(16);
        int y = 0;

        if (_entries.Count == 0)
        {
            int height = Measure(_emptyText, FluentStyle.UiFont, Width);
            _rows.Add(new Row(RowKind.Message, _emptyText, null, 0, height));
            y = height;
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            ReleaseNoteEntry entry = _entries[i];
            if (i > 0)
                y += LogicalToDeviceUnits(16);

            string detail = entry.Date.HasValue ? entry.Date.Value.ToString("dd/MM/yyyy") : null;
            int headerHeight = FluentStyle.UiFontSemibold.Height + LogicalToDeviceUnits(4);
            _rows.Add(new Row(RowKind.Version, entry.Version, detail, y, headerHeight));
            y += headerHeight + LogicalToDeviceUnits(2);

            foreach (NoteLine line in entry.Lines)
            {
                if (line.IsHeading)
                {
                    int height = FluentStyle.UiFontSmallBold.Height + LogicalToDeviceUnits(6);
                    _rows.Add(new Row(RowKind.Heading, line.Text, null, y + LogicalToDeviceUnits(4), height));
                    y += height + LogicalToDeviceUnits(4);
                }
                else
                {
                    int height = Measure(line.Text, FluentStyle.UiFont, Width - indent) + LogicalToDeviceUnits(4);
                    _rows.Add(new Row(RowKind.Bullet, line.Text, null, y, height));
                    y += height;
                }
            }
        }

        int total = Math.Max(LogicalToDeviceUnits(24), y);
        if (Height != total)
            Height = total;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(OnSurface ? theme.SurfaceColor : theme.BackgroundColor);
        if (Width <= 2 || Height <= 2)
            return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        int indent = LogicalToDeviceUnits(16);
        int dot = LogicalToDeviceUnits(4);
        TextFormatFlags single = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        foreach (Row row in _rows)
        {
            switch (row.Kind)
            {
                case RowKind.Version:
                    TextRenderer.DrawText(g, row.Text, FluentStyle.UiFontSemibold, new Rectangle(0, row.Y, Width, row.Height), theme.ForegroundColor, single);
                    if (row.Detail != null)
                    {
                        int textWidth = TextRenderer.MeasureText(row.Text, FluentStyle.UiFontSemibold, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
                        int x = textWidth + LogicalToDeviceUnits(10);
                        TextRenderer.DrawText(g, row.Detail, FluentStyle.UiFontSmall, new Rectangle(x, row.Y, Math.Max(0, Width - x), row.Height), theme.MutedForegroundColor, single);
                    }

                    break;
                case RowKind.Heading:
                    TextRenderer.DrawText(g, row.Text.ToUpperInvariant(), FluentStyle.UiFontSmallBold, new Rectangle(0, row.Y, Width, row.Height), theme.MutedForegroundColor, single);
                    break;
                case RowKind.Bullet:
                    using (SolidBrush brush = new(theme.AccentColor))
                        g.FillEllipse(brush, 2, row.Y + ((FluentStyle.UiFont.Height - dot) / 2) + LogicalToDeviceUnits(1), dot, dot);
                    TextRenderer.DrawText(g, row.Text, FluentStyle.UiFont, new Rectangle(indent, row.Y, Math.Max(1, Width - indent), row.Height), theme.ForegroundColor, WrapFlags);
                    break;
                default:
                    TextRenderer.DrawText(g, row.Text, FluentStyle.UiFont, new Rectangle(0, row.Y, Width, row.Height), theme.MutedForegroundColor, WrapFlags);
                    break;
            }
        }
    }
}
