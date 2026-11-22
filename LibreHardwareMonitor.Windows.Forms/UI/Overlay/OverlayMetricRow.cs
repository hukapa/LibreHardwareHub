// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// One reading that is shown on the HUD, as a compact row: grip, editable name, label and value colors, only the pills
/// that apply to the reading ("Cor auto", "Barra", memory format), move arrows and hide. Edits go straight to the
/// <see cref="OverlayMetricItem" />; structural changes (move, hide) are raised as events for the list to apply.
/// </summary>
public sealed class OverlayMetricRow : Control, IThemedControl
{
    private static readonly string[] MemoryFormats = { "GB + %", "Só GB", "Usado / Total" };

    private const int RowLogical = 36;
    private const int ControlLogical = 28;

    private readonly OverlayMetricItem _item;
    private readonly OverlayConfig _config;
    private readonly ToolTip _toolTip;
    private readonly OverlayDragHandle _grip = new();
    private readonly FluentTextBox _name = new();
    private readonly FluentColorButton _labelColor;
    private readonly FluentColorButton _valueColor;
    private readonly FluentButton _auto;
    private readonly FluentButton _bar;
    private readonly FluentButton _format;
    private readonly OverlayIconButton _up = new(OverlayGlyph.Up);
    private readonly OverlayIconButton _down = new(OverlayGlyph.Down);
    private readonly OverlayIconButton _hide = new(OverlayGlyph.Close);
    private bool _hovered;
    private bool _syncing;
    private bool _hasAuto;
    private bool _hasBar;
    private bool _hasFormat;
    private bool _built; // layout needs every control; setting Height in the constructor already asks for one

    public OverlayMetricRow(OverlayMetricItem item, OverlayConfig config, ToolTip toolTip, bool canMoveUp, bool canMoveDown)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        _item = item;
        _config = config;
        _toolTip = toolTip;
        Height = LogicalToDeviceUnits(RowLogical);

        _toolTip.SetToolTip(_grip, "Arrastar para reordenar");

        _name.Text = item.DisplayName;
        _name.MaxLength = 24;
        _name.Size = new Size(LogicalToDeviceUnits(150), LogicalToDeviceUnits(ControlLogical));
        _toolTip.SetToolTip(_name, item.IsCustom ? $"Sensor: {item.DefaultTitle}" : OverlayConfig.IsFpsMetric(item.Id)
            ? $"Leitura: {item.DefaultTitle}. Mede o jogo em primeiro plano enquanto o HUD está visível; sem jogo mostra --"
            : $"Leitura: {item.DefaultTitle}");
        _name.TextChanged += (_, _) =>
        {
            if (_syncing)
                return;

            item.CustomTitle = _name.Text;
            Edited?.Invoke(this, EventArgs.Empty);
        };

        _labelColor = NewColor("Nome", () => _config.LabelColor);
        _labelColor.ValueChanged += (_, _) =>
        {
            item.CustomLabelColor = _labelColor.Value;
            Edited?.Invoke(this, EventArgs.Empty);
        };

        _valueColor = NewColor("Valor", () => _config.ValueColor);
        _valueColor.ValueChanged += (_, _) =>
        {
            item.CustomValueColor = _valueColor.Value;

            // A fixed color is only visible when the automatic one is not overriding it.
            if (AutoColorApplies)
                item.UseDynamicColor = false;

            Sync();
            Edited?.Invoke(this, EventArgs.Empty);
        };

        _auto = NewPill("Cor auto");
        _toolTip.SetToolTip(_auto, SupportsAutoColor(item) && OverlayConfig.IsFpsMetric(item.Id)
            ? "O valor fica amarelo abaixo de 60 FPS (acima de 16,7 ms) e vermelho abaixo de 30 FPS (acima de 33,3 ms)"
            : "O valor muda de cor conforme a temperatura ou a carga");
        _auto.Click += (_, _) =>
        {
            item.UseDynamicColor = !item.UseDynamicColor;
            Sync();
            Edited?.Invoke(this, EventArgs.Empty);
        };

        _bar = NewPill("Barra");
        _bar.Click += (_, _) =>
        {
            item.ShowBar = !item.ShowBar;
            Sync();
            Edited?.Invoke(this, EventArgs.Empty);
        };

        _format = NewPill(MemoryFormats[0]);
        _toolTip.SetToolTip(_format, "Formato do valor");
        _format.Click += (_, _) =>
        {
            item.DisplayFormat = (item.DisplayFormat + 1) % MemoryFormats.Length;
            Sync();
            FormatChanged?.Invoke(this, EventArgs.Empty);
        };

        _up.Enabled = canMoveUp;
        _down.Enabled = canMoveDown;
        _toolTip.SetToolTip(_up, "Mover para cima");
        _toolTip.SetToolTip(_down, "Mover para baixo");
        _toolTip.SetToolTip(_hide, "Ocultar do HUD");
        _up.Click += (_, _) => MoveRequested?.Invoke(this, -1);
        _down.Click += (_, _) => MoveRequested?.Invoke(this, +1);
        _hide.Click += (_, _) => HideRequested?.Invoke(this, EventArgs.Empty);

        // Which pills apply is remembered in flags: Control.Visible is false for every control of a row that is not on screen
        // yet, which is exactly when the first layouts run.
        _hasAuto = SupportsAutoColor(item);
        _hasBar = SupportsBar(item);
        _hasFormat = HasFormat(item);
        _auto.Visible = _hasAuto;
        _bar.Visible = _hasBar;
        _format.Visible = _hasFormat;

        Controls.AddRange(new Control[] { _grip, _name, _labelColor, _valueColor, _auto, _bar, _format, _up, _down, _hide });

        // The row is "hovered" while the mouse is over it or over any of its controls.
        MouseEnter += (_, _) => UpdateHover();
        MouseLeave += (_, _) => UpdateHover();
        foreach (Control child in Controls)
        {
            child.MouseEnter += (_, _) => UpdateHover();
            child.MouseLeave += (_, _) => UpdateHover();
        }

        _built = true;
        Sync();
    }

    public OverlayMetricItem Item => _item;

    /// <summary>An edit that changes the HUD but not the structure of the list (name, color, pill).</summary>
    public event EventHandler Edited;

    /// <summary>The memory format changed: the reading must be formatted again.</summary>
    public event EventHandler FormatChanged;

    /// <summary>Move up (-1) or down (+1) by one position.</summary>
    public event EventHandler<int> MoveRequested;

    public event EventHandler HideRequested;

    // ---- What each reading supports ----

    /// <summary>Readings with a gauge: the percent of something (load, temperature, memory) can be drawn as a mini bar.</summary>
    public static bool SupportsBar(OverlayMetricItem item) =>
        item.IsCustom || item.Id is "cpu_temp" or "cpu_load" or "gpu_temp" or "gpu_load" or "ram_used" or "vram_used";

    /// <summary>Readings whose value color can follow their state (frame rates included, with inverted limits).</summary>
    public static bool SupportsAutoColor(OverlayMetricItem item) => SupportsBar(item) || OverlayConfig.IsFpsMetric(item.Id);

    public static bool HasFormat(OverlayMetricItem item) => item.Id is "ram_used" or "vram_used";

    private bool AutoColorApplies => SupportsAutoColor(_item) && _item.UseDynamicColor && _config.ColorBySeverity;

    private FluentColorButton NewColor(string caption, Func<Color> fallback) =>
        new(caption, allowReset: true) { Fallback = fallback, Height = LogicalToDeviceUnits(ControlLogical) };

    private FluentButton NewPill(string text) => PageMetrics.FitButton(new FluentButton { Text = text }, ControlLogical);

    /// <summary>Reads the item back into the controls (after a preset, a reset or an edit that affects other controls).</summary>
    public void Sync()
    {
        _syncing = true;
        if (!_name.Focused && _name.Text != _item.DisplayName)
            _name.Text = _item.DisplayName;

        _labelColor.Value = _item.CustomLabelColor;
        _valueColor.Value = _item.CustomValueColor;
        _toolTip.SetToolTip(_labelColor, "Cor do rótulo (o ponto indica que segue a cor geral)");

        bool dimmed = AutoColorApplies;
        _valueColor.Dimmed = dimmed;
        _toolTip.SetToolTip(_valueColor, dimmed
            ? "Cor automática por estado ativa - clique para cor fixa"
            : "Cor do valor (o ponto indica que segue a cor geral)");

        _auto.IsActive = _item.UseDynamicColor;
        _bar.IsActive = _item.ShowBar;
        _format.Text = MemoryFormats[Math.Clamp(_item.DisplayFormat, 0, MemoryFormats.Length - 1)];
        PageMetrics.FitButton(_format, ControlLogical);
        _syncing = false;
        PerformLayout();
    }

    // ---- Look ----

    public void ApplyTheme(Theme theme)
    {
        BackColor = _hovered ? FluentStyle.Blend(theme.SubtleSurfaceColor, theme.ForegroundColor, 0.04) : theme.SubtleSurfaceColor;
        foreach (Control child in Controls)
        {
            if (child is IThemedControl themed)
                themed.ApplyTheme(theme);
        }

        Invalidate();
    }

    private void UpdateHover()
    {
        bool over = IsHandleCreated && ClientRectangle.Contains(PointToClient(Cursor.Position));
        if (over == _hovered)
            return;

        _hovered = over;
        ApplyTheme(Theme.Current);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        if (!_built)
            return;

        int pad = LogicalToDeviceUnits(8);
        int gap = LogicalToDeviceUnits(6);
        int group = LogicalToDeviceUnits(12);

        // Right edge: up, down and hide, close together.
        int right = Width - pad;
        foreach (Control icon in new Control[] { _hide, _down, _up })
        {
            right -= icon.Width;
            icon.Location = new Point(right, (Height - icon.Height) / 2);
            right -= LogicalToDeviceUnits(2);
        }

        right -= group;

        // Everything between: grip, name (takes what is left, within limits), colors and the pills that apply.
        int fixedWidth = _labelColor.Width + gap + _valueColor.Width;
        if (_hasAuto)
            fixedWidth += gap + _auto.Width;
        if (_hasBar)
            fixedWidth += gap + _bar.Width;
        if (_hasFormat)
            fixedWidth += gap + _format.Width;

        int x = pad;
        _grip.Location = new Point(x, (Height - _grip.Height) / 2);
        x += _grip.Width + gap;

        int nameWidth = Math.Clamp(right - x - fixedWidth - group, LogicalToDeviceUnits(90), LogicalToDeviceUnits(170));
        _name.SetBounds(x, (Height - _name.Height) / 2, nameWidth, _name.Height);
        x += nameWidth + group;

        for (int i = 0; i < 5; i++)
        {
            Control c = i switch { 0 => _labelColor, 1 => _valueColor, 2 => _auto, 3 => _bar, _ => _format };
            if ((c == _auto && !_hasAuto) || (c == _bar && !_hasBar) || (c == _format && !_hasFormat))
                continue;

            c.Location = new Point(x, (Height - c.Height) / 2);
            x += c.Width + gap;
            if (c == _valueColor)
                x += group - gap;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.Clear(theme.SurfaceColor);
        if (Width <= 2 || Height <= 2)
            return;

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        FluentStyle.FillRounded(g, BackColor, new RectangleF(0, 0, Width, Height), LogicalToDeviceUnits(6));
    }
}
