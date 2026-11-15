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
            g.Clear(theme.BackgroundColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.SetClip(new Rectangle(0, TopHeight, Width, Height - TopHeight));
            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                Rectangle bounds = RowBounds(row);
                if (bounds.Bottom < TopHeight || bounds.Top > Height)
                    continue;

                if (row.Item == null)
                    DrawHeader(g, theme, row, bounds, i == _hoverRow);
                else
                    DrawItem(g, theme, row.Item, bounds, row.Item == _selected, i == _hoverRow);
            }

            g.ResetClip();
            DrawToggle(g, theme);

            // Thin scroll indicator, only while the list overflows.
            if (MaxScroll > 0)
            {
                int track = Height - TopHeight - LogicalToDeviceUnits(8);
                int thumb = Math.Max(LogicalToDeviceUnits(24), track * (Height - TopHeight) / _contentHeight);
                int y = TopHeight + LogicalToDeviceUnits(4) + ((track - thumb) * _scrollOffset / MaxScroll);
                FluentStyle.FillRounded(g, theme.ScrollbarTrack, new RectangleF(Width - LogicalToDeviceUnits(4), y, LogicalToDeviceUnits(3), thumb), LogicalToDeviceUnits(3) / 2f);
            }
        }

        private void DrawToggle(Graphics g, Theme theme)
        {
            Rectangle bounds = ToggleBounds;
            if (_hoverRow == -2)
                FluentStyle.FillRounded(g, FluentStyle.Blend(theme.BackgroundColor, theme.ForegroundColor, theme.IsDark ? 0.07 : 0.05), bounds, LogicalToDeviceUnits(4));

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

            bool collapsed = _collapsed.Contains(row.Section);
            string text = row.Hidden > 0 ? $"{row.Section.ToUpperInvariant()}  ({row.Hidden})" : row.Section.ToUpperInvariant();
            Rectangle textBounds = new(bounds.Left + LogicalToDeviceUnits(10), bounds.Top + LogicalToDeviceUnits(4), bounds.Width - LogicalToDeviceUnits(34), bounds.Height - LogicalToDeviceUnits(4));
            TextRenderer.DrawText(g, text, FluentStyle.UiFontSmallBold, textBounds, hover ? theme.ForegroundColor : theme.MutedForegroundColor,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Chevron: right when collapsed, down when open.
            float size = LogicalToDeviceUnits(3);
            float cx = bounds.Right - LogicalToDeviceUnits(14);
            float cy = textBounds.Top + (textBounds.Height / 2f);
            using Pen pen = new(hover ? theme.ForegroundColor : theme.MutedForegroundColor, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            if (collapsed)
                g.DrawLines(pen, new[] { new PointF(cx - (size / 2), cy - size), new PointF(cx + (size / 2), cy), new PointF(cx - (size / 2), cy + size) });
            else
                g.DrawLines(pen, new[] { new PointF(cx - size, cy - (size / 2)), new PointF(cx, cy + (size / 2)), new PointF(cx + size, cy - (size / 2)) });
        }

        private void DrawItem(Graphics g, Theme theme, SidebarItem item, Rectangle bounds, bool selected, bool hover)
        {
            float radius = LogicalToDeviceUnits(4);
            if (selected || hover)
            {
                Color fill = FluentStyle.Blend(theme.BackgroundColor, theme.ForegroundColor, selected ? (theme.IsDark ? 0.10 : 0.07) : (theme.IsDark ? 0.05 : 0.035));
                FluentStyle.FillRounded(g, fill, bounds, radius);
            }

            if (selected)
            {
                int pillHeight = LogicalToDeviceUnits(16);
                RectangleF pill = new(bounds.Left, bounds.Top + ((bounds.Height - pillHeight) / 2f), LogicalToDeviceUnits(3), pillHeight);
                FluentStyle.FillRounded(g, theme.AccentColor, pill, pill.Width / 2f);
                if (Focused)
                    FluentStyle.DrawRounded(g, FluentStyle.Blend(theme.BackgroundColor, theme.ForegroundColor, 0.35), bounds, radius);
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
