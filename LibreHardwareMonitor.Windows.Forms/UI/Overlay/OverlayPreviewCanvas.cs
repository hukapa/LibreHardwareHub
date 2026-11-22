// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// Live preview of the HUD over a mock game backdrop. It paints with the same <see cref="OverlayRenderer" />
/// and the same live readings as the real overlay. Dragging a reading switches to the free layout,
/// starting from the positions currently on screen.
/// </summary>
internal sealed class OverlayPreviewCanvas : Control
{
    private const int MinLogicalHeight = 150;
    private const int MaxLogicalHeight = 360;
    private const int SnapPixels = 4;

    private readonly OverlayConfig _config;
    private readonly OverlayRenderer _renderer;
    private OverlayMetricItem _hovered;
    private OverlayMetricItem _dragged;
    private Point _dragOffset;
    private float _scale = 1f;

    public OverlayPreviewCanvas(OverlayConfig config, OverlayTelemetry telemetry)
    {
        _config = config;
        _renderer = new OverlayRenderer(config, telemetry, preview: true);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = LogicalToDeviceUnits(MinLogicalHeight);
    }

    /// <summary>Raised while a reading is dragged and when the drag ends.</summary>
    public event Action LayoutEdited;

    private int Inset => LogicalToDeviceUnits(16);

    /// <summary>Call after a configuration change: forgets cached widths and fits the canvas height to the HUD.</summary>
    public void ConfigChanged()
    {
        _renderer.Invalidate();
        RefreshPreview();
    }

    /// <summary>Call when new readings arrived.</summary>
    public void RefreshPreview()
    {
        if (!IsHandleCreated)
            return;

        using (Graphics g = CreateGraphics())
            _renderer.Layout(g);

        int wanted = Math.Clamp(_renderer.Size.Height + (Inset * 2), LogicalToDeviceUnits(MinLogicalHeight), LogicalToDeviceUnits(MaxLogicalHeight));
        if (wanted != Height && _dragged == null)
            Height = wanted;

        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RefreshPreview();
    }

    // HUD coordinates of a canvas point (the HUD is drawn at the inset, scaled down when it does not fit).
    private Point ToHud(Point point) => new((int)((point.X - Inset) / _scale), (int)((point.Y - Inset) / _scale));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        Point hud = ToHud(e.Location);
        OverlayMetricItem item = _renderer.HitTest(hud);
        if (item == null)
            return;

        if (_config.Layout != OverlayLayout.FreeCanvas)
        {
            _renderer.SeedFreePositions();
            _config.Layout = OverlayLayout.FreeCanvas;
        }

        _dragged = item;
        _dragOffset = new Point(hud.X - item.GridX, hud.Y - item.GridY);
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point hud = ToHud(e.Location);
        if (_dragged == null)
        {
            OverlayMetricItem hovered = _renderer.HitTest(hud);
            if (hovered != _hovered)
            {
                _hovered = hovered;
                Cursor = hovered != null ? Cursors.SizeAll : Cursors.Default;
                Invalidate();
            }

            return;
        }

        int x = Math.Max(0, (hud.X - _dragOffset.X) / SnapPixels * SnapPixels);
        int y = Math.Max(0, (hud.Y - _dragOffset.Y) / SnapPixels * SnapPixels);
        if (x == _dragged.GridX && y == _dragged.GridY)
            return;

        _dragged.GridX = x;
        _dragged.GridY = y;
        LayoutEdited?.Invoke();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragged == null)
            return;

        _dragged = null;
        Capture = false;
        TrimFreeMargins();
        LayoutEdited?.Invoke();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hovered != null)
        {
            _hovered = null;
            Invalidate();
        }
    }

    /// <summary>Moves all readings together so the free layout keeps a regular margin at its top-left corner.</summary>
    private void TrimFreeMargins()
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        foreach (OverlayMetricItem item in _config.MetricItems)
        {
            if (!item.Enabled)
                continue;

            minX = Math.Min(minX, item.GridX);
            minY = Math.Min(minY, item.GridY);
        }

        if (minX == int.MaxValue)
            return;

        int margin = _config.Spacing + 4;
        foreach (OverlayMetricItem item in _config.MetricItems)
        {
            item.GridX += margin - minX;
            item.GridY += margin - minY;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme theme = Theme.Current;
        Rectangle bounds = ClientRectangle;

        // Mock game scene: dark backdrop with a faint grid, so transparency and shadows can be judged.
        using (LinearGradientBrush backdrop = new(bounds, Color.FromArgb(34, 40, 52), Color.FromArgb(14, 16, 22), 60f))
            g.FillRectangle(backdrop, bounds);
        using (Pen mesh = new(Color.FromArgb(14, Color.White)))
        {
            int step = LogicalToDeviceUnits(32);
            for (int x = step; x < bounds.Width; x += step)
                g.DrawLine(mesh, x, 0, x, bounds.Height);
            for (int y = step; y < bounds.Height; y += step)
                g.DrawLine(mesh, 0, y, bounds.Width, y);
        }

        Size size = _renderer.Layout(g);
        if (size.IsEmpty)
        {
            TextRenderer.DrawText(g, "Nenhuma leitura ativa. Ative pelo menos uma em \"Leituras\".", FluentStyle.UiFont, bounds,
                                  Color.FromArgb(170, 175, 190), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        int inset = Inset;
        _scale = Math.Min(1f, Math.Min((bounds.Width - (inset * 2)) / (float)size.Width, (bounds.Height - (inset * 2)) / (float)size.Height));
        _scale = Math.Max(0.1f, _scale);

        GraphicsState state = g.Save();
        g.TranslateTransform(inset, inset);
        g.ScaleTransform(_scale, _scale);
        _renderer.Draw(g, Point.Empty);

        OverlayMetricItem marked = _dragged ?? _hovered;
        if (marked != null)
        {
            foreach (OverlayRenderer.Row row in _renderer.Rows)
            {
                if (row.Item != marked)
                    continue;

                using Pen outline = new(theme.AccentColor, 1.5f / _scale) { DashStyle = _dragged != null ? DashStyle.Solid : DashStyle.Dash };
                g.SmoothingMode = SmoothingMode.None;
                g.DrawRectangle(outline, Rectangle.Inflate(row.Bounds, 3, 1));
            }
        }

        g.Restore(state);

        string hint = _config.Layout == OverlayLayout.FreeCanvas
            ? "Disposição livre: arraste cada leitura"
            : "Arraste uma leitura para passar a disposição livre";
        if (_scale < 0.99f)
            hint += $"  ·  {_scale * 100f:F0}%";

        Rectangle hintBounds = new(0, bounds.Height - LogicalToDeviceUnits(22), bounds.Width - LogicalToDeviceUnits(10), LogicalToDeviceUnits(18));
        TextRenderer.DrawText(g, hint, FluentStyle.UiFontSmall, hintBounds, Color.FromArgb(130, 136, 150),
                              TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _renderer.Dispose();

        base.Dispose(disposing);
    }
}
