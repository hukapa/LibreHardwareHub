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

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls;

/// <summary>
/// Shared page metrics so every tool page lines up with the dashboard (KPI strip, toolbar, cards).
/// All values are logical (96 DPI) units.
/// </summary>
public static class PageMetrics
{
    /// <summary>Gap between page blocks; matches the dashboard summary/toolbar gutter.</summary>
    public const int Gap = 12;

    /// <summary>KPI card height; matches <see cref="SummaryPanel" />.</summary>
    public const int CardHeight = 104;

    /// <summary>Minimum KPI card width; matches <see cref="SummaryPanel" />.</summary>
    public const int CardMinWidth = 150;

    /// <summary>Inner padding of content cards.</summary>
    public const int CardPadding = 16;

    /// <summary>Marker placed in <see cref="Control.Tag" /> to right-align a control in a <see cref="ToolbarRow" />.</summary>
    public static readonly object AlignRight = new();

    /// <summary>Marker placed in <see cref="Control.Tag" /> so a control takes the remaining width of a <see cref="ToolbarRow" />.</summary>
    public static readonly object Fill = new();

    /// <summary>Sizes a button to its text (semibold, so it never clips when it becomes the accent button).</summary>
    public static T FitButton<T>(T button, int heightLogical = 32) where T : FluentButton
    {
        int text = TextRenderer.MeasureText(button.Text, FluentStyle.UiFontSemibold).Width;
        button.Size = new Size(text + button.LogicalToDeviceUnits(36), button.LogicalToDeviceUnits(heightLogical));
        return button;
    }

    /// <summary>
    /// Lays out the children of <paramref name="container" /> on a single row, vertically centered:
    /// left-aligned controls in order, <see cref="AlignRight" /> controls from the right edge, and at most
    /// one <see cref="Fill" /> control stretched over the remaining width.
    /// </summary>
    public static void ArrangeRow(Control container, int gapLogical = 8)
    {
        Rectangle area = new(
            container.Padding.Left,
            container.Padding.Top,
            Math.Max(0, container.ClientSize.Width - container.Padding.Horizontal),
            Math.Max(0, container.ClientSize.Height - container.Padding.Vertical));

        int gap = container.LogicalToDeviceUnits(gapLogical);
        List<Control> left = new();
        List<Control> right = new();
        Control fill = null;

        foreach (Control c in container.Controls)
        {
            if (!c.Visible && container.Visible)
                continue;

            // Overriding OnLayout bypasses the default engine, which is what normally applies AutoSize.
            if (c.AutoSize && !ReferenceEquals(c.Tag, Fill))
                c.Size = c.GetPreferredSize(Size.Empty);

            if (ReferenceEquals(c.Tag, AlignRight))
                right.Add(c);
            else if (ReferenceEquals(c.Tag, Fill))
                fill = c;
            else
                left.Add(c);
        }

        int rightX = area.Right;
        foreach (Control c in right)
        {
            rightX -= c.Width;
            c.Location = new Point(rightX, area.Y + ((area.Height - c.Height) / 2));
            rightX -= gap;
        }

        int x = area.X;
        foreach (Control c in left)
        {
            c.Location = new Point(x, area.Y + ((area.Height - c.Height) / 2));
            x += c.Width + gap;
        }

        if (fill != null)
        {
            int width = Math.Max(0, rightX - x);
            fill.SetBounds(x, area.Y + ((area.Height - fill.Height) / 2), width, fill.Height);
        }
    }
}

/// <summary>
/// Vertical stack: lays visible children top-to-bottom at full width with a uniform gap and sizes
/// itself to its content, so it can be docked at the top of a scrolling page.
/// </summary>
public class StackPanel : Panel, IThemedControl
{
    public StackPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    /// <summary>True when the stack sits inside a card (uses the surface color instead of the window background).</summary>
    public bool OnSurface { get; set; }

    public void ApplyTheme(Theme theme)
    {
        BackColor = OnSurface ? theme.SurfaceColor : theme.BackgroundColor;
        foreach (Control child in Controls)
            theme.Apply(child);
    }

    /// <summary>Gap between children in logical units.</summary>
    public int Gap { get; set; } = PageMetrics.Gap;

    protected override void OnLayout(LayoutEventArgs e)
    {
        int gap = LogicalToDeviceUnits(Gap);
        int width = ClientSize.Width - Padding.Horizontal;
        int y = Padding.Top;
        bool any = false;

        // Pass 1: give every child its width first. Children that size themselves from their width
        // (cards, grids, nested stacks) recompute their own height here, before it is read below.
        foreach (Control c in Controls)
        {
            if ((c.Visible || !Visible) && c.Width != Math.Max(0, width))
                c.Width = Math.Max(0, width);
        }

        foreach (Control c in Controls)
        {
            if (!c.Visible && Visible)
                continue;

            if (any)
                y += gap;

            c.SetBounds(Padding.Left, y, Math.Max(0, width), c.Height);
            y += c.Height;
            any = true;
        }

        int height = y + Padding.Bottom;
        if (Height != height)
            Height = height;
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
            PerformLayout();
    }
}

/// <summary>
/// Scrolling viewport for a page with a thin themed scrollbar (same look as the sensor tree's indicator)
/// instead of the native one. The mouse wheel always scrolls the page, so combo boxes and sliders under
/// the cursor are never changed by accident.
/// </summary>
public sealed class ScrollHost : Control, IThemedControl, IMessageFilter
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int GutterLogical = 12;
    private const int BarLogical = 6;

    private readonly Control _content;
    private int _offset;
    private bool _canScroll;
    private bool _hover;
    private bool _dragging;
    private int _dragStartY;
    private int _dragStartOffset;

    public ScrollHost(Control content)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        _content = content;
        _content.Dock = DockStyle.None;
        Controls.Add(_content);
    }

    private int MaxOffset => Math.Max(0, _content.Height - Height);

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        theme.Apply(_content);
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        int gutter = LogicalToDeviceUnits(GutterLogical);

        // Width first (the content computes its height from it), then decide whether a scrollbar is needed.
        _content.Width = _canScroll ? Width - gutter : Width;
        bool canScroll = _content.Height > Height;
        if (canScroll != _canScroll)
        {
            _canScroll = canScroll;
            _content.Width = _canScroll ? Width - gutter : Width;
        }

        _offset = Math.Max(0, Math.Min(_offset, MaxOffset));
        _content.Location = new Point(0, -_offset);
        Invalidate();
    }

    public void ScrollBy(int delta) => ScrollTo(_offset + delta);

    private void ScrollTo(int offset)
    {
        offset = Math.Max(0, Math.Min(offset, MaxOffset));
        if (offset == _offset)
            return;

        _offset = offset;
        _content.Location = new Point(0, -_offset);
        Invalidate();
        Update();
    }

    private Rectangle ThumbBounds()
    {
        if (!_canScroll || _content.Height <= 0)
            return Rectangle.Empty;

        int bar = LogicalToDeviceUnits(_hover || _dragging ? BarLogical : BarLogical - 2);
        int inset = LogicalToDeviceUnits(2);
        int track = Height - (inset * 2);
        int thumb = Math.Max(LogicalToDeviceUnits(32), track * Height / _content.Height);
        int y = inset + (MaxOffset == 0 ? 0 : (track - thumb) * _offset / MaxOffset);
        return new Rectangle(Width - bar - inset, y, bar, thumb);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        e.Graphics.Clear(theme.BackgroundColor);
        Rectangle thumb = ThumbBounds();
        if (thumb.IsEmpty)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color color = _hover || _dragging ? FluentStyle.Blend(theme.BackgroundColor, theme.ForegroundColor, 0.45) : theme.ScrollbarTrack;
        FluentStyle.FillRounded(e.Graphics, color, thumb, thumb.Width / 2f);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            Rectangle thumb = ThumbBounds();
            int track = Height - thumb.Height - LogicalToDeviceUnits(4);
            if (track > 0)
                ScrollTo(_dragStartOffset + ((e.Y - _dragStartY) * MaxOffset / track));
        }
        else if (!_hover)
        {
            _hover = true;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !_canScroll)
            return;

        Rectangle thumb = ThumbBounds();
        if (thumb.Contains(e.Location) || (e.Y >= thumb.Top && e.Y <= thumb.Bottom))
        {
            _dragging = true;
            _dragStartY = e.Y;
            _dragStartOffset = _offset;
            Capture = true;
        }
        else
        {
            // Click on the track: page towards the cursor.
            ScrollBy(e.Y < thumb.Top ? -Height + LogicalToDeviceUnits(48) : Height - LogicalToDeviceUnits(48));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Application.AddMessageFilter(this);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Application.RemoveMessageFilter(this);
        base.OnHandleDestroyed(e);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_MOUSEWHEEL || !_canScroll || !Visible || !IsHandleCreated)
            return false;

        // While a drop-down is open the wheel does nothing (it must not scroll the page under the popup
        // nor change the value of the field).
        if (HasOpenDropDown(_content))
            return true;

        if (!RectangleToScreen(ClientRectangle).Contains(Cursor.Position))
            return false;

        int delta = (short)((long)m.WParam >> 16);
        ScrollBy(-delta * LogicalToDeviceUnits(64) / 120);
        return true;
    }

    private static bool HasOpenDropDown(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (child is FluentComboBox { IsPopupOpen: true } or FluentColorButton { IsPopupOpen: true } || child is ComboBox { DroppedDown: true } || HasOpenDropDown(child))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Responsive grid: as many equal-width columns as fit <see cref="MinColumnWidth" />; each row is as tall
/// as its tallest child. Sizes itself to its content.
/// </summary>
public sealed class WrapGrid : StackPanel
{
    /// <summary>Minimum column width in logical units.</summary>
    public int MinColumnWidth { get; set; } = 320;

    protected override void OnLayout(LayoutEventArgs e)
    {
        List<Control> items = new();
        foreach (Control c in Controls)
        {
            if (c.Visible || !Visible)
                items.Add(c);
        }

        int gap = LogicalToDeviceUnits(Gap);
        int width = ClientSize.Width - Padding.Horizontal;
        int minWidth = LogicalToDeviceUnits(MinColumnWidth);
        // Column count depends on the width only, so every group on a page shares the same card width
        // and a lone card does not stretch across the page.
        int columns = Math.Max(1, (width + gap) / (minWidth + gap));
        int columnWidth = Math.Max(0, (width - (gap * (columns - 1))) / columns);

        // Pass 1: set the column width so each card recomputes its own height before the rows are measured.
        foreach (Control item in items)
        {
            if (item.Width != columnWidth)
                item.Width = columnWidth;
        }

        int y = Padding.Top;
        for (int i = 0; i < items.Count; i += columns)
        {
            int rowHeight = 0;
            for (int j = i; j < Math.Min(items.Count, i + columns); j++)
                rowHeight = Math.Max(rowHeight, items[j].Height);

            for (int j = i; j < Math.Min(items.Count, i + columns); j++)
            {
                int column = j - i;
                items[j].SetBounds(Padding.Left + (column * (columnWidth + gap)), y, columnWidth, items[j].Height);
            }

            y += rowHeight + gap;
        }

        int height = (items.Count > 0 ? y - gap : y) + Padding.Bottom;
        if (Height != height)
            Height = height;
    }
}

/// <summary>
/// Row of KPI cards laid out exactly like the dashboard <see cref="SummaryPanel" />: equal widths,
/// 12px gutter, and cards that no longer fit are hidden instead of being squeezed.
/// </summary>
public sealed class MetricStrip : Panel, IThemedControl
{
    private readonly List<FluentMetricCard> _cards = new();

    public MetricStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = LogicalToDeviceUnits(PageMetrics.CardHeight);
    }

    public void AddCard(FluentMetricCard card)
    {
        _cards.Add(card);
        Controls.Add(card);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_cards.Count == 0)
            return;

        int gap = LogicalToDeviceUnits(PageMetrics.Gap);
        int height = LogicalToDeviceUnits(PageMetrics.CardHeight);
        int minWidth = LogicalToDeviceUnits(PageMetrics.CardMinWidth);
        int count = Math.Max(1, Math.Min(_cards.Count, (ClientSize.Width + gap) / (minWidth + gap)));
        int width = (ClientSize.Width - (gap * (count - 1))) / count;

        for (int i = 0; i < _cards.Count; i++)
        {
            bool fits = i < count;
            _cards[i].Visible = fits;
            if (fits)
                _cards[i].SetBounds(i * (width + gap), 0, width, height);
        }

        if (Height != height)
            Height = height;
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        foreach (FluentMetricCard card in _cards)
            card.ApplyTheme(theme);
    }
}

/// <summary>
/// Single-row toolbar (same role as the dashboard search row): left controls in order, controls tagged
/// <see cref="PageMetrics.AlignRight" /> on the right, everything vertically centered.
/// </summary>
public sealed class ToolbarRow : Panel, IThemedControl
{
    /// <summary>True when the row sits inside a card (uses the surface color instead of the window background).</summary>
    public bool OnSurface { get; set; }

    public void ApplyTheme(Theme theme)
    {
        BackColor = OnSurface ? theme.SurfaceColor : theme.BackgroundColor;
        foreach (Control child in Controls)
        {
            theme.Apply(child);
            if (child is Label or CheckBox)
                child.BackColor = BackColor;
        }
    }

    public ToolbarRow()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = LogicalToDeviceUnits(32);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        PageMetrics.ArrangeRow(this);

        // Children can grow after the parent sized this row (e.g. DPI scaling of the buttons); ask a hosting
        // SettingRow to make room instead of clipping the last control.
        if (Parent is SettingRow && GetPreferredSize(Size.Empty).Width > Width)
            Parent.PerformLayout();
    }

    /// <summary>Natural width: all children side by side with the row gap.</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = 0;
        int count = 0;
        foreach (Control c in Controls)
        {
            if (!c.Visible && Visible)
                continue;

            width += c.AutoSize ? c.GetPreferredSize(Size.Empty).Width : c.Width;
            count++;
        }

        return new Size(width + (Math.Max(0, count - 1) * LogicalToDeviceUnits(8)) + Padding.Horizontal, Height);
    }
}

/// <summary>Small caption shown above a group of cards (e.g. one fan controller).</summary>
public sealed class GroupHeader : Control, IThemedControl
{
    private string _detail = string.Empty;
    private Image _icon;
    private bool _collapsible;
    private bool _collapsed;
    private bool _hover;

    public GroupHeader()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = LogicalToDeviceUnits(26);
    }

    /// <summary>Raised when a collapsible header is clicked; <see cref="Collapsed" /> already holds the new state.</summary>
    public event EventHandler CollapsedChanged;

    public string Detail
    {
        get => _detail;
        set { _detail = value ?? string.Empty; Invalidate(); }
    }

    public Image Icon
    {
        get => _icon;
        set { _icon = value; Invalidate(); }
    }

    /// <summary>When true a chevron is shown and a click toggles <see cref="Collapsed" />.</summary>
    public bool Collapsible
    {
        get => _collapsible;
        set { _collapsible = value; Cursor = value ? Cursors.Hand : Cursors.Default; Invalidate(); }
    }

    public bool Collapsed
    {
        get => _collapsed;
        set { _collapsed = value; Invalidate(); }
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_collapsible && e.Button == MouseButtons.Left)
        {
            _collapsed = !_collapsed;
            Invalidate();
            CollapsedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(theme.BackgroundColor);

        int x = LogicalToDeviceUnits(2);
        if (_collapsible)
        {
            // Chevron: right when collapsed, down when open.
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float size = LogicalToDeviceUnits(4);
            float cx = x + size;
            float cy = Height / 2f;
            using Pen pen = new(_hover ? theme.ForegroundColor : theme.MutedForegroundColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            if (_collapsed)
                g.DrawLines(pen, new[] { new PointF(cx - (size / 2), cy - size), new PointF(cx + (size / 2), cy), new PointF(cx - (size / 2), cy + size) });
            else
                g.DrawLines(pen, new[] { new PointF(cx - size, cy - (size / 2)), new PointF(cx, cy + (size / 2)), new PointF(cx + size, cy - (size / 2)) });

            x += LogicalToDeviceUnits(16);
        }

        if (_icon != null)
        {
            int icon = LogicalToDeviceUnits(16);
            g.DrawImage(_icon, new Rectangle(x, (Height - icon) / 2, icon, icon));
            x += icon + LogicalToDeviceUnits(8);
        }

        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        Size titleSize = TextRenderer.MeasureText(g, Text, FluentStyle.UiFontSemibold, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, Text, FluentStyle.UiFontSemibold, new Rectangle(x, 0, Width - x, Height), theme.ForegroundColor, flags);

        if (_detail.Length > 0)
        {
            int dx = x + titleSize.Width + LogicalToDeviceUnits(6);
            TextRenderer.DrawText(g, _detail, FluentStyle.UiFont, new Rectangle(dx, 0, Math.Max(0, Width - dx), Height), theme.MutedForegroundColor, flags);
        }
    }
}

/// <summary>
/// Windows 11 settings-style row used inside a card: title and description on the left, an editor control
/// in the middle and the current value (with its default as a hint) on the right. Rows after the first draw
/// a hairline divider on top.
/// </summary>
public sealed class SettingRow : Panel, IThemedControl
{
    private const int RowLogicalHeight = 60;
    private const int ValueLogicalWidth = 132;
    private const int EditorMaxLogicalWidth = 420;

    private readonly Control _editor;
    private string _title;
    private string _description;
    private string _value = string.Empty;
    private string _hint = string.Empty;
    private Font _detailFont;

    public SettingRow(string title, string description, Control editor, bool showValue = true)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        _title = title ?? string.Empty;
        _description = description ?? string.Empty;
        _editor = editor;
        ShowValue = showValue;
        Height = LogicalToDeviceUnits(RowLogicalHeight);
        if (_editor != null)
            Controls.Add(_editor);
    }

    public bool ShowValue { get; }

    /// <summary>Draws the divider above the row; disable for the first row of a card.</summary>
    public bool ShowDivider { get; set; } = true;

    private Font DetailFont => _detailFont ??= FluentStyle.CreateUiFont(FontStyle.Regular, 8.25f);

    public void SetValue(string value, string hint = "")
    {
        value ??= string.Empty;
        hint ??= string.Empty;
        if (value == _value && hint == _hint)
            return;

        _value = value;
        _hint = hint;
        Invalidate();
    }

    public string Description
    {
        get => _description;
        set { _description = value ?? string.Empty; Invalidate(); }
    }

    private int TextColumnWidth => Math.Max(LogicalToDeviceUnits(180), Math.Min(LogicalToDeviceUnits(320), (int)(Width * 0.38)));

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_editor == null)
            return;

        // Windows 11 settings pattern: the editor column has a maximum width and sits next to the value
        // column on the right; the value column is always reserved so every editor shares the same left edge.
        int gap = LogicalToDeviceUnits(16);
        int valueColumn = LogicalToDeviceUnits(ValueLogicalWidth) + gap;
        int available = Width - TextColumnWidth - gap - valueColumn;
        int column = Math.Max(LogicalToDeviceUnits(80), Math.Min(LogicalToDeviceUnits(EditorMaxLogicalWidth), available));
        if (_editor is ToolbarRow)
            column = Math.Max(column, _editor.GetPreferredSize(Size.Empty).Width);
        int x = Width - valueColumn - column;

        int width = _editor is ComboBox ? Math.Min(column, LogicalToDeviceUnits(260)) : column;
        _editor.SetBounds(x, (Height - _editor.Height) / 2, width, _editor.Height);
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        if (_editor is IThemedControl themed)
            themed.ApplyTheme(theme);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(theme.SurfaceColor);

        if (ShowDivider)
        {
            using Pen pen = new(theme.LineColor);
            g.DrawLine(pen, 0, 0, Width, 0);
        }

        bool enabled = Enabled && (_editor?.Enabled ?? true);
        Color fore = enabled ? theme.ForegroundColor : theme.MutedForegroundColor;
        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        int textWidth = TextColumnWidth;
        int titleHeight = FluentStyle.UiFont.Height;
        int detailHeight = DetailFont.Height;
        bool hasDescription = _description.Length > 0;
        int blockHeight = hasDescription ? titleHeight + LogicalToDeviceUnits(2) + detailHeight : titleHeight;
        int y = (Height - blockHeight) / 2;

        TextRenderer.DrawText(g, _title, FluentStyle.UiFont, new Rectangle(0, y, textWidth, titleHeight), fore, flags);
        if (hasDescription)
            TextRenderer.DrawText(g, _description, DetailFont, new Rectangle(0, y + titleHeight + LogicalToDeviceUnits(2), textWidth, detailHeight), theme.MutedForegroundColor, flags);

        if (!ShowValue)
            return;

        int valueWidth = LogicalToDeviceUnits(ValueLogicalWidth);
        TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        bool hasHint = _hint.Length > 0;
        int valueBlock = hasHint ? titleHeight + LogicalToDeviceUnits(2) + detailHeight : titleHeight;
        int vy = (Height - valueBlock) / 2;
        TextRenderer.DrawText(g, _value, FluentStyle.UiFontSemibold, new Rectangle(Width - valueWidth, vy, valueWidth, titleHeight), fore, right);
        if (hasHint)
            TextRenderer.DrawText(g, _hint, DetailFont, new Rectangle(Width - valueWidth, vy + titleHeight + LogicalToDeviceUnits(2), valueWidth, detailHeight), theme.MutedForegroundColor, right);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _detailFont?.Dispose();

        base.Dispose(disposing);
    }
}
