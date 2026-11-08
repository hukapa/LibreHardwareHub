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
/// Fluent (Windows 11) combo box: slightly elevated fill, hairline border, accent underline when focused
/// or open, custom chevron and owner-drawn items. The drop-down list uses the dark system theme in dark mode.
/// The closed control is painted through a back buffer to avoid flicker over the native drawing.
/// </summary>
public class FluentComboBox : ComboBox, IThemedControl
{
    private const int WM_PAINT = 0x000F;
    private bool _isHovered;

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

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.SurfaceColor;
        ForeColor = theme.ForegroundColor;
        ApplyListTheme(theme);
        Invalidate();
    }

    private static Color Fill(Theme theme, bool hover) =>
        theme.IsDark
            ? FluentStyle.Blend(theme.SurfaceColor, Color.White, hover ? 0.09 : 0.05)
            : (hover ? FluentStyle.Blend(Color.White, theme.ForegroundColor, 0.03) : Color.White);

    private static Color Border(Theme theme) =>
        FluentStyle.Blend(theme.SurfaceColor, theme.ForegroundColor, theme.IsDark ? 0.12 : 0.16);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyListTheme(Theme.Current);
    }

    /// <summary>Dark drop-down list (and its scrollbar) in dark mode, default otherwise.</summary>
    private void ApplyListTheme(Theme theme)
    {
        if (!IsHandleCreated || Software.OperatingSystem.IsUnix)
            return;

        try
        {
            // The closed field is fully painted by us: removing its visual style also removes the native
            // hot-track animation, which paints outside WM_PAINT and flashed white on hover.
            SetWindowTheme(Handle, string.Empty, string.Empty);

            // The drop-down list is a separate window; give it (and its scrollbar) the matching system theme.
            COMBOBOXINFO info = new() { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            if (GetComboBoxInfo(Handle, ref info) && info.hwndList != IntPtr.Zero)
                SetWindowTheme(info.hwndList, theme.IsDark ? "DarkMode_Explorer" : "Explorer", null);
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

    protected override void OnDropDownClosed(EventArgs e)
    {
        base.OnDropDownClosed(e);
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
            return;

        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        bool isEditArea = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
        bool isCurrent = e.Index == SelectedIndex && !isEditArea;
        isSelected &= !isEditArea;

        Color listBack = theme.IsDark ? FluentStyle.Blend(theme.SurfaceColor, Color.White, 0.03) : Color.White;
        using (SolidBrush back = new(listBack))
            g.FillRectangle(back, e.Bounds);

        int inset = LogicalToDeviceUnits(4);
        Rectangle item = new(e.Bounds.X + inset, e.Bounds.Y + 1, e.Bounds.Width - (inset * 2), e.Bounds.Height - 2);
        if (isSelected)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            FluentStyle.FillRounded(g, FluentStyle.Blend(listBack, theme.ForegroundColor, theme.IsDark ? 0.09 : 0.06), item, LogicalToDeviceUnits(4));
        }

        // Windows 11 marks the current value with a short accent pill on the left.
        if (isCurrent)
        {
            int pillHeight = LogicalToDeviceUnits(14);
            RectangleF pill = new(item.X + 1, item.Y + ((item.Height - pillHeight) / 2f), LogicalToDeviceUnits(3), pillHeight);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            FluentStyle.FillRounded(g, theme.AccentColor, pill, pill.Width / 2f);
        }

        Rectangle textBounds = new(item.X + LogicalToDeviceUnits(10), item.Y, item.Width - LogicalToDeviceUnits(14), item.Height);
        TextRenderer.DrawText(g, Items[e.Index]?.ToString() ?? string.Empty, Font, textBounds, theme.ForegroundColor,
                              TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void WndProc(ref Message m)
    {
        if (DropDownStyle == ComboBoxStyle.DropDownList)
        {
            switch (m.Msg)
            {
                case WM_ERASEBKGND:
                    m.Result = (IntPtr)1;
                    return;

                case WM_PAINT:
                    // Paint the closed field ourselves only; the native control never draws it.
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
        }

        base.WndProc(ref m);
    }

    private void PaintControlSurface(Graphics target)
    {
        if (Width <= 0 || Height <= 0)
            return;

        Theme theme = Theme.Current;
        Rectangle client = ClientRectangle;

        using BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(target, client);
        Graphics g = buffer.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Parent?.BackColor ?? theme.BackgroundColor);

        float radius = Math.Max(3f, LogicalToDeviceUnits(theme.CornerRadius - 2));
        RectangleF bounds = new(0.5f, 0.5f, client.Width - 1.5f, client.Height - 1.5f);
        bool active = Focused || DroppedDown;

        FluentStyle.FillRounded(g, Enabled ? Fill(theme, _isHovered || DroppedDown) : theme.SurfaceColor, bounds, radius);
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

    private const int WM_ERASEBKGND = 0x0014;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct COMBOBOXINFO
    {
        public int cbSize;
        public RECT rcItem;
        public RECT rcButton;
        public int stateButton;
        public IntPtr hwndCombo;
        public IntPtr hwndItem;
        public IntPtr hwndList;
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

    [DllImport("user32.dll")]
    private static extern bool GetComboBoxInfo(IntPtr hWnd, ref COMBOBOXINFO pcbi);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
}
