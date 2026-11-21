// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>
/// Base of the Bento hardware cards. A card has a header and a fixed-height summary (drawn by the subclass).
/// Clicking it expands it: the card takes a full row of the grid and shows every sensor, grouped by type,
/// next to the summary (or below it when the window is narrow). The grid asks the card for its height
/// (<see cref="IWrapGridItem" />), so cards of one row always end up with the same height.
/// </summary>
public abstract partial class BentoCardBase : Control, IThemedControl, IWrapGridItem
{
    private const int HeaderLogical = 58;
    private const int PadLogical = 18;
    private const int FooterLogical = 22;
    private const int MinSummaryLogical = 176;
    private const int SummaryPaneLogical = 372;
    private const int SideBySideMinLogical = 760;
    private const int DetailColumnLogical = 236;
    private const int DetailRowLogical = 22;
    private const int DetailCaptionLogical = 24;
    private const int DetailGroupGapLogical = 10;
    private const int DetailColumnGapLogical = 12;
    private const int SmallGroupRows = 6;

    private sealed class DetailGroup
    {
        public string Title;
        public readonly List<(string Name, string Value)> Rows = new();
    }

    private readonly List<DetailGroup> _details = new();
    private int _detailGroups;
    private int _layoutHeight;
    private bool _isHovered;
    private bool _isExpanded;

    protected BentoCardBase()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Size = new Size(LogicalToDeviceUnits(400), LogicalToDeviceUnits(HeaderLogical + MinSummaryLogical + PadLogical + FooterLogical));
        Cursor = Cursors.Hand;
    }

    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public Image Icon { get; set; }
    public string BadgeText { get; set; } = string.Empty;
    public Color BadgeColor { get; set; } = Color.Empty;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;
            if (!value)
                _detailGroups = 0;

            ExpansionChanged?.Invoke(this, EventArgs.Empty);
            Parent?.PerformLayout();
            Invalidate();
        }
    }

    /// <summary>Raised when the card was expanded or collapsed; the owner refreshes its sensors and the grid.</summary>
    public event EventHandler ExpansionChanged;

    /// <summary>Height of the summary area in logical units (without header and paddings).</summary>
    protected abstract int SummaryHeight { get; }

    /// <summary>Draws the always-visible part of the card inside <paramref name="area" />.</summary>
    protected abstract void DrawSummary(Graphics g, Theme theme, Rectangle area);

    /// <summary>Lets a card leave sensors out of the detailed list (for example the ones its summary already plots).</summary>
    protected virtual bool IncludeDetail(ISensor sensor) => true;

    // ---- Detailed sensors (only collected while the card is expanded) ----

    protected void BeginDetails() => _detailGroups = 0;

    /// <summary>
    /// Adds every sensor of <paramref name="hardware" /> (and sub-hardware) to the detailed list: grouped by sensor
    /// type, or all under <paramref name="groupTitle" /> when one is given.
    /// </summary>
    protected void AddDetails(IHardware hardware, string groupTitle = null)
    {
        if (!_isExpanded || hardware == null)
            return;

        int first = _detailGroups;
        AddDetailSensors(hardware, groupTitle, first);
    }

    private void AddDetailSensors(IHardware hardware, string groupTitle, int first)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue || !IncludeDetail(sensor))
                continue;

            // One group per sensor type, or one per device when several devices share the card.
            string title = groupTitle ?? SensorLookup.GroupName(sensor.SensorType);

            DetailGroup group = null;
            for (int i = first; i < _detailGroups; i++)
            {
                if (_details[i].Title == title)
                {
                    group = _details[i];
                    break;
                }
            }

            if (group == null)
            {
                // Group objects (and their row lists) are reused between updates.
                if (_detailGroups == _details.Count)
                    _details.Add(new DetailGroup());

                group = _details[_detailGroups++];
                group.Title = title;
                group.Rows.Clear();
            }

            group.Rows.Add((sensor.Name, SensorLookup.Format(sensor)));
        }

        foreach (IHardware sub in hardware.SubHardware)
            AddDetailSensors(sub, groupTitle, first);
    }

    /// <summary>Call at the end of a sensor update: re-runs the grid layout if the card needs another height.</summary>
    protected void EndUpdate()
    {
        if (Parent != null && Measure(Width) != _layoutHeight)
            Parent.PerformLayout();

        Invalidate();
    }

    // ---- Geometry ----

    public bool FullRow => _isExpanded;

    private bool SideBySide(int width) => width >= LogicalToDeviceUnits(SideBySideMinLogical);

    private int SummaryPixels => LogicalToDeviceUnits(Math.Max(MinSummaryLogical, SummaryHeight));

    private int DetailsWidth(int width)
    {
        int pad = LogicalToDeviceUnits(PadLogical);
        return SideBySide(width) ? width - LogicalToDeviceUnits(SummaryPaneLogical) - (pad * 3) : width - (pad * 2);
    }

    private int DetailsHeight(int detailsWidth) => LayoutDetails(detailsWidth, null);

    /// <summary>
    /// Arranges the detail groups inside a strip <paramref name="width" /> pixels wide and returns its height.
    /// Small groups sit side by side, one column each; a large group takes the full width and spreads its rows
    /// over all the columns. Measuring and painting both go through here, so they can never disagree.
    /// </summary>
    private int LayoutDetails(int width, Action<DetailGroup, Rectangle, int> visit)
    {
        int columns = Math.Max(1, width / LogicalToDeviceUnits(DetailColumnLogical));
        int gap = LogicalToDeviceUnits(DetailColumnGapLogical);
        int columnWidth = (width - (gap * (columns - 1))) / columns;
        int rowHeight = LogicalToDeviceUnits(DetailRowLogical);
        int chrome = LogicalToDeviceUnits(DetailCaptionLogical) + LogicalToDeviceUnits(DetailGroupGapLogical);

        int y = 0;
        int shelfColumn = 0;
        int shelfHeight = 0;
        for (int i = 0; i < _detailGroups; i++)
        {
            DetailGroup group = _details[i];
            bool small = columns > 1 && group.Rows.Count <= SmallGroupRows;
            if (!small || shelfColumn == columns)
            {
                y += shelfHeight;
                shelfColumn = shelfHeight = 0;
            }

            if (small)
            {
                int height = chrome + (group.Rows.Count * rowHeight);
                visit?.Invoke(group, new Rectangle(shelfColumn * (columnWidth + gap), y, columnWidth, height), 1);
                shelfColumn++;
                shelfHeight = Math.Max(shelfHeight, height);
            }
            else
            {
                int height = chrome + ((group.Rows.Count + columns - 1) / columns * rowHeight);
                visit?.Invoke(group, new Rectangle(0, y, width, height), columns);
                y += height;
            }
        }

        return y + shelfHeight;
    }

    public int HeightForWidth(int width) => _layoutHeight = Measure(width);

    private int Measure(int width)
    {
        int chrome = LogicalToDeviceUnits(HeaderLogical) + LogicalToDeviceUnits(PadLogical) + LogicalToDeviceUnits(FooterLogical);
        if (!_isExpanded || _detailGroups == 0)
            return chrome + SummaryPixels;

        int details = DetailsHeight(DetailsWidth(width));
        return SideBySide(width)
            ? chrome + Math.Max(SummaryPixels, details)
            : chrome + SummaryPixels + LogicalToDeviceUnits(PadLogical) + details;
    }

    // ---- Input ----

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

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left)
            IsExpanded = !IsExpanded;
    }

    // ---- Painting ----

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 1 || Height <= 1)
            return;

        Graphics g = e.Graphics;
        Theme theme = Theme.Current;
        g.Clear(Parent?.BackColor ?? theme.BackgroundColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        RectangleF bounds = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        float radius = LogicalToDeviceUnits(10);
        Color stroke = _isHovered || _isExpanded ? FluentStyle.Blend(theme.SurfaceStrokeColor, theme.AccentColor, _isHovered ? 0.65 : 0.35) : theme.SurfaceStrokeColor;
        FluentStyle.FillRounded(g, theme.SurfaceColor, bounds, radius);
        FluentStyle.DrawRounded(g, stroke, bounds, radius, _isHovered ? 1.5f : 1f);

        int pad = LogicalToDeviceUnits(PadLogical);
        int footer = LogicalToDeviceUnits(FooterLogical);
        int top = LogicalToDeviceUnits(HeaderLogical);
        DrawHeader(g, theme, pad);
        DrawFooter(g, theme, pad, footer);

        bool sideBySide = _isExpanded && _detailGroups > 0 && SideBySide(Width);
        int summaryWidth = sideBySide ? LogicalToDeviceUnits(SummaryPaneLogical) : Width - (pad * 2);
        DrawSummary(g, theme, new Rectangle(pad, top, summaryWidth, SummaryPixels));

        if (!_isExpanded || _detailGroups == 0)
            return;

        using Pen divider = new(theme.LineColor);
        int bottom = Height - pad - footer;
        if (sideBySide)
        {
            int x = pad + summaryWidth + pad;
            g.DrawLine(divider, x - (pad / 2), top, x - (pad / 2), bottom);
            DrawDetails(g, theme, new Rectangle(x, top, Width - x - pad, Math.Max(0, bottom - top)));
        }
        else
        {
            int y = top + SummaryPixels + pad;
            g.DrawLine(divider, pad, y - (pad / 2), Width - pad, y - (pad / 2));
            DrawDetails(g, theme, new Rectangle(pad, y, Width - (pad * 2), Math.Max(0, bottom - y)));
        }
    }

    /// <summary>Minimal footer cue: "Detalhes" / "Recolher" with a vector arrow, accent only while the card is hovered.</summary>
    private void DrawFooter(Graphics g, Theme theme, int pad, int footer)
    {
        Color color = _isHovered ? theme.AccentColor : theme.MutedForegroundColor;
        string text = _isExpanded ? "Recolher" : "Detalhes";
        int textWidth = TextRenderer.MeasureText(text, FluentStyle.UiFontSmall, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
        int arrow = LogicalToDeviceUnits(14);
        Rectangle bounds = new(Width - pad - textWidth - arrow, Height - pad - footer + LogicalToDeviceUnits(6), textWidth + arrow, footer);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        TextRenderer.DrawText(g, text, FluentStyle.UiFontSmall, new Rectangle(bounds.X, bounds.Y, textWidth, footer), color,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

        float size = LogicalToDeviceUnits(3);
        float cx = bounds.Right - (arrow / 2f) + LogicalToDeviceUnits(2);
        float cy = bounds.Y + (footer / 2f);
        float tip = _isExpanded ? -size / 2 : size / 2;
        using Pen chevron = new(color, LogicalToDeviceUnits(1) * 1.25f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(chevron, new[] { new PointF(cx - size, cy - tip), new PointF(cx, cy + tip), new PointF(cx + size, cy - tip) });
    }

    private void DrawHeader(Graphics g, Theme theme, int pad)
    {
        int iconBox = LogicalToDeviceUnits(28);
        int x = pad;

        if (Icon != null)
        {
            // Icon on a barely visible tile (about 6% of the foreground).
            Rectangle iconRect = new(x, pad, iconBox, iconBox);
            FluentStyle.FillRounded(g, Color.FromArgb(15, theme.ForegroundColor), iconRect, LogicalToDeviceUnits(8));
            int inset = LogicalToDeviceUnits(6);
            g.DrawImage(Icon, x + inset, pad + inset, iconBox - (inset * 2), iconBox - (inset * 2));
            x += iconBox + LogicalToDeviceUnits(12);
        }

        int right = Width - pad;
        if (!string.IsNullOrEmpty(BadgeText))
        {
            // Soft pill: state color at about 14% over the surface, hairline border at 25%, crisp text in the state color.
            int badgeHeight = LogicalToDeviceUnits(22);
            int badgeWidth = TextRenderer.MeasureText(BadgeText, FluentStyle.UiFontSmallBold).Width + LogicalToDeviceUnits(18);
            Rectangle badge = new(right - badgeWidth, pad + ((iconBox - badgeHeight) / 2), badgeWidth, badgeHeight);
            Color color = BadgeColor.IsEmpty ? theme.AccentColor : BadgeColor;
            FluentStyle.FillRounded(g, FluentStyle.Blend(theme.SurfaceColor, color, 0.14), badge, badgeHeight / 2f);
            FluentStyle.DrawRounded(g, FluentStyle.Blend(theme.SurfaceColor, color, 0.25), badge, badgeHeight / 2f);
            TextRenderer.DrawText(g, BadgeText, FluentStyle.UiFontSmallBold, badge, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            right = badge.Left - LogicalToDeviceUnits(10);
        }

        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        int textWidth = Math.Max(10, right - x);
        TextRenderer.DrawText(g, Title, FluentStyle.UiFontSemibold, new Rectangle(x, pad - LogicalToDeviceUnits(1), textWidth, LogicalToDeviceUnits(16)), theme.ForegroundColor, flags);
        TextRenderer.DrawText(g, Subtitle, FluentStyle.UiFontSmall, new Rectangle(x, pad + LogicalToDeviceUnits(15), textWidth, LogicalToDeviceUnits(14)), theme.MutedForegroundColor, flags);
    }

    private void DrawDetails(Graphics g, Theme theme, Rectangle area)
    {
        using SolidBrush stripe = new(theme.SubtleSurfaceColor);
        LayoutDetails(area.Width, (group, bounds, columns) =>
        {
            bounds.Offset(area.Location);
            DrawDetailGroup(g, theme, stripe, group, bounds, columns);
        });
    }

    private void DrawDetailGroup(Graphics g, Theme theme, Brush stripe, DetailGroup group, Rectangle bounds, int columns)
    {
        int gap = LogicalToDeviceUnits(DetailColumnGapLogical);
        int columnWidth = (bounds.Width - (gap * (columns - 1))) / columns;
        int rowHeight = LogicalToDeviceUnits(DetailRowLogical);
        int captionHeight = LogicalToDeviceUnits(DetailCaptionLogical);

        TextRenderer.DrawText(g, group.Title.ToUpperInvariant(), FluentStyle.UiFontSmallBold, new Rectangle(bounds.X, bounds.Y, bounds.Width, captionHeight),
            theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        // Column-major: reading down a column keeps related sensors (Core #1, #2, ...) together.
        int rows = (group.Rows.Count + columns - 1) / columns;
        for (int r = 0; r < group.Rows.Count; r++)
        {
            int row = r % rows;
            Rectangle cell = new(bounds.X + (r / rows * (columnWidth + gap)), bounds.Y + captionHeight + (row * rowHeight), columnWidth, rowHeight);
            if (row % 2 == 0)
                g.FillRectangle(stripe, cell);

            (string name, string value) = group.Rows[r];
            int valueWidth = TextRenderer.MeasureText(value, FluentStyle.MonoFontSmall).Width;
            TextRenderer.DrawText(g, value, FluentStyle.MonoFontSmall, new Rectangle(cell.Right - valueWidth - 4, cell.Y, valueWidth, rowHeight), theme.ForegroundColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, name, FluentStyle.UiFontSmall, new Rectangle(cell.X + 4, cell.Y, Math.Max(10, cell.Width - valueWidth - 14), rowHeight), theme.MutedForegroundColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    public virtual void ApplyTheme(Theme theme) => Invalidate();
}
