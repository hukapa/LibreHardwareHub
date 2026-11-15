// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// Measures, arranges and paints the HUD. The overlay window and the editor preview both use it, so the
/// preview is pixel-identical to what shows over the game. Fonts are sized in pixels: the result does not
/// depend on the DPI of the target surface. Reuses its fonts, brush and pen between frames.
/// </summary>
public sealed class OverlayRenderer : IDisposable
{
    public struct Row
    {
        public OverlayMetricItem Item;
        public OverlayReading Reading;
        public Rectangle Bounds;
        public int Width;
    }

    private readonly OverlayConfig _config;
    private readonly OverlayTelemetry _telemetry;
    private readonly List<Row> _rows = new();
    private readonly Dictionary<OverlayMetricItem, int> _valueWidths = new();
    private readonly SolidBrush _brush = new(Color.White);
    private readonly Pen _pen = new(Color.White);

    private Font _labelFont;
    private Font _valueFont;
    private float _fontSize;
    private bool _fontMono;
    private int _rowHeight;
    private int _barHeight;

    public OverlayRenderer(OverlayConfig config, OverlayTelemetry telemetry)
    {
        _config = config;
        _telemetry = telemetry;
    }

    /// <summary>Rows of the last <see cref="Layout" />, with bounds relative to the top-left of the HUD.</summary>
    public IReadOnlyList<Row> Rows => _rows;

    /// <summary>Size of the HUD computed by the last <see cref="Layout" />; empty when nothing is enabled.</summary>
    public Size Size { get; private set; }

    /// <summary>Forgets the remembered column widths; call after a configuration change.</summary>
    public void Invalidate() => _valueWidths.Clear();

    private void EnsureFonts()
    {
        if (_labelFont != null && _fontSize == _config.FontSize && _fontMono == _config.UseMonospaceNumbers)
            return;

        _labelFont?.Dispose();
        _valueFont?.Dispose();
        _fontSize = _config.FontSize;
        _fontMono = _config.UseMonospaceNumbers;

        float pixels = _fontSize * 96f / 72f;
        _labelFont = FluentStyle.CreateUiFont(FontStyle.Regular, pixels, GraphicsUnit.Pixel);
        _valueFont = _fontMono
            ? FluentStyle.CreateMonoFont(pixels, FontStyle.Bold, GraphicsUnit.Pixel)
            : FluentStyle.CreateUiFont(FontStyle.Bold, pixels, GraphicsUnit.Pixel);
        _valueWidths.Clear();
    }

    /// <summary>Measures every enabled reading and arranges it according to the configured layout.</summary>
    public Size Layout(Graphics g)
    {
        EnsureFonts();
        _rows.Clear();

        int spacing = _config.Spacing;
        int pad = spacing + 4;
        int labelGap = _config.ShowLabels ? (int)(_fontSize * 1.2f) + 4 : 0;
        int textHeight = (int)Math.Ceiling(Math.Max(_labelFont.GetHeight(g), _valueFont.GetHeight(g)));
        _barHeight = _config.ShowProgressBars ? Math.Max(2, (int)Math.Round(_fontSize * 0.2f)) : 0;
        _rowHeight = textHeight + _barHeight + Math.Max(2, spacing / 2);

        foreach (OverlayMetricItem item in _config.MetricItems)
        {
            if (!item.Enabled)
                continue;

            if (!_telemetry.TryGet(item.Id, out OverlayReading reading) || reading.Text == null)
                reading = new OverlayReading("--");

            int labelWidth = _config.ShowLabels ? (int)Math.Ceiling(g.MeasureString(item.DisplayName, _labelFont, PointF.Empty, FluentStyle.FormatNearCenter).Width) : 0;
            int valueWidth = (int)Math.Ceiling(g.MeasureString(reading.Text, _valueFont, PointF.Empty, FluentStyle.FormatFarCenter).Width);

            // A column only grows: the HUD does not change width every second as the digits of a value change.
            _valueWidths.TryGetValue(item, out int widest);
            if (valueWidth > widest)
                _valueWidths[item] = valueWidth;
            else
                valueWidth = widest;

            _rows.Add(new Row { Item = item, Reading = reading, Width = Math.Max(40, labelWidth + labelGap + valueWidth) });
        }

        if (_rows.Count == 0)
            return Size = Size.Empty;

        int width = 0;
        int height = 0;
        switch (_config.Layout)
        {
            case OverlayLayout.HorizontalBar:
            {
                int x = pad;
                for (int i = 0; i < _rows.Count; i++)
                {
                    Place(i, x, pad, _rows[i].Width);
                    x += _rows[i].Width + (spacing * 2) + 8;
                }

                width = x - (spacing * 2) - 8 + pad;
                height = _rowHeight + (pad * 2);
                break;
            }

            case OverlayLayout.CompactGrid:
            {
                int columns = Math.Min(_config.GridColumns, _rows.Count);
                int x = pad;
                for (int c = 0; c < columns; c++)
                {
                    int columnWidth = 0;
                    for (int i = c; i < _rows.Count; i += columns)
                        columnWidth = Math.Max(columnWidth, _rows[i].Width);
                    for (int i = c; i < _rows.Count; i += columns)
                        Place(i, x, pad + (i / columns * _rowHeight), columnWidth);

                    x += columnWidth + (spacing * 2) + 8;
                }

                width = x - (spacing * 2) - 8 + pad;
                height = (((_rows.Count + columns - 1) / columns) * _rowHeight) + (pad * 2);
                break;
            }

            case OverlayLayout.FreeCanvas:
                for (int i = 0; i < _rows.Count; i++)
                {
                    Place(i, Math.Max(0, _rows[i].Item.GridX), Math.Max(0, _rows[i].Item.GridY), _rows[i].Width);
                    width = Math.Max(width, _rows[i].Bounds.Right + pad);
                    height = Math.Max(height, _rows[i].Bounds.Bottom + pad);
                }

                break;

            default:
            {
                int columnWidth = 0;
                foreach (Row row in _rows)
                    columnWidth = Math.Max(columnWidth, row.Width);
                for (int i = 0; i < _rows.Count; i++)
                    Place(i, pad, pad + (i * _rowHeight), columnWidth);

                width = columnWidth + (pad * 2);
                height = (_rows.Count * _rowHeight) + (pad * 2);
                break;
            }
        }

        return Size = new Size(width, height);
    }

    private void Place(int index, int x, int y, int width)
    {
        Row row = _rows[index];
        row.Bounds = new Rectangle(x, y, width, _rowHeight);
        _rows[index] = row;
    }

    /// <summary>
    /// Copies the arranged positions into the items, so switching to <see cref="OverlayLayout.FreeCanvas" />
    /// starts from exactly what is on screen instead of jumping.
    /// </summary>
    public void SeedFreePositions()
    {
        foreach (Row row in _rows)
        {
            row.Item.GridX = row.Bounds.X;
            row.Item.GridY = row.Bounds.Y;
        }
    }

    /// <summary>The reading under <paramref name="point" /> (relative to the HUD), or null.</summary>
    public OverlayMetricItem HitTest(Point point)
    {
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (_rows[i].Bounds.Contains(point))
                return _rows[i].Item;
        }

        return null;
    }

    /// <summary>Paints the HUD of the last <see cref="Layout" /> with its top-left corner at <paramref name="origin" />.</summary>
    public void Draw(Graphics g, Point origin)
    {
        if (_rows.Count == 0)
            return;

        GraphicsState state = g.Save();
        g.TranslateTransform(origin.X, origin.Y);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // Grayscale anti-aliasing: ClearType needs an opaque background and fringes on a per-pixel alpha window.
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        DrawFrame(g);
        foreach (Row row in _rows)
            DrawRow(g, row);

        g.Restore(state);
    }

    private void DrawFrame(Graphics g)
    {
        int alpha = _config.BackgroundOpacity * 255 / 100;
        bool border = _config.BorderThickness > 0 && _config.BorderColor.A > 0;
        if (alpha == 0 && !border)
            return;

        float inset = border ? _config.BorderThickness / 2f : 0f;
        RectangleF bounds = new(inset, inset, Size.Width - (inset * 2) - 1, Size.Height - (inset * 2) - 1);
        using GraphicsPath path = FluentStyle.RoundedRectangle(bounds, _config.CornerRadius);
        if (alpha > 0)
        {
            _brush.Color = Color.FromArgb(alpha, _config.BackgroundColor);
            g.FillPath(_brush, path);
        }

        if (border)
        {
            _pen.Color = _config.BorderColor;
            _pen.Width = _config.BorderThickness;
            g.DrawPath(_pen, path);
        }
    }

    private void DrawRow(Graphics g, Row row)
    {
        OverlayMetricItem item = row.Item;
        Color labelColor = item.CustomLabelColor ?? _config.LabelColor;
        Color valueColor = item.CustomValueColor ?? _config.ValueColor;
        if (_config.ColorBySeverity && item.UseDynamicColor)
        {
            valueColor = row.Reading.Severity switch
            {
                OverlaySeverity.Cool => _config.CoolColor,
                OverlaySeverity.Warm => _config.WarmColor,
                OverlaySeverity.Hot => _config.HotColor,
                _ => valueColor
            };
        }

        RectangleF text = new(row.Bounds.X, row.Bounds.Y, row.Bounds.Width, row.Bounds.Height - _barHeight);
        if (_config.ShowTextShadow)
        {
            RectangleF shadow = text;
            shadow.Offset(1f, 1f);
            _brush.Color = Color.FromArgb(180, Color.Black);
            DrawTexts(g, item, row.Reading.Text, shadow);
        }

        _brush.Color = labelColor;
        if (_config.ShowLabels)
            g.DrawString(item.DisplayName, _labelFont, _brush, text, FluentStyle.FormatNearCenter);

        _brush.Color = valueColor;
        g.DrawString(row.Reading.Text, _valueFont, _brush, text, FluentStyle.FormatFarCenter);

        if (_barHeight > 0 && item.ShowBar && row.Reading.Ratio is float ratio)
        {
            int y = row.Bounds.Bottom - _barHeight - 1;
            _brush.Color = Color.FromArgb(50, labelColor);
            g.FillRectangle(_brush, row.Bounds.X, y, row.Bounds.Width, _barHeight);
            _brush.Color = valueColor;
            g.FillRectangle(_brush, row.Bounds.X, y, row.Bounds.Width * Math.Clamp(ratio, 0f, 1f), _barHeight);
        }
    }

    private void DrawTexts(Graphics g, OverlayMetricItem item, string value, RectangleF bounds)
    {
        if (_config.ShowLabels)
            g.DrawString(item.DisplayName, _labelFont, _brush, bounds, FluentStyle.FormatNearCenter);

        g.DrawString(value, _valueFont, _brush, bounds, FluentStyle.FormatFarCenter);
    }

    public void Dispose()
    {
        _labelFont?.Dispose();
        _valueFont?.Dispose();
        _brush.Dispose();
        _pen.Dispose();
    }
}
