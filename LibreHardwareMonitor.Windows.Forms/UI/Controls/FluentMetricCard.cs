// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls;

public enum MetricCardSeverity
{
    Normal,
    Cool,
    Warm,
    Hot,
    Accent
}

/// <summary>
/// Modern Fluent KPI metric card replicating the design, typography,
/// layout and hover interaction of SummaryPanel.
/// Supports icons, large display value, detail line, progress bar and zero flickering.
/// </summary>
public sealed class FluentMetricCard : Control, IThemedControl
{
    private const int CardLogicalHeight = 104;

    private string _caption = string.Empty;
    private string _valueText = "--";
    private string _unitText = string.Empty;
    private string _detailText = string.Empty;
    private Image _icon;
    private float? _ratio;
    private MetricCardSeverity _severity = MetricCardSeverity.Normal;
    private bool _isHovered;

    private Font _valueFont;
    private Font _compactValueFont;
    private Font _detailFont;

    public FluentMetricCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Height = LogicalToDeviceUnits(CardLogicalHeight);
        Width = LogicalToDeviceUnits(160);
    }

    public Image Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                Invalidate();
            }
        }
    }

    public string Caption
    {
        get => _caption;
        set
        {
            if (_caption != value)
            {
                _caption = value ?? string.Empty;
                Invalidate();
            }
        }
    }

    public string ValueText
    {
        get => _valueText;
        set
        {
            if (_valueText != value)
            {
                _valueText = value ?? "--";
                Invalidate();
            }
        }
    }

    public string UnitText
    {
        get => _unitText;
        set
        {
            if (_unitText != value)
            {
                _unitText = value ?? string.Empty;
                Invalidate();
            }
        }
    }

    public string DetailText
    {
        get => _detailText;
        set
        {
            if (_detailText != value)
            {
                _detailText = value ?? string.Empty;
                Invalidate();
            }
        }
    }

    public float? Ratio
    {
        get => _ratio;
        set
        {
            if (_ratio != value)
            {
                _ratio = value;
                Invalidate();
            }
        }
    }

    public MetricCardSeverity Severity
    {
        get => _severity;
        set
        {
            if (_severity != value)
            {
                _severity = value;
                Invalidate();
            }
        }
    }

    public void SetMetric(string caption, string valueText, string unitText = "", string detailText = "", MetricCardSeverity severity = MetricCardSeverity.Normal, float? ratio = null, Image icon = null)
    {
        bool changed = _caption != caption || _valueText != valueText || _unitText != unitText ||
                       _detailText != detailText || _severity != severity || _ratio != ratio || _icon != icon;

        if (!changed)
            return;

        _caption = caption ?? string.Empty;
        _valueText = valueText ?? "--";
        _unitText = unitText ?? string.Empty;
        _detailText = detailText ?? string.Empty;
        _severity = severity;
        _ratio = ratio;
        if (icon != null)
            _icon = icon;

        Invalidate();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Cursor = Cursors.Hand;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        e.Graphics.Clear(Parent?.BackColor ?? theme.BackgroundColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = LogicalToDeviceUnits(theme.CornerRadius + 2);
        RectangleF card = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

        // Hover matches SummaryPanel exactly: only surface changes to HoverBackgroundColor, stroke stays SurfaceStrokeColor
        Color surface = _isHovered ? theme.HoverBackgroundColor : theme.SurfaceColor;
        FluentStyle.FillRounded(g, surface, card, radius);
        FluentStyle.DrawRounded(g, theme.SurfaceStrokeColor, card, radius);

        int pad = LogicalToDeviceUnits(14);
        int iconSize = LogicalToDeviceUnits(16);
        int x = pad;
        int innerWidth = Width - (pad * 2);
        int y = pad;

        // 1. Icon and Caption
        if (_icon != null)
        {
            g.DrawImage(_icon, new Rectangle(x, y, iconSize, iconSize));
            Rectangle titleRect = new(x + iconSize + LogicalToDeviceUnits(8), y - LogicalToDeviceUnits(2), innerWidth - iconSize - LogicalToDeviceUnits(8), iconSize + LogicalToDeviceUnits(4));
            TextRenderer.DrawText(g, _caption, FluentStyle.UiFont, titleRect, theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        else
        {
            Rectangle titleRect = new(x, y - LogicalToDeviceUnits(2), innerWidth, iconSize + LogicalToDeviceUnits(4));
            TextRenderer.DrawText(g, _caption, FluentStyle.UiFont, titleRect, theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // 2. Headline Value
        _valueFont ??= FluentStyle.CreateDisplayFont(17f);
        _detailFont ??= FluentStyle.CreateUiFont(FontStyle.Regular, 8.25f);

        Color valColor = _severity switch
        {
            MetricCardSeverity.Warm => theme.StateWarmColor,
            MetricCardSeverity.Hot => theme.StateHotColor,
            MetricCardSeverity.Cool => theme.StateCoolColor,
            MetricCardSeverity.Accent => theme.AccentColor,
            _ => theme.ForegroundColor
        };

        y += iconSize + LogicalToDeviceUnits(6);
        string formattedValue = string.IsNullOrEmpty(_unitText) ? _valueText : $"{_valueText} {_unitText}";
        Font valFont = _valueFont;
        if (formattedValue.Length > 8)
        {
            _compactValueFont ??= FluentStyle.CreateDisplayFont(12.5f);
            valFont = _compactValueFont;
        }
        Rectangle valueRect = new(x - LogicalToDeviceUnits(1), y, innerWidth, _valueFont.Height + LogicalToDeviceUnits(2));
        TextRenderer.DrawText(g, formattedValue, valFont, valueRect, valColor, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

        // 3. Detail line
        y = valueRect.Bottom + LogicalToDeviceUnits(2);
        Rectangle detailRect = new(x, y, innerWidth, _detailFont.Height + LogicalToDeviceUnits(2));
        TextRenderer.DrawText(g, _detailText, _detailFont, detailRect, theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        // 4. Progress bar (matching SummaryPanel bottom bar)
        if (_ratio.HasValue)
        {
            float barHeight = Math.Max(3f, LogicalToDeviceUnits(4));
            RectangleF track = new(x, Height - pad - barHeight + LogicalToDeviceUnits(2), innerWidth, barHeight);
            FluentStyle.FillRounded(g, FluentStyle.Blend(surface, theme.ForegroundColor, 0.1), track, barHeight / 2);

            float clampedRatio = Math.Max(0f, Math.Min(1f, _ratio.Value));
            if (clampedRatio > 0f)
            {
                RectangleF fill = new(track.X, track.Y, Math.Max(barHeight, track.Width * clampedRatio), barHeight);
                Color barColor = _severity switch
                {
                    MetricCardSeverity.Hot => theme.StateHotColor,
                    MetricCardSeverity.Warm => theme.StateWarmColor,
                    MetricCardSeverity.Cool => theme.StateCoolColor,
                    _ => theme.AccentColor
                };
                FluentStyle.FillRounded(g, barColor, fill, barHeight / 2);
            }
        }
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
