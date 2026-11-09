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

/// <summary>Shared painting of the rounded input frame used by <see cref="FluentTextBox" /> and <see cref="FluentNumberBox" />.</summary>
internal static class InputFrame
{
    public static void Draw(Graphics g, Control control, bool hovered, bool focused)
    {
        Theme theme = Theme.Current;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(control.Parent?.BackColor ?? theme.BackgroundColor);

        float radius = Math.Max(3f, control.LogicalToDeviceUnits(FluentStyle.ControlCornerLogical));
        RectangleF bounds = new(0.5f, 0.5f, control.Width - 1f, control.Height - 1f);
        Color fill = control.Enabled ? FluentComboBox.Fill(theme, hovered) : theme.SurfaceColor;

        FluentStyle.FillRounded(g, fill, bounds, radius);
        FluentStyle.DrawRounded(g, FluentComboBox.Border(theme), bounds, radius);

        // Accent underline while the field has the keyboard focus, like Windows 11 text fields.
        if (focused)
        {
            float lineHeight = Math.Max(2f, control.LogicalToDeviceUnits(2));
            g.SetClip(new RectangleF(0, control.Height - lineHeight - 1, control.Width, lineHeight + 1));
            FluentStyle.DrawRounded(g, theme.AccentColor, bounds, radius, lineHeight);
            g.ResetClip();
        }
    }
}

/// <summary>Single-line text field drawn like the other Fluent controls (rounded, dark, accent underline on focus).</summary>
public sealed class FluentTextBox : Control, IThemedControl
{
    private readonly TextBox _box;
    private bool _hovered;

    public FluentTextBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        _box = new TextBox { BorderStyle = BorderStyle.None, Font = FluentStyle.UiFont };
        _box.GotFocus += (_, _) => Invalidate();
        _box.LostFocus += (_, _) => Invalidate();
        _box.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        Controls.Add(_box);

        Height = LogicalToDeviceUnits(32);
        Font = FluentStyle.UiFont;
        ApplyTheme(Theme.Current);
    }

    public override string Text
    {
        get => _box.Text;
        set => _box.Text = value ?? string.Empty;
    }

    public int MaxLength
    {
        get => _box.MaxLength;
        set => _box.MaxLength = value;
    }

    public void SelectAllText() => _box.SelectAll();

    public void ApplyTheme(Theme theme)
    {
        _box.BackColor = FluentComboBox.Fill(theme, false);
        _box.ForeColor = theme.ForegroundColor;
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        int padX = LogicalToDeviceUnits(10);
        int height = _box.PreferredHeight;
        _box.SetBounds(padX, Math.Max(0, (Height - height) / 2), Math.Max(10, Width - (padX * 2)), height);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _box.Enabled = Enabled;
        Invalidate();
    }

    // Clicking the frame outside the inner box still puts the caret in the field.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _box.Focus();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _box.Focus();
    }

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

    protected override void OnPaint(PaintEventArgs e) => InputFrame.Draw(e.Graphics, this, _hovered, _box.Focused);
}

/// <summary>
/// Integer stepper: value on the left, up/down chevrons on the right. Click the chevrons, use the arrow keys or the
/// mouse wheel, or type digits. Replaces the white system numeric box.
/// </summary>
public sealed class FluentNumberBox : Control, IThemedControl
{
    private int _value = 1;
    private int _minimum = 1;
    private int _maximum = 12;
    private string _typed = string.Empty;
    private int _hoverZone = -1; // 0 = up, 1 = down, 2 = field

    public FluentNumberBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.UserPaint | ControlStyles.Selectable, true);
        TabStop = true;
        Height = LogicalToDeviceUnits(32);
        Width = LogicalToDeviceUnits(96);
        Font = FluentStyle.UiFont;
    }

    public event EventHandler ValueChanged;

    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; Value = _value; }
    }

    public int Maximum
    {
        get => _maximum;
        set { _maximum = value; Value = _value; }
    }

    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
            if (clamped == _value)
                return;

            _value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyTheme(Theme theme) => Invalidate();

    private int ButtonsWidth => LogicalToDeviceUnits(26);

    private Rectangle UpZone => new(Width - ButtonsWidth - 1, 2, ButtonsWidth, (Height / 2) - 2);

    private Rectangle DownZone => new(Width - ButtonsWidth - 1, Height / 2, ButtonsWidth, (Height / 2) - 2);

    private int ZoneAt(Point p) => UpZone.Contains(p) ? 0 : (DownZone.Contains(p) ? 1 : (ClientRectangle.Contains(p) ? 2 : -1));

    private void Step(int delta)
    {
        _typed = string.Empty;
        Value = _value + delta;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int zone = ZoneAt(e.Location);
        if (zone != _hoverZone)
        {
            _hoverZone = zone;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverZone = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left)
            return;

        int zone = ZoneAt(e.Location);
        if (zone == 0) Step(1);
        else if (zone == 1) Step(-1);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Focused || ClientRectangle.Contains(e.Location))
            Step(e.Delta > 0 ? 1 : -1);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Up: Step(1); e.Handled = true; break;
            case Keys.Down: Step(-1); e.Handled = true; break;
            case Keys.PageUp: Step(5); e.Handled = true; break;
            case Keys.PageDown: Step(-5); e.Handled = true; break;
            case Keys.Home: Step(_minimum - _value); e.Handled = true; break;
            case Keys.End: Step(_maximum - _value); e.Handled = true; break;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!char.IsDigit(e.KeyChar))
            return;

        // Typing "1" then "2" gives 12; a digit that would exceed the maximum starts a new number.
        string next = _typed + e.KeyChar;
        if (!int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number > _maximum)
        {
            next = e.KeyChar.ToString();
            number = e.KeyChar - '0';
        }

        _typed = next;
        Value = number;
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
        _typed = string.Empty;
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
        InputFrame.Draw(g, this, _hoverZone >= 0, Focused);

        TextRenderer.DrawText(g, _value.ToString(CultureInfo.CurrentCulture), FluentStyle.UiFont,
                              new Rectangle(LogicalToDeviceUnits(11), 0, Width - ButtonsWidth - LogicalToDeviceUnits(14), Height),
                              Enabled ? theme.ForegroundColor : theme.MutedForegroundColor,
                              TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

        // Separator and the two chevrons, each with its own hover highlight.
        using (Pen divider = new(theme.LineColor))
            g.DrawLine(divider, UpZone.Left - 1, LogicalToDeviceUnits(7), UpZone.Left - 1, Height - LogicalToDeviceUnits(7));

        DrawChevron(g, theme, UpZone, true, _hoverZone == 0, _value < _maximum);
        DrawChevron(g, theme, DownZone, false, _hoverZone == 1, _value > _minimum);
    }

    private void DrawChevron(Graphics g, Theme theme, Rectangle zone, bool up, bool hovered, bool enabled)
    {
        if (hovered && enabled)
        {
            RectangleF back = new(zone.X + 2, zone.Y + 1, zone.Width - 4, zone.Height - 1);
            FluentStyle.FillRounded(g, FluentStyle.Blend(FluentComboBox.Fill(theme, true), theme.ForegroundColor, 0.1), back, LogicalToDeviceUnits(4));
        }

        float size = LogicalToDeviceUnits(3);
        float cx = zone.X + (zone.Width / 2f);
        float cy = zone.Y + (zone.Height / 2f);
        Color color = enabled ? (hovered ? theme.ForegroundColor : theme.MutedForegroundColor) : FluentStyle.Blend(theme.SurfaceColor, theme.MutedForegroundColor, 0.4);
        using Pen pen = new(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, up
            ? new[] { new PointF(cx - size, cy + (size / 2)), new PointF(cx, cy - (size / 2)), new PointF(cx + size, cy + (size / 2)) }
            : new[] { new PointF(cx - size, cy - (size / 2)), new PointF(cx, cy + (size / 2)), new PointF(cx + size, cy - (size / 2)) });
    }
}
