using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls
{
    /// <summary>
    /// Fluent navigation pane: entries grouped in collapsible sections, rounded hover and selection highlights
    /// with an accent pill, a button to switch to the icon-only compact mode, keyboard navigation
    /// (Up/Down/Home/End) and mouse wheel scrolling with a thin scroll indicator.
    /// </summary>
    public sealed partial class SidebarPanel : Control, IThemedControl
    {
        private const int ItemLogicalHeight = 36;
        private const int HeaderLogicalHeight = 30;
        private const int TopLogicalHeight = 44;
        private const int SectionGapLogical = 6;
        private const int FooterPaddingLogical = 10;

        // Hit-test result for the pinned footer: FooterHit - index of the footer entry.
        private const int FooterHit = -100;

        // A row is either a section caption (Item == null) or an entry.
        private struct Row
        {
            public SidebarItem Item;
            public string Section;
            public int Top;
            public int Height;
            public int Hidden;
            public string HiddenText;
        }

        private readonly List<SidebarItem> _items = new();
        private readonly List<SidebarItem> _footer = new();
        private readonly List<Row> _rows = new();
        private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
        private ToolTip _toolTip;
        private SidebarItem _selected;
        private int _hoverRow = -1; // -2 = compact toggle
        private int _contentHeight;
        private int _scrollOffset;
        private bool _compact;

        public SidebarPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
        }

        public event EventHandler SelectionChanged;

        /// <summary>Raised when the user asks to switch between the wide and the icon-only mode.</summary>
        public event EventHandler CompactToggleRequested;

        /// <summary>Raised when a section was collapsed or expanded (see <see cref="CollapsedSections" />).</summary>
        public event EventHandler CollapsedSectionsChanged;

        public SidebarItem SelectedItem => _selected;

        public bool Compact
        {
            get => _compact;
            set
            {
                if (_compact == value)
                    return;

                _compact = value;
                BuildRows();
            }
        }

        /// <summary>Names of the collapsed sections joined with '|', for persistence.</summary>
        public string CollapsedSections
        {
            get => string.Join("|", _collapsed);
            set
            {
                _collapsed.Clear();
                foreach (string section in (value ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
                    _collapsed.Add(section);

                BuildRows();
            }
        }

        /// <summary>Replaces the items, keeping the selection on the item with the same tag when possible.</summary>
        public void SetItems(IEnumerable<SidebarItem> items, object selectedTag)
        {
            _items.Clear();
            _items.AddRange(items);
            _selected = _items.Find(i => Equals(i.Tag, selectedTag)) ?? (_items.Count > 0 ? _items[0] : null);
            _hoverRow = -1;
            BuildRows();
        }

        /// <summary>Selects the item carrying <paramref name="tag" /> and raises <see cref="SelectionChanged" /> when it changes.</summary>
        public void SelectTag(object tag) => Select(_items.Find(i => Equals(i.Tag, tag)));

        /// <summary>Selects the item at <paramref name="index" /> (in the order given to <see cref="SetItems" />).</summary>
        public void SelectIndex(int index)
        {
            if (index >= 0 && index < _items.Count)
                Select(_items[index]);
        }

        private void Select(SidebarItem item)
        {
            if (item == null || item == _selected)
                return;

            _selected = item;
            BuildRows();
            EnsureVisible(item);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyTheme(Theme theme)
        {
            BackColor = theme.SidebarColor;
            ForeColor = theme.ForegroundColor;
            Invalidate();
        }

        // ---- Layout ----

        private int TopHeight => LogicalToDeviceUnits(TopLogicalHeight);

        /// <summary>Height of the pinned footer (its entries plus padding); 0 when no entry is pinned.</summary>
        private int FooterHeight => _footer.Count == 0 ? 0 : LogicalToDeviceUnits((ItemLogicalHeight * _footer.Count) + FooterPaddingLogical);

        /// <summary>Height of the scrolling area: between the top bar and the footer.</summary>
        private int ViewHeight => Math.Max(0, Height - TopHeight - FooterHeight);

        private void BuildRows()
        {
            _rows.Clear();
            _footer.Clear();
            int y = 0;
            string section = string.Empty;
            int header = -1;

            foreach (SidebarItem item in _items)
            {
                // Pinned entries live below the scrolling list and never take part in sections.
                if (item.Footer)
                {
                    _footer.Add(item);
                    continue;
                }

                if (item.Section != section)
                {
                    section = item.Section;
                    header = -1;
                    if (section.Length > 0)
                    {
                        // Same breathing room above every caption; in compact mode it is just a hairline separator.
                        if (!_compact && _rows.Count > 0)
                            y += LogicalToDeviceUnits(SectionGapLogical);

                        int height = LogicalToDeviceUnits(_compact ? 13 : HeaderLogicalHeight);
                        header = _rows.Count;
                        _rows.Add(new Row { Section = section, Top = y, Height = height });
                        y += height;
                    }
                }

                // A collapsed section still shows the selected entry, so the user never loses track of where they are.
                if (!_compact && _collapsed.Contains(section) && item != _selected)
                {
                    if (header >= 0)
                    {
                        Row caption = _rows[header];
                        caption.Hidden++;
                        _rows[header] = caption;
                    }

                    continue;
                }

                int itemHeight = LogicalToDeviceUnits(ItemLogicalHeight);
                _rows.Add(new Row { Item = item, Section = section, Top = y, Height = itemHeight });
                y += itemHeight;
            }

            // The count of hidden entries of a collapsed section is formatted here, not on every paint.
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Hidden > 0)
                {
                    Row caption = _rows[i];
                    caption.HiddenText = caption.Hidden.ToString();
                    _rows[i] = caption;
                }
            }

            _contentHeight = y + LogicalToDeviceUnits(8);
            ClampScroll();
            Invalidate();
        }

        private Rectangle RowBounds(Row row)
        {
            int margin = LogicalToDeviceUnits(6);
            return new Rectangle(margin, TopHeight + row.Top - _scrollOffset, Width - (margin * 2), row.Height - LogicalToDeviceUnits(2));
        }

        private Rectangle FooterItemBounds(int index)
        {
            int margin = LogicalToDeviceUnits(6);
            int itemHeight = LogicalToDeviceUnits(ItemLogicalHeight);
            int top = Height - FooterHeight + LogicalToDeviceUnits(FooterPaddingLogical / 2) + (index * itemHeight);
            return new Rectangle(margin, top, Width - (margin * 2), itemHeight - LogicalToDeviceUnits(2));
        }

        private Rectangle ToggleBounds => new(LogicalToDeviceUnits(6), LogicalToDeviceUnits(6), LogicalToDeviceUnits(44), LogicalToDeviceUnits(32));

        private int MaxScroll => Math.Max(0, _contentHeight - ViewHeight);

        private void ClampScroll() => _scrollOffset = Math.Clamp(_scrollOffset, 0, MaxScroll);

        private void EnsureVisible(SidebarItem item)
        {
            int index = _rows.FindIndex(r => r.Item == item);
            if (index < 0)
                return;

            Row row = _rows[index];
            int view = ViewHeight;
            if (row.Top < _scrollOffset)
                _scrollOffset = row.Top;
            else if (row.Top + row.Height > _scrollOffset + view)
                _scrollOffset = row.Top + row.Height - view;

            ClampScroll();
            Invalidate();
        }

        private int HitTest(Point point)
        {
            if (ToggleBounds.Contains(point))
                return -2;

            for (int i = 0; i < _footer.Count; i++)
            {
                if (FooterItemBounds(i).Contains(point))
                    return FooterHit - i;
            }

            // Rows scrolled behind the top bar or the footer are not clickable.
            if (point.Y < TopHeight || point.Y >= Height - FooterHeight)
                return -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                // In the icon-only mode captions are plain separators.
                if (_compact && _rows[i].Item == null)
                    continue;

                if (RowBounds(_rows[i]).Contains(point))
                    return i;
            }

            return -1;
        }

        // ---- Input ----

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int row = HitTest(e.Location);
            if (row == _hoverRow)
                return;

            _hoverRow = row;
            Cursor = row != -1 ? Cursors.Hand : Cursors.Default;
            UpdateToolTip(row);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverRow = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left)
                return;

            int index = HitTest(e.Location);
            if (index == -2)
            {
                CompactToggleRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (index <= FooterHit)
            {
                Select(_footer[FooterHit - index]);
            }
            else if (index >= 0 && _rows[index].Item != null)
            {
                Select(_rows[index].Item);
            }
            else if (index >= 0 && !_compact)
            {
                string section = _rows[index].Section;
                if (!_collapsed.Remove(section))
                    _collapsed.Add(section);

                BuildRows();
                CollapsedSectionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _scrollOffset -= e.Delta / 120 * LogicalToDeviceUnits(ItemLogicalHeight);
            ClampScroll();
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Moves through the entries currently listed (collapsed sections are skipped); the pinned entries come last.
            List<SidebarItem> visible = new();
            foreach (Row row in _rows)
            {
                if (row.Item != null)
                    visible.Add(row.Item);
            }

            visible.AddRange(_footer);

            if (visible.Count == 0)
                return;

            int index = visible.IndexOf(_selected);
            int target = e.KeyCode switch
            {
                Keys.Up => Math.Max(0, index - 1),
                Keys.Down => Math.Min(visible.Count - 1, index + 1),
                Keys.Home => 0,
                Keys.End => visible.Count - 1,
                _ => -1
            };

            if (target < 0)
                return;

            Select(visible[target]);
            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampScroll();
        }

        // A tooltip only where the text is not fully readable: icon-only mode, truncated names and the toggle.
        private void UpdateToolTip(int row)
        {
            string text = null;
            if (row == -2)
            {
                text = _compact ? "Expandir navegação" : "Compactar navegação";
            }
            else if (row <= FooterHit)
            {
                SidebarItem pinned = _footer[FooterHit - row];
                text = ItemToolTip(pinned);
            }
            else if (row >= 0 && _rows[row].Item is { } item)
            {
                bool truncated = TextRenderer.MeasureText(item.Text, FluentStyle.UiFontSemibold).Width > TextBounds(RowBounds(_rows[row])).Width;
                if (_compact || truncated)
                    text = ItemToolTip(item);
            }

            _toolTip ??= new ToolTip { ShowAlways = true };
            if (_toolTip.GetToolTip(this) != (text ?? string.Empty))
                _toolTip.SetToolTip(this, text);
        }

        // "Definições (Ctrl+7)": the name, plus the shortcut when the entry has one.
        private static string ItemToolTip(SidebarItem item) => item.Shortcut.Length > 0 ? $"{item.Text} ({item.Shortcut})" : item.Text;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _toolTip?.Dispose();

            base.Dispose(disposing);
        }
    }
}
