// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls;

/// <summary>HSV conversions and hex parsing used by the color picker.</summary>
public static class ColorMath
{
    public static Color FromHsv(float hue, float saturation, float value)
    {
        hue = ((hue % 360f) + 360f) % 360f;
        float c = value * saturation;
        float x = c * (1f - Math.Abs((hue / 60f % 2f) - 1f));
        float m = value - c;
        (float r, float g, float b) = ((int)(hue / 60f)) switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x)
        };

        return Color.FromArgb((int)Math.Round((r + m) * 255f), (int)Math.Round((g + m) * 255f), (int)Math.Round((b + m) * 255f));
    }

    public static void ToHsv(Color color, out float hue, out float saturation, out float value)
    {
        float max = Math.Max(color.R, Math.Max(color.G, color.B)) / 255f;
        float min = Math.Min(color.R, Math.Min(color.G, color.B)) / 255f;
        hue = color.GetHue();
        saturation = max <= 0f ? 0f : (max - min) / max;
        value = max;
    }

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static bool TryParseHex(string text, out Color color)
    {
        color = Color.Empty;
        string hex = (text ?? string.Empty).Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);

        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            return false;

        color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }
}

/// <summary>
/// Body of the color flyout: saturation/brightness square, hue bar, a palette of ready-made colors and a
/// hex field. Every change is reported immediately through <see cref="ColorPicked" /> (live preview).
/// </summary>
internal sealed class FluentColorPickerPanel : Control
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb(255, 255, 255), Color.FromArgb(200, 205, 215), Color.FromArgb(140, 146, 160), Color.FromArgb(24, 24, 28),
        Color.FromArgb(0, 0, 0), Color.FromArgb(255, 75, 75), Color.FromArgb(255, 140, 40), Color.FromArgb(255, 190, 40),
        Color.FromArgb(250, 235, 90), Color.FromArgb(80, 220, 110), Color.FromArgb(0, 255, 150), Color.FromArgb(40, 210, 220),
        Color.FromArgb(70, 160, 255), Color.FromArgb(120, 120, 255), Color.FromArgb(190, 110, 255), Color.FromArgb(255, 110, 190)
    };

    private const int PaletteColumns = 8;

    private readonly FluentTextBox _hex;
    private readonly FluentButton _reset;
    private float _hue;
    private float _saturation;
    private float _value;
    private int _dragZone; // 0 = none, 1 = square, 2 = hue bar
    private bool _syncingHex;

    public FluentColorPickerPanel(Color color, bool allowReset)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        ColorMath.ToHsv(color, out _hue, out _saturation, out _value);

        _hex = new FluentTextBox { MaxLength = 7, Width = LogicalToDeviceUnits(92), Text = ColorMath.ToHex(color) };
        _hex.TextChanged += (_, _) =>
        {
            if (_syncingHex || !ColorMath.TryParseHex(_hex.Text, out Color typed))
                return;

            ColorMath.ToHsv(typed, out _hue, out _saturation, out _value);
            Invalidate();
            ColorPicked?.Invoke(typed);
        };
        Controls.Add(_hex);

        _reset = PageMetrics.FitButton(new FluentButton { Text = "Repor", Visible = allowReset });
        _reset.Click += (_, _) => ResetRequested?.Invoke();
        Controls.Add(_reset);

        Width = LogicalToDeviceUnits(248);
        Height = FooterTop + LogicalToDeviceUnits(32 + 12);
        BackColor = FluentComboBox.PopupBack(Theme.Current);
        _hex.ApplyTheme(Theme.Current);
    }

    public event Action<Color> ColorPicked;
    public event Action ResetRequested;

    private Color Current => ColorMath.FromHsv(_hue, _saturation, _value);

    private int Pad => LogicalToDeviceUnits(12);
    private Rectangle SquareBounds => new(Pad, Pad, Width - (Pad * 2), LogicalToDeviceUnits(132));
    private Rectangle HueBounds => new(Pad, SquareBounds.Bottom + LogicalToDeviceUnits(10), Width - (Pad * 2), LogicalToDeviceUnits(14));
    private int SwatchSize => LogicalToDeviceUnits(22);
    private int SwatchGap => (Width - (Pad * 2) - (SwatchSize * PaletteColumns)) / (PaletteColumns - 1);
    private int PaletteTop => HueBounds.Bottom + LogicalToDeviceUnits(12);
    private int FooterTop => PaletteTop + (SwatchSize * 2) + SwatchGap + LogicalToDeviceUnits(12);

    private Rectangle SwatchBounds(int index)
    {
        int step = SwatchSize + SwatchGap;
        return new Rectangle(Pad + (index % PaletteColumns * step), PaletteTop + (index / PaletteColumns * step), SwatchSize, SwatchSize);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        // Adding the first child already runs a layout, before the second one exists.
        if (_hex == null || _reset == null)
            return;

        int preview = LogicalToDeviceUnits(32);
        _hex.Location = new Point(Pad + preview + LogicalToDeviceUnits(8), FooterTop);
        _reset.Location = new Point(Width - Pad - _reset.Width, FooterTop);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        _dragZone = SquareBounds.Contains(e.Location) ? 1 : HueBounds.Contains(e.Location) ? 2 : 0;
        if (_dragZone != 0)
        {
            Track(e.Location);
            return;
        }

        for (int i = 0; i < Palette.Length; i++)
        {
            if (!SwatchBounds(i).Contains(e.Location))
                continue;

            ColorMath.ToHsv(Palette[i], out _hue, out _saturation, out _value);
            Publish();
            return;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragZone != 0)
            Track(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragZone = 0;
    }

    private void Track(Point point)
    {
        if (_dragZone == 1)
        {
            Rectangle square = SquareBounds;
            _saturation = Math.Clamp((point.X - square.X) / (float)square.Width, 0f, 1f);
            _value = 1f - Math.Clamp((point.Y - square.Y) / (float)square.Height, 0f, 1f);
        }
        else
        {
            Rectangle bar = HueBounds;
            _hue = Math.Clamp((point.X - bar.X) / (float)bar.Width, 0f, 1f) * 359.9f;
        }

        Publish();
    }

    private void Publish()
    {
        _syncingHex = true;
        _hex.Text = ColorMath.ToHex(Current);
        _syncingHex = false;
        Invalidate();
        Update();
        ColorPicked?.Invoke(Current);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Saturation (left to right) over the pure hue, then brightness (top to bottom) as a black veil.
        Rectangle square = SquareBounds;
        Rectangle brushArea = Rectangle.Inflate(square, 1, 1);
        using (GraphicsPath clip = FluentStyle.RoundedRectangle(square, LogicalToDeviceUnits(4)))
        {
            g.SetClip(clip);
            using (LinearGradientBrush saturation = new(brushArea, Color.White, ColorMath.FromHsv(_hue, 1f, 1f), LinearGradientMode.Horizontal))
                g.FillRectangle(saturation, square);
            using (LinearGradientBrush brightness = new(brushArea, Color.FromArgb(0, Color.Black), Color.Black, LinearGradientMode.Vertical))
                g.FillRectangle(brightness, square);
            g.ResetClip();
        }

        Rectangle bar = HueBounds;
        using (GraphicsPath clip = FluentStyle.RoundedRectangle(bar, bar.Height / 2f))
        using (LinearGradientBrush hue = new(Rectangle.Inflate(bar, 1, 1), Color.Red, Color.Red, LinearGradientMode.Horizontal))
        {
            hue.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.Red, Color.Yellow, Color.Lime, Color.Cyan, Color.Blue, Color.Magenta, Color.Red },
                Positions = new[] { 0f, 1 / 6f, 2 / 6f, 3 / 6f, 4 / 6f, 5 / 6f, 1f }
            };
            g.FillPath(hue, clip);
        }

        DrawThumb(g, square.X + (_saturation * square.Width), square.Y + ((1f - _value) * square.Height), Current);
        DrawThumb(g, bar.X + (_hue / 360f * bar.Width), bar.Y + (bar.Height / 2f), ColorMath.FromHsv(_hue, 1f, 1f));

        Color stroke = FluentComboBox.Border(theme);
        for (int i = 0; i < Palette.Length; i++)
        {
            Rectangle swatch = SwatchBounds(i);
            FluentStyle.FillRounded(g, Palette[i], swatch, LogicalToDeviceUnits(4));
            FluentStyle.DrawRounded(g, Palette[i].ToArgb() == Current.ToArgb() ? theme.AccentColor : stroke, swatch, LogicalToDeviceUnits(4),
                                    Palette[i].ToArgb() == Current.ToArgb() ? 2f : 1f);
        }

        Rectangle preview = new(Pad, FooterTop, LogicalToDeviceUnits(32), LogicalToDeviceUnits(32));
        FluentStyle.FillRounded(g, Current, preview, LogicalToDeviceUnits(6));
        FluentStyle.DrawRounded(g, stroke, preview, LogicalToDeviceUnits(6));
    }

    private void DrawThumb(Graphics g, float x, float y, Color fill)
    {
        float radius = LogicalToDeviceUnits(6);
        RectangleF bounds = new(x - radius, y - radius, radius * 2, radius * 2);
        using SolidBrush brush = new(fill);
        using Pen outer = new(Color.FromArgb(160, Color.Black), 3f);
        using Pen inner = new(Color.White, 1.6f);
        g.FillEllipse(brush, bounds);
        g.DrawEllipse(outer, bounds);
        g.DrawEllipse(inner, bounds);
    }
}

/// <summary>
/// Button that shows the current color as a swatch next to its caption and opens the color flyout.
/// <see cref="Value" /> may be null, meaning "inherit <see cref="Fallback" />" (the flyout then offers "Repor").
/// </summary>
public sealed class FluentColorButton : Control, IThemedControl
{
    private ToolStripDropDown _popup;
    private Color? _value;
    private bool _hovered;

    public FluentColorButton(string text, bool allowReset = false)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Text = text;
        AllowReset = allowReset;
        Cursor = Cursors.Hand;
        int textWidth = string.IsNullOrEmpty(text) ? 0 : TextRenderer.MeasureText(text, FluentStyle.UiFont).Width + LogicalToDeviceUnits(6);
        Size = new Size(LogicalToDeviceUnits(16 + 20) + textWidth, LogicalToDeviceUnits(32));
    }

    /// <summary>Raised on every change, including while the user drags inside the flyout.</summary>
    public event EventHandler ValueChanged;

    public bool AllowReset { get; }

    public bool IsPopupOpen => _popup is { Visible: true };

    /// <summary>Color shown while <see cref="Value" /> is null.</summary>
    public Func<Color> Fallback { get; set; }

    public Color? Value
    {
        get => _value;
        set { _value = value; Invalidate(); }
    }

    public Color EffectiveColor => _value ?? Fallback?.Invoke() ?? Color.White;

    public void ApplyTheme(Theme theme) => Invalidate();

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (IsPopupOpen)
            return;

        FluentColorPickerPanel panel = new(EffectiveColor, AllowReset && _value.HasValue);
        panel.ColorPicked += color => Set(color);
        panel.ResetRequested += () =>
        {
            Set(null);
            _popup?.Close();
        };

        ToolStripControlHost host = new(panel) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty, Size = panel.Size };
        ToolStripDropDown popup = new()
        {
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            AutoSize = false,
            Size = panel.Size,
            BackColor = panel.BackColor,
            DropShadowEnabled = true,
            Renderer = new FluentComboBox.PopupRenderer()
        };
        popup.Items.Add(host);
        popup.Closed += (_, _) =>
        {
            _popup = null;
            Invalidate();
            BeginInvoke(new Action(popup.Dispose));
        };

        // Below the button, or above it when there is no room on the screen.
        Rectangle screen = Screen.FromControl(this).WorkingArea;
        int gap = LogicalToDeviceUnits(4);
        bool fitsBelow = PointToScreen(new Point(0, Height + gap)).Y + panel.Height <= screen.Bottom;
        _popup = popup;
        popup.Show(this, new Point(0, fitsBelow ? Height + gap : -panel.Height - gap));
        FluentStyle.ApplyWindowChrome(popup.Handle, FluentComboBox.Border(Theme.Current), smallCorners: true);
    }

    private void Set(Color? color)
    {
        _value = color;
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? theme.BackgroundColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = LogicalToDeviceUnits(FluentStyle.ControlCornerLogical);
        RectangleF bounds = new(0.5f, 0.5f, Width - 1f, Height - 1f);
        bool active = _hovered || IsPopupOpen;
        FluentStyle.FillRounded(g, FluentComboBox.Fill(theme, active), bounds, radius);
        FluentStyle.DrawRounded(g, active ? FluentStyle.Blend(FluentComboBox.Border(theme), theme.AccentColor, 0.55) : FluentComboBox.Border(theme), bounds, radius);

        int size = LogicalToDeviceUnits(16);
        Rectangle swatch = new(LogicalToDeviceUnits(10), (Height - size) / 2, size, size);
        FluentStyle.FillRounded(g, Color.FromArgb(255, EffectiveColor), swatch, LogicalToDeviceUnits(4));
        FluentStyle.DrawRounded(g, FluentStyle.Blend(FluentComboBox.Border(theme), theme.ForegroundColor, 0.25), swatch, LogicalToDeviceUnits(4));

        // An inherited color is marked with a small dot so it reads as "automatic".
        if (AllowReset && !_value.HasValue)
        {
            float dot = LogicalToDeviceUnits(4);
            using SolidBrush brush = new(FluentStyle.RelativeLuminance(EffectiveColor) > 0.5 ? Color.Black : Color.White);
            g.FillEllipse(brush, swatch.X + ((size - dot) / 2f), swatch.Y + ((size - dot) / 2f), dot, dot);
        }

        if (Text.Length > 0)
        {
            Rectangle text = new(swatch.Right + LogicalToDeviceUnits(8), 0, Width - swatch.Right - LogicalToDeviceUnits(12), Height);
            TextRenderer.DrawText(g, Text, FluentStyle.UiFont, text, theme.ForegroundColor,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _popup?.Dispose();

        base.Dispose(disposing);
    }
}
