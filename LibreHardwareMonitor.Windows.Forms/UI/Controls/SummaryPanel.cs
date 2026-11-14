// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls;

/// <summary>
/// Strip of Fluent KPI cards (title, headline value, detail line and progress bar) that
/// adapts the number of visible cards to the available width.
/// </summary>
public sealed class SummaryPanel : Control, IThemedControl
{
    private const int CardLogicalHeight = 96;
    private const int CardLogicalMinWidth = 150;
    private const int GapLogical = 10;

    private readonly List<SummaryMetric> _metrics = new();
    private readonly List<Rectangle> _cardBounds = new();
    private Font _valueFont;
    private Font _detailFont;
    private int _hoverIndex = -1;

    public SummaryPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        Height = LogicalToDeviceUnits(CardLogicalHeight + GapLogical);
    }

    /// <summary>Raised with the card's hardware when a card is clicked.</summary>
    public event EventHandler<IHardware> CardClicked;

    /// <summary>Returns true when temperatures must be shown in Fahrenheit.</summary>
    public Func<bool> UseFahrenheit { get; set; } = () => false;

    public bool HasMetrics => _metrics.Count > 0;

    public int MetricCount => _metrics.Count;

    public void SetMetrics(IEnumerable<SummaryMetric> metrics)
    {
        _metrics.Clear();
        if (metrics != null)
            _metrics.AddRange(metrics);

        Invalidate();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        Invalidate();
    }

    private Font ValueFont => _valueFont ??= FluentStyle.CreateDisplayFont(16f);

    private Font DetailFont => _detailFont ??= FluentStyle.CreateUiFont(FontStyle.Regular, 8f);

    private void LayoutCards()
    {
        _cardBounds.Clear();
        if (_metrics.Count == 0 || Width <= 0)
            return;

        int gap = LogicalToDeviceUnits(GapLogical);
        int height = LogicalToDeviceUnits(CardLogicalHeight);
        int minWidth = LogicalToDeviceUnits(CardLogicalMinWidth);

        int maxPossible = Math.Max(1, (Width + gap) / (minWidth + gap));
        int count = Math.Max(1, Math.Min(_metrics.Count, maxPossible));
        int totalGaps = gap * (count - 1);
        int width = Math.Max(minWidth, (Width - totalGaps) / count);

        for (int i = 0; i < count && i < _metrics.Count; i++)
        {
            int x = i * (width + gap);
            _cardBounds.Add(new Rectangle(x, 0, width, height));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        try
        {
            Theme theme = Theme.Current;
            Graphics g = e.Graphics;
            g.Clear(theme.BackgroundColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            LayoutCards();

            int renderCount = Math.Min(_cardBounds.Count, _metrics.Count);
            for (int i = 0; i < renderCount; i++)
            {
                DrawCard(g, theme, _metrics[i], _cardBounds[i], i == _hoverIndex);
            }
        }
        catch
        {
            // Failsafe against unexpected GDI+ state during window minimizes/resizes
        }
    }

    private void DrawCard(Graphics g, Theme theme, SummaryMetric metric, Rectangle bounds, bool hover)
    {
        if (bounds.Width < 20 || bounds.Height < 20 || metric == null)
            return;

        float radius = LogicalToDeviceUnits(theme.CornerRadius + 2);
        RectangleF card = new(bounds.X + 0.5f, bounds.Y + 0.5f, Math.Max(1f, bounds.Width - 1.5f), Math.Max(1f, bounds.Height - 1.5f));
        Color surface = hover ? theme.HoverBackgroundColor : theme.SurfaceColor;
        FluentStyle.FillRounded(g, surface, card, radius);
        FluentStyle.DrawRounded(g, theme.SurfaceStrokeColor, card, radius);

        int pad = LogicalToDeviceUnits(12);
        int iconSize = LogicalToDeviceUnits(16);
        int x = bounds.X + pad;
        int innerWidth = Math.Max(10, bounds.Width - (pad * 2));
        int y = bounds.Y + pad;

        Image icon = IconFor(metric);
        if (icon != null)
            g.DrawImage(icon, new Rectangle(x, y, iconSize, iconSize));

        int titleOffset = icon != null ? iconSize + LogicalToDeviceUnits(8) : 0;
        Rectangle title = new(x + titleOffset, y - LogicalToDeviceUnits(2), Math.Max(10, innerWidth - titleOffset), iconSize + LogicalToDeviceUnits(4));
        TextRenderer.DrawText(g, metric.Title ?? string.Empty, FluentStyle.UiFont, title, theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        float? value = metric.Sensor?.Value;
        SeverityLevel level = metric.Sensor != null ? SensorSeverity.Classify(metric.Sensor) : SeverityLevel.None;
        Color valueColor = SensorSeverity.ColorFor(level, theme, theme.ForegroundColor);
        y += iconSize + LogicalToDeviceUnits(6);

        Rectangle valueRect = new(x - LogicalToDeviceUnits(1), y, innerWidth, ValueFont.Height + LogicalToDeviceUnits(2));
        string valueStr = metric.Sensor != null ? FormatValue(metric.Sensor.SensorType, value) : "--";
        TextRenderer.DrawText(g, valueStr, ValueFont, valueRect, valueColor, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

        y = valueRect.Bottom + LogicalToDeviceUnits(2);
        Rectangle detailRect = new(x, y, innerWidth, DetailFont.Height + LogicalToDeviceUnits(2));
        string detailText;
        try
        {
            detailText = metric.Detail?.Invoke() ?? metric.Hardware?.Name ?? string.Empty;
        }
        catch
        {
            detailText = metric.Hardware?.Name ?? string.Empty;
        }
        TextRenderer.DrawText(g, detailText, DetailFont, detailRect, theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // Progress bar
        if (metric.Sensor != null)
        {
            float? ratio = SensorSeverity.Ratio(metric.Sensor, value);
            if (ratio.HasValue && !float.IsNaN(ratio.Value) && ratio.Value > 0)
            {
                float clampedRatio = Math.Max(0f, Math.Min(1f, ratio.Value));
                float barHeight = Math.Max(3f, LogicalToDeviceUnits(4));
                RectangleF track = new(x, bounds.Bottom - pad - barHeight + LogicalToDeviceUnits(2), innerWidth, barHeight);
                FluentStyle.FillRounded(g, FluentStyle.Blend(surface, theme.ForegroundColor, 0.1), track, barHeight / 2);

                float fillW = Math.Max(barHeight, Math.Min(track.Width, track.Width * clampedRatio));
                RectangleF fill = new(track.X, track.Y, fillW, barHeight);
                Color barColor = SensorSeverity.ColorFor(level, theme, theme.AccentColor);
                FluentStyle.FillRounded(g, barColor, fill, barHeight / 2);
            }
        }
    }

    private static Image IconFor(SummaryMetric metric)
    {
        if (metric?.Sensor == null)
            return null;

        return metric.Sensor.SensorType is SensorType.Temperature or SensorType.Power or SensorType.Voltage
            ? FluentIcons.ForSensorType(metric.Sensor.SensorType)
            : FluentIcons.ForHardware(metric.Hardware?.HardwareType ?? HardwareType.Cpu);
    }

    private string FormatValue(SensorType type, float? value)
    {
        if (!value.HasValue || float.IsNaN(value.Value))
            return "-";

        CultureInfo culture = CultureInfo.CurrentCulture;
        float v = value.Value;
        return type switch
        {
            SensorType.Load => string.Format(culture, "{0:F0} %", v),
            SensorType.Temperature when UseFahrenheit() => string.Format(culture, "{0:F0} °F", (v * 1.8f) + 32f),
            SensorType.Temperature => string.Format(culture, "{0:F0} °C", v),
            SensorType.Power => string.Format(culture, "{0:F0} W", v),
            SensorType.Voltage => string.Format(culture, "{0:F2} V", v),
            _ => string.Format(culture, "{0:F1}", v)
        };
    }

    private int HitTest(Point point)
    {
        return _cardBounds.FindIndex(r => r.Contains(point));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = HitTest(e.Location);
        if (index == _hoverIndex)
            return;

        _hoverIndex = index;
        Cursor = index >= 0 && index < _metrics.Count ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverIndex = -1;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int index = HitTest(e.Location);
        if (e.Button == MouseButtons.Left && index >= 0 && index < _metrics.Count)
            CardClicked?.Invoke(this, _metrics[index].Hardware);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _valueFont?.Dispose();
            _detailFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
