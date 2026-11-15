// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls;

/// <summary>
/// Fluent (Windows 11) combo box. The closed field and the drop-down are both painted by the app:
/// the native list (which tracks the mouse with its own selection drawing) is replaced by a rounded popup
/// with hover highlight, an accent pill on the current value and keyboard support.
/// </summary>
public class FluentComboBox : ComboBox, IThemedControl
{
    private const int WM_PAINT = 0x000F;
    private const int WM_ERASEBKGND = 0x0014;
    private const int WM_PRINTCLIENT = 0x0318;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    private bool _isHovered;
    private ToolStripDropDown _popup;
    private BufferedGraphicsContext _bufferContext;
    private FluentComboList _list;
    private int _lastPopupClose;

    protected override bool ShowFocusCues => false;

    public FluentComboBox()
    {
        FlatStyle = FlatStyle.Flat;
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        ItemHeight = LogicalToDeviceUnits(26);
        Font = FluentStyle.UiFont;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    /// <summary>True while the custom drop-down popup is open.</summary>
    public bool IsPopupOpen => _popup is { Visible: true };

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        ForeColor = theme.ForegroundColor;
        Invalidate();
    }

    internal static Color Fill(Theme theme, bool hover) =>
        theme.IsDark
            ? FluentStyle.Blend(theme.SurfaceColor, Color.White, hover ? 0.09 : 0.05)
            : (hover ? FluentStyle.Blend(Color.White, theme.ForegroundColor, 0.03) : Color.White);

    internal static Color Border(Theme theme) =>
        FluentStyle.Blend(theme.SurfaceColor, theme.ForegroundColor, theme.IsDark ? 0.12 : 0.16);

    internal static Color PopupBack(Theme theme) =>
        theme.IsDark ? FluentStyle.Blend(theme.BackgroundColor, Color.White, 0.07) : Color.FromArgb(249, 249, 249);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Software.OperatingSystem.IsUnix)
            return;

        try
        {
            // The closed field is fully painted by us: removing its visual style also removes the native
            // hot-track animation, which paints outside WM_PAINT and flashed white on hover.
            SetWindowTheme(Handle, string.Empty, string.Empty);
        }
        catch (DllNotFoundException)
        { }
        catch (EntryPointNotFoundException)
        { }
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

    protected override bool IsInputKey(Keys keyData) =>
        (IsPopupOpen && keyData is Keys.Escape or Keys.Enter or Keys.Up or Keys.Down or Keys.Home or Keys.End) || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // The popup does not take keyboard focus: route navigation keys to its list.
        if (IsPopupOpen && _list != null)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode is Keys.Up or Keys.Down))
                _popup.Close(ToolStripDropDownCloseReason.Keyboard);
            else
                _list.HandleKey(e.KeyCode);

            return;
        }

        // Open our popup instead of the native list (F4, Alt+Down, Space, Enter).
        if (e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode is Keys.Down or Keys.Up) || e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            ShowPopup();
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        // The native list is never shown; nothing to draw.
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_LBUTTONDOWN:
            case WM_LBUTTONDBLCLK:
                if (!Focused)
                    Focus();

                // A click on the field while the popup is open first closes the popup (outside click);
                // don't reopen it immediately.
                if (unchecked(Environment.TickCount - _lastPopupClose) > 200)
                    ShowPopup();

                return;

            case WM_ERASEBKGND:
                m.Result = (IntPtr)1;
                return;

            case WM_PAINT:
            case WM_PRINTCLIENT:
                PAINTSTRUCT ps = new();
                IntPtr hdc = m.WParam != IntPtr.Zero ? m.WParam : BeginPaint(Handle, ref ps);
                try
                {
                    using Graphics target = Graphics.FromHdc(hdc);
                    PaintControlSurface(target);
                }
                finally
                {
                    if (m.WParam == IntPtr.Zero)
                        EndPaint(Handle, ref ps);
                }

                m.Result = IntPtr.Zero;
                return;
        }

        base.WndProc(ref m);
    }

    private void ShowPopup()
    {
        if (IsPopupOpen || Items.Count == 0 || !Enabled)
            return;

        Theme theme = Theme.Current;
        FluentComboList list = new(this);
        _list = list;

        ToolStripControlHost host = new(list)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = list.Size
        };

        _popup = new ToolStripDropDown
        {
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false,
            Size = list.Size,
            BackColor = PopupBack(theme),
            DropShadowEnabled = true,
            Renderer = new PopupRenderer()
        };
        _popup.Items.Add(host);
        _popup.Closed += (_, _) =>
        {
            _lastPopupClose = Environment.TickCount;
            ToolStripDropDown closed = _popup;
            _popup = null;
            _list = null;
            Invalidate();
            BeginInvoke(new Action(() => closed?.Dispose()));
        };

        // Below the field, or above it when there is no room on the screen.
        Rectangle screen = Screen.FromControl(this).WorkingArea;
        Point below = PointToScreen(new Point(0, Height + LogicalToDeviceUnits(2)));
        Point location = below.Y + list.Height <= screen.Bottom
            ? new Point(0, Height + LogicalToDeviceUnits(2))
            : new Point(0, -list.Height - LogicalToDeviceUnits(2));

        _popup.Show(this, location);
        FluentStyle.ApplyWindowChrome(_popup.Handle, Border(theme), smallCorners: true);
        list.Focus();
        Invalidate();
    }

    internal void ClosePopup() => _popup?.Close(ToolStripDropDownCloseReason.ItemClicked);

    private void PaintControlSurface(Graphics target)
    {
        if (Width <= 0 || Height <= 0)
            return;

        Theme theme = Theme.Current;
        Rectangle client = ClientRectangle;

        // A private buffer context: the shared BufferedGraphicsManager.Current is also used by every double-buffered
        // control, and mixing the two left pixels of this field inside neighbouring controls (e.g. the corner of a button).
        _bufferContext ??= new BufferedGraphicsContext();
        using BufferedGraphics buffer = _bufferContext.Allocate(target, client);
        Graphics g = buffer.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Parent?.BackColor ?? theme.BackgroundColor);

        float radius = Math.Max(3f, LogicalToDeviceUnits(FluentStyle.ControlCornerLogical));
        RectangleF bounds = new(0.5f, 0.5f, client.Width - 1.5f, client.Height - 1.5f);
        bool active = Focused || IsPopupOpen;

        FluentStyle.FillRounded(g, Enabled ? Fill(theme, _isHovered || IsPopupOpen) : theme.SurfaceColor, bounds, radius);
        FluentStyle.DrawRounded(g, Border(theme), bounds, radius);

        // Accent underline when focused/open, like Windows 11 text and combo fields.
        if (active)
        {
            float lineHeight = Math.Max(2f, LogicalToDeviceUnits(2));
            g.SetClip(new RectangleF(0, client.Height - lineHeight - 1, client.Width, lineHeight + 1));
            FluentStyle.DrawRounded(g, theme.AccentColor, bounds, radius, lineHeight);
            g.ResetClip();
        }

        string selectedText = SelectedIndex >= 0 && SelectedIndex < Items.Count
            ? Items[SelectedIndex]?.ToString() ?? string.Empty
            : Text;

        int textPadX = LogicalToDeviceUnits(11);
        int arrowWidth = LogicalToDeviceUnits(30);
        Rectangle textRect = new(textPadX, 0, Math.Max(10, client.Width - textPadX - arrowWidth), client.Height);
        TextRenderer.DrawText(g, selectedText, Font, textRect, Enabled ? theme.ForegroundColor : theme.MutedForegroundColor,
                              TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        float chevronX = client.Width - LogicalToDeviceUnits(16);
        float chevronY = (client.Height / 2f) - LogicalToDeviceUnits(2);
        float arrowSize = LogicalToDeviceUnits(4);
        using (Pen arrowPen = new(theme.MutedForegroundColor, Math.Max(1.2f, LogicalToDeviceUnits(1)) * 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            g.DrawLines(arrowPen, new[]
            {
                new PointF(chevronX - arrowSize, chevronY),
                new PointF(chevronX, chevronY + arrowSize),
                new PointF(chevronX + arrowSize, chevronY)
            });
        }

        buffer.Render(target);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _popup?.Dispose();
            _bufferContext?.Dispose();

        base.Dispose(disposing);
    }

    /// <summary>Paints nothing around the hosted list (no ToolStrip border or margins).</summary>
    internal sealed class PopupRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using SolidBrush brush = new(PopupBack(Theme.Current));
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using Pen pen = new(Border(Theme.Current));
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
}

/// <summary>Item list shown inside the <see cref="FluentComboBox" /> popup.</summary>
internal sealed class FluentComboList : Control
{
    private const int ItemLogicalHeight = 32;
    private const int PaddingLogical = 4;

    private readonly FluentComboBox _owner;
    private int _hoverIndex = -1;

    public FluentComboList(FluentComboBox owner)
    {
        _owner = owner;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Font = owner.Font;
        _hoverIndex = owner.SelectedIndex;
        Size = new Size(Math.Max(owner.Width, LogicalToDeviceUnits(120)),
                        (owner.Items.Count * ItemHeight) + (owner.LogicalToDeviceUnits(PaddingLogical) * 2));
    }

    private int ItemHeight => _owner.LogicalToDeviceUnits(ItemLogicalHeight);

    private int Pad => _owner.LogicalToDeviceUnits(PaddingLogical);

    private Rectangle ItemBounds(int index) => new(Pad, Pad + (index * ItemHeight), Width - (Pad * 2), ItemHeight);

    private int HitTest(Point p)
    {
        if (p.X < 0 || p.X >= Width || p.Y < Pad)
            return -1;

        int index = (p.Y - Pad) / ItemHeight;
        return index >= 0 && index < _owner.Items.Count ? index : -1;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Enter or Keys.Escape or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        HandleKey(e.KeyCode);
    }

    public void HandleKey(Keys key)
    {
        int count = _owner.Items.Count;
        switch (key)
        {
            case Keys.Down:
                SetHover(Math.Min(count - 1, _hoverIndex + 1));
                break;
            case Keys.Up:
                SetHover(Math.Max(0, _hoverIndex - 1));
                break;
            case Keys.Home:
                SetHover(0);
                break;
            case Keys.End:
                SetHover(count - 1);
                break;
            case Keys.Enter:
                Commit(_hoverIndex);
                break;
            case Keys.Escape:
                _owner.ClosePopup();
                break;
        }
    }

    private void SetHover(int index)
    {
        if (index == _hoverIndex)
            return;

        _hoverIndex = index;
        Invalidate();
    }

    private void Commit(int index)
    {
        if (index >= 0 && index < _owner.Items.Count && index != _owner.SelectedIndex)
            _owner.SelectedIndex = index;

        _owner.ClosePopup();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHover(HitTest(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            int index = HitTest(e.Location);
            if (index >= 0)
                Commit(index);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        Color back = FluentComboBox.PopupBack(theme);
        g.Clear(back);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = _owner.LogicalToDeviceUnits(4);
        for (int i = 0; i < _owner.Items.Count; i++)
        {
            Rectangle item = ItemBounds(i);
            bool current = i == _owner.SelectedIndex;

            // Exactly one highlighted row: the one under the mouse / keyboard cursor.
            if (i == _hoverIndex)
                FluentStyle.FillRounded(g, FluentStyle.Blend(back, theme.ForegroundColor, theme.IsDark ? 0.08 : 0.06), item, radius);
            else if (current)
                FluentStyle.FillRounded(g, FluentStyle.Blend(back, theme.ForegroundColor, theme.IsDark ? 0.05 : 0.04), item, radius);

            if (current)
            {
                int pillHeight = _owner.LogicalToDeviceUnits(16);
                RectangleF pill = new(item.X, item.Y + ((item.Height - pillHeight) / 2f), _owner.LogicalToDeviceUnits(3), pillHeight);
                FluentStyle.FillRounded(g, theme.AccentColor, pill, pill.Width / 2f);
            }

            Rectangle text = new(item.X + _owner.LogicalToDeviceUnits(12), item.Y, item.Width - _owner.LogicalToDeviceUnits(16), item.Height);
            TextRenderer.DrawText(g, _owner.Items[i]?.ToString() ?? string.Empty, Font, text, theme.ForegroundColor,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
