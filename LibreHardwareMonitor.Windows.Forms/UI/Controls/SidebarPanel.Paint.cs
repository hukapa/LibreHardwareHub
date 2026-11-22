using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls
{
    /// <summary>Painting of the navigation pane: compact toggle, section captions, entries and scroll indicator.</summary>
    public sealed partial class SidebarPanel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme theme = Theme.Current;
            Graphics g = e.Graphics;
            g.Clear(theme.SidebarColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // The scrolling list is clipped strictly between the top bar and the pinned footer: nothing can be seen
            // (or clicked) behind them while scrolling.
            int view = ViewHeight;
            if (view > 0)
            {
                g.SetClip(new Rectangle(0, TopHeight, Width, view));
                for (int i = 0; i < _rows.Count; i++)
                {
                    Row row = _rows[i];
                    Rectangle bounds = RowBounds(row);
                    if (bounds.Bottom < TopHeight || bounds.Top > TopHeight + view)
                        continue;

                    if (row.Item == null)
                        DrawHeader(g, theme, row, bounds, i == _hoverRow);
                    else
                        DrawItem(g, theme, row.Item, bounds, row.Item == _selected, i == _hoverRow);
                }

                g.ResetClip();
            }

            DrawToggle(g, theme);
            DrawFooter(g, theme);

            // Thin scroll indicator, only while the list overflows.
            if (MaxScroll > 0 && view > 0)
            {
                int track = Math.Max(1, view - LogicalToDeviceUnits(8));
                int thumb = Math.Max(LogicalToDeviceUnits(24), track * view / _contentHeight);
                int y = TopHeight + LogicalToDeviceUnits(4) + ((track - thumb) * _scrollOffset / MaxScroll);
                FluentStyle.FillRounded(g, theme.ScrollbarTrack, new RectangleF(Width - LogicalToDeviceUnits(4), y, LogicalToDeviceUnits(3), thumb), LogicalToDeviceUnits(3) / 2f);
            }
        }

        /// <summary>The pinned entries (Definicoes): always visible, above them a 1 px hairline.</summary>
        private void DrawFooter(Graphics g, Theme theme)
        {
            if (_footer.Count == 0)
                return;

            int top = Height - FooterHeight;
            using (SolidBrush background = new(theme.SidebarColor))
                g.FillRectangle(background, 0, top, Width, FooterHeight);

            using (Pen line = new(theme.LineColor, 1f))
                g.DrawLine(line, LogicalToDeviceUnits(6), top, Width - LogicalToDeviceUnits(6), top);

            for (int i = 0; i < _footer.Count; i++)
                DrawItem(g, theme, _footer[i], FooterItemBounds(i), _footer[i] == _selected, _hoverRow == FooterHit - i);
        }

        private void DrawToggle(Graphics g, Theme theme)
        {
            Rectangle bounds = ToggleBounds;
            if (_hoverRow == -2)
                FluentStyle.FillRounded(g, FluentStyle.Blend(theme.SidebarColor, theme.ForegroundColor, theme.IsDark ? 0.07 : 0.05), bounds, LogicalToDeviceUnits(4));

            // Three lines: the conventional "navigation" glyph.
            using Pen pen = new(theme.ForegroundColor, LogicalToDeviceUnits(1) * 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float half = LogicalToDeviceUnits(7);
            float cx = bounds.Left + (bounds.Width / 2f);
            float cy = bounds.Top + (bounds.Height / 2f);
            for (int i = -1; i <= 1; i++)
                g.DrawLine(pen, cx - half, cy + (i * LogicalToDeviceUnits(5)), cx + half, cy + (i * LogicalToDeviceUnits(5)));
        }

        private void DrawHeader(Graphics g, Theme theme, Row row, Rectangle bounds, bool hover)
        {
            if (_compact)
            {
                using Pen line = new(theme.LineColor);
                int y = bounds.Top + (bounds.Height / 2);
                g.DrawLine(line, bounds.Left + LogicalToDeviceUnits(8), y, bounds.Right - LogicalToDeviceUnits(8), y);
                return;
            }

            // A collapsible caption reacts to the mouse: a subtle rounded wash (6 px radius, like the entries).
            if (hover)
                FluentStyle.FillRounded(g, FluentStyle.Blend(theme.SidebarColor, theme.ForegroundColor, theme.IsDark ? 0.05 : 0.035), bounds, LogicalToDeviceUnits(6));

            bool collapsed = _collapsed.Contains(row.Section);
            Color tone = hover ? theme.ForegroundColor : theme.MutedForegroundColor;

            // Right edge: chevron, and left of it the number of entries hidden by a collapsed section.
            int chevronSpace = LogicalToDeviceUnits(30);
            int countWidth = 0;
            if (row.HiddenText != null)
            {
                countWidth = TextRenderer.MeasureText(row.HiddenText, FluentStyle.UiFontSmall, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width + LogicalToDeviceUnits(4);
                Rectangle countBounds = new(bounds.Right - chevronSpace - countWidth, bounds.Top, countWidth, bounds.Height);
                if (countBounds.Width > 0 && countBounds.Height > 0)
                {
                    TextRenderer.DrawText(g, row.HiddenText, FluentStyle.UiFontSmall, countBounds, theme.MutedForegroundColor,
                                          TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                }
            }

            Rectangle textBounds = new(bounds.Left + LogicalToDeviceUnits(10), bounds.Top, Math.Max(1, bounds.Width - LogicalToDeviceUnits(10) - chevronSpace - countWidth), bounds.Height);
            TextRenderer.DrawText(g, row.Section.ToUpperInvariant(), FluentStyle.UiFontSmallBold, textBounds, tone,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Chevron: 1.25 px vector stroke, down when open and pointing right when collapsed.
            float size = LogicalToDeviceUnits(3);
            float cx = bounds.Right - LogicalToDeviceUnits(14);
            float cy = bounds.Top + (bounds.Height / 2f);
            using Pen pen = new(tone, LogicalToDeviceUnits(1) * 1.25f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            if (collapsed)
                g.DrawLines(pen, new[] { new PointF(cx - (size / 2), cy - size), new PointF(cx + (size / 2), cy), new PointF(cx - (size / 2), cy + size) });
            else
                g.DrawLines(pen, new[] { new PointF(cx - size, cy - (size / 2)), new PointF(cx, cy + (size / 2)), new PointF(cx + size, cy - (size / 2)) });
        }

        private void DrawItem(Graphics g, Theme theme, SidebarItem item, Rectangle bounds, bool selected, bool hover)
        {
            float radius = LogicalToDeviceUnits(6);
            if (selected || hover)
            {
                // Active entry: accent at 12% over the pane; hover is a faint neutral wash.
                Color fill = selected
                    ? FluentStyle.Blend(theme.SidebarColor, theme.AccentColor, 0.12)
                    : FluentStyle.Blend(theme.SidebarColor, theme.ForegroundColor, theme.IsDark ? 0.05 : 0.035);
                FluentStyle.FillRounded(g, fill, bounds, radius);
            }

            if (selected)
            {
                int pillHeight = LogicalToDeviceUnits(16);
                RectangleF pill = new(bounds.Left + LogicalToDeviceUnits(1), bounds.Top + ((bounds.Height - pillHeight) / 2f), LogicalToDeviceUnits(3), pillHeight);
                FluentStyle.FillRounded(g, theme.AccentColor, pill, pill.Width / 2f);
                if (Focused)
                    FluentStyle.DrawRounded(g, FluentStyle.Blend(theme.SidebarColor, theme.ForegroundColor, 0.35), bounds, radius);
            }

            int iconSize = LogicalToDeviceUnits(16);
            int iconLeft = _compact ? bounds.Left + ((bounds.Width - iconSize) / 2) : bounds.Left + LogicalToDeviceUnits(12);
            if (item.Image != null)
                g.DrawImage(item.Image, new Rectangle(iconLeft, bounds.Top + ((bounds.Height - iconSize) / 2), iconSize, iconSize));

            if (_compact)
                return;

            TextRenderer.DrawText(g, item.Text, selected ? FluentStyle.UiFontSemibold : FluentStyle.UiFont, TextBounds(bounds), theme.ForegroundColor,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private Rectangle TextBounds(Rectangle bounds)
        {
            int left = bounds.Left + LogicalToDeviceUnits(12 + 16 + 12);
            return new Rectangle(left, bounds.Top, bounds.Right - left - LogicalToDeviceUnits(6), bounds.Height);
        }
    }
}
