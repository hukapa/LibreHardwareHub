// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// In-game HUD window: per-pixel alpha through UpdateLayeredWindow, WS_EX_NOACTIVATE so it never steals
/// focus and WS_EX_TRANSPARENT for click-through. It paints straight into a DIB section that stays
/// selected in a memory DC, so a frame allocates nothing and copies no bitmap.
/// </summary>
public sealed class OverlayForm : Form
{
    private readonly OverlayConfig _config;
    private readonly OverlayRenderer _renderer;

    private IntPtr _memoryDc;
    private IntPtr _dib;
    private IntPtr _previousBitmap;
    private Bitmap _surface;
    private Graphics _graphics;
    private bool _dragging;
    private Point _dragOffset;

    public OverlayForm(OverlayConfig config, OverlayTelemetry telemetry)
    {
        _config = config;
        _renderer = new OverlayRenderer(config, telemetry);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
    }

    /// <summary>Raised after the user dragged the HUD to a new position (already stored in the configuration).</summary>
    public event EventHandler UserMoved;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WsExLayered | WsExToolWindow | WsExNoActivate | WsExTopMost;
            if (_config is { ClickThrough: true })
                cp.ExStyle |= WsExTransparent;
            return cp;
        }
    }

    /// <summary>
    /// Mouse interaction (drag mode) must never take the keyboard focus from the game: the click is delivered
    /// to the HUD but the window is not activated.
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate)
        {
            m.Result = (IntPtr)MaNoActivate;
            return;
        }

        base.WndProc(ref m);
    }

    /// <summary>Shows the HUD without activating it and raises it above every other topmost window.</summary>
    public void ShowNoActivate()
    {
        if (!IsHandleCreated)
            CreateHandle();

        ShowWindow(Handle, SwShowNoActivate);
        SetWindowPos(Handle, HwndTopMost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpShowWindow | SwpNoOwnerZOrder);
    }

    /// <summary>Re-asserts the topmost position (borderless games, launchers) without activating or flickering.</summary>
    public void EnsureTopMostNoActivate()
    {
        if (!IsHandleCreated)
            return;

        SetWindowPos(Handle, HwndTopMost, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZOrder);
    }

    /// <summary>Applies a configuration change: click-through style, layout, position and a fresh frame.</summary>
    public void ApplyConfig()
    {
        _renderer.Invalidate();
        if (!IsHandleCreated)
            return;

        int style = GetWindowLong(Handle, GwlExStyle);
        int wanted = _config.ClickThrough ? style | WsExTransparent : style & ~WsExTransparent;
        if (wanted != style)
            SetWindowLong(Handle, GwlExStyle, wanted);

        Render();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
            ApplyConfig();
    }

    /// <summary>Lays the HUD out, repositions the window when its size changed and pushes a new frame.</summary>
    public void Render()
    {
        if (!IsHandleCreated || !Visible)
            return;

        if (_graphics == null && !CreateSurface(new Size(1, 1)))
            return;

        Size size = _renderer.Layout(_graphics);
        if (size.IsEmpty)
            size = new Size(1, 1);

        if (size != _surface.Size && !CreateSurface(size))
            return;

        _graphics.Clear(Color.Transparent);
        _renderer.Draw(_graphics, Point.Empty);

        Point location = _dragging ? Location : ResolveLocation(size);
        Bounds = new Rectangle(location, size);

        NativePoint destination = new() { X = location.X, Y = location.Y };
        NativePoint source = default;
        NativeSize nativeSize = new() { Cx = size.Width, Cy = size.Height };
        BlendFunction blend = new() { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = AcSrcAlpha };
        UpdateLayeredWindow(Handle, IntPtr.Zero, ref destination, ref nativeSize, _memoryDc, ref source, 0, ref blend, UlwAlpha);

        // Another topmost window (a borderless game, a launcher) may have been raised above the HUD meanwhile.
        EnsureTopMostNoActivate();
    }

    private Point ResolveLocation(Size size)
    {
        Screen[] screens = Screen.AllScreens;
        Screen screen = _config.ScreenIndex >= 0 && _config.ScreenIndex < screens.Length ? screens[_config.ScreenIndex] : Screen.PrimaryScreen;
        Rectangle area = screen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        int margin = _config.Margin;

        int left = area.X + margin;
        int center = area.X + ((area.Width - size.Width) / 2);
        int right = area.Right - size.Width - margin;
        int top = area.Y + margin;
        int bottom = area.Bottom - size.Height - margin;

        switch (_config.Anchor)
        {
            case OverlayAnchor.TopCenter: return new Point(center, top);
            case OverlayAnchor.TopRight: return new Point(right, top);
            case OverlayAnchor.BottomLeft: return new Point(left, bottom);
            case OverlayAnchor.BottomCenter: return new Point(center, bottom);
            case OverlayAnchor.BottomRight: return new Point(right, bottom);
            case OverlayAnchor.Custom:
                // Never let a saved position (other monitor layout, other resolution) leave the HUD off-screen.
                Rectangle desktop = SystemInformation.VirtualScreen;
                return new Point(
                    Math.Clamp(_config.CustomPosition.X, desktop.Left, Math.Max(desktop.Left, desktop.Right - size.Width)),
                    Math.Clamp(_config.CustomPosition.Y, desktop.Top, Math.Max(desktop.Top, desktop.Bottom - size.Height)));
            default: return new Point(left, top);
        }
    }

    private bool CreateSurface(Size size)
    {
        ReleaseSurface();

        BitmapInfoHeader header = new()
        {
            Size = Marshal.SizeOf<BitmapInfoHeader>(),
            Width = size.Width,
            Height = -size.Height, // top-down, the row order GDI+ expects
            Planes = 1,
            BitCount = 32
        };

        _memoryDc = CreateCompatibleDC(IntPtr.Zero);
        _dib = CreateDIBSection(_memoryDc, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
        if (_dib == IntPtr.Zero || bits == IntPtr.Zero)
        {
            ReleaseSurface();
            return false;
        }

        _previousBitmap = SelectObject(_memoryDc, _dib);
        _surface = new Bitmap(size.Width, size.Height, size.Width * 4, PixelFormat.Format32bppPArgb, bits);
        _graphics = Graphics.FromImage(_surface);
        return true;
    }

    private void ReleaseSurface()
    {
        _graphics?.Dispose();
        _graphics = null;
        _surface?.Dispose();
        _surface = null;

        if (_memoryDc != IntPtr.Zero)
        {
            if (_previousBitmap != IntPtr.Zero)
                SelectObject(_memoryDc, _previousBitmap);
            DeleteDC(_memoryDc);
        }

        if (_dib != IntPtr.Zero)
            DeleteObject(_dib);

        _memoryDc = _dib = _previousBitmap = IntPtr.Zero;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_config.ClickThrough || e.Button != MouseButtons.Left)
            return;

        _dragging = true;
        _dragOffset = e.Location;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
            return;

        Point cursor = Cursor.Position;
        Location = new Point(cursor.X - _dragOffset.X, cursor.Y - _dragOffset.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging)
            return;

        _dragging = false;
        Capture = false;
        _config.Anchor = OverlayAnchor.Custom;
        _config.CustomPosition = Location;
        UserMoved?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        ReleaseSurface();
        if (disposing)
            _renderer.Dispose();

        base.Dispose(disposing);
    }

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTopMost = 0x00000008;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private const int SwShowNoActivate = 4;
    private const byte AcSrcAlpha = 0x01;
    private const int UlwAlpha = 0x02;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private static readonly IntPtr HwndTopMost = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Cx;
        public int Cy;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize,
                                                   IntPtr hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader header, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern bool DeleteObject(IntPtr hObject);
}
