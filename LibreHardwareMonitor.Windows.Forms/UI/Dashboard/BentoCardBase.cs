// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>
/// Base class for Bento hardware cards providing unified Fluent geometry,
/// header rendering, cached brushes, interactive click-to-expand capabilities,
/// and zero-flicker double-buffered painting.
/// </summary>
public abstract class BentoCardBase : Control, IThemedControl
{
    protected const int HeaderHeightLogical = 48;
    protected const int CornerRadiusLogical = 10;

    private bool _isHovered;
    private bool _isExpanded;

    public event EventHandler ExpansionChanged;

    protected BentoCardBase()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        Height = LogicalToDeviceUnits(260);
        Width = LogicalToDeviceUnits(400);
        Margin = new Padding(LogicalToDeviceUnits(6));
        Cursor = Cursors.Hand;
    }

    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public Image Icon { get; set; }
    public string BadgeText { get; set; } = string.Empty;
    public Color BadgeColor { get; set; } = Color.Empty;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                RecalculateCardHeight();
                ExpansionChanged?.Invoke(this, EventArgs.Empty);
                Parent?.PerformLayout();
                Invalidate();
            }
        }
    }

    protected bool IsHovered => _isHovered;

    public static float? ToGigabytes(float? val)
    {
        if (!val.HasValue)
            return null;

        float v = val.Value;
        if (v > 100_000_000f) // Raw Bytes (e.g. 16,840,245,248)
            return v / (1024f * 1024f * 1024f);
        if (v > 50_000f) // Kilobytes
            return v / (1024f * 1024f);
        if (v > 100f) // Megabytes (e.g. 16,384 or 6,144)
            return v / 1024f;

        return v; // Already Gigabytes
    }

    protected virtual void RecalculateCardHeight()
    {
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

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left)
        {
            IsExpanded = !IsExpanded;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 1 || Height <= 1)
            return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Theme theme = Theme.Current;
        RectangleF bounds = new(0, 0, Width - 1, Height - 1);
        float radius = LogicalToDeviceUnits(CornerRadiusLogical);

        using (GraphicsPath path = FluentStyle.RoundedRectangle(bounds, radius))
        {
            using SolidBrush bgBrush = new(theme.SurfaceColor);
            g.FillPath(bgBrush, path);

            Color strokeColor = _isHovered
                ? FluentStyle.Blend(theme.SurfaceStrokeColor, theme.AccentColor, 0.65)
                : theme.SurfaceStrokeColor;
            float strokeWidth = _isHovered ? 1.5f : 1f;

            using Pen strokePen = new(strokeColor, strokeWidth);
            g.DrawPath(strokePen, path);
        }

        DrawHeader(g, theme);
        DrawCardContent(g, theme);
    }

    protected virtual void DrawHeader(Graphics g, Theme theme)
    {
        int pad = LogicalToDeviceUnits(14);
        int iconBox = LogicalToDeviceUnits(28);
        int x = pad;

        if (Icon != null && Icon.Width > 0 && Icon.Height > 0)
        {
            Rectangle iconRect = new(x, pad, iconBox, iconBox);
            using (GraphicsPath ip = FluentStyle.RoundedRectangle(iconRect, 6f))
            {
                using SolidBrush ib = new(Color.FromArgb(theme.IsDark ? 28 : 14, theme.ForegroundColor));
                g.FillPath(ib, ip);
            }

            int iconPad = LogicalToDeviceUnits(4);
            g.DrawImage(Icon, x + iconPad, pad + iconPad, iconBox - iconPad * 2, iconBox - iconPad * 2);
            x += iconBox + LogicalToDeviceUnits(10);
        }

        int availWidth = Width - x - pad;
        if (!string.IsNullOrEmpty(BadgeText))
        {
            int badgeW = LogicalToDeviceUnits(74);
            int badgeH = LogicalToDeviceUnits(22);
            Rectangle badgeRect = new(Width - pad - badgeW, pad + 3, badgeW, badgeH);
            Color badgeBg = BadgeColor.IsEmpty ? theme.AccentColor : BadgeColor;

            using (GraphicsPath bPath = FluentStyle.RoundedRectangle(badgeRect, badgeH / 2f))
            {
                using SolidBrush bb = new(Color.FromArgb(35, badgeBg));
                g.FillPath(bb, bPath);
                using Pen bp = new(badgeBg, 1f);
                g.DrawPath(bp, bPath);
            }

            TextRenderer.DrawText(g, BadgeText, FluentStyle.UiFontSmallBold, badgeRect, badgeBg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            availWidth -= (badgeW + LogicalToDeviceUnits(10));
        }

        string titleText = string.IsNullOrEmpty(Title) ? " " : Title;
        Rectangle titleRect = new(x, pad, Math.Max(10, availWidth), LogicalToDeviceUnits(16));
        TextRenderer.DrawText(g, titleText, FluentStyle.UiFontSemibold, titleRect, theme.ForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        if (!string.IsNullOrEmpty(Subtitle))
        {
            Rectangle subRect = new(x, pad + LogicalToDeviceUnits(16), Math.Max(10, availWidth), LogicalToDeviceUnits(14));
            TextRenderer.DrawText(g, Subtitle, FluentStyle.UiFontSmall, subRect, theme.MutedForegroundColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        // Hairline subtle divider below header
        int divY = LogicalToDeviceUnits(HeaderHeightLogical) - LogicalToDeviceUnits(2);
        using Pen divPen = new(theme.LineColor, 1f);
        g.DrawLine(divPen, pad, divY, Width - pad, divY);

        // Subtle interactive hint at bottom right
        string hint = _isExpanded ? "Clique para recolher [^]" : "Clique para mais detalhes [v]";
        int hintW = LogicalToDeviceUnits(140);
        int hintH = LogicalToDeviceUnits(14);
        Rectangle hintRect = new(Width - pad - hintW, Height - pad / 2 - hintH, hintW, hintH);
        Color hintColor = _isHovered ? theme.AccentColor : theme.MutedForegroundColor;
        TextRenderer.DrawText(g, hint, FluentStyle.UiFontMini, hintRect, hintColor,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected abstract void DrawCardContent(Graphics g, Theme theme);

    protected void DrawSectionTitle(Graphics g, string title, int x, int y, Theme theme)
    {
        Rectangle titleRect = new(x, y, Math.Max(10, Width - x - LogicalToDeviceUnits(14)), LogicalToDeviceUnits(14));
        TextRenderer.DrawText(g, title, FluentStyle.UiFontSmallBold, titleRect, theme.MutedForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    protected void DrawProgressBar(Graphics g, Rectangle bounds, float ratio, Color fill, Color bg)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        using (GraphicsPath path = FluentStyle.RoundedRectangle(bounds, bounds.Height / 2f))
        {
            using SolidBrush bb = new(bg);
            g.FillPath(bb, path);
        }

        float clamped = Math.Max(0f, Math.Min(1f, ratio));
        if (clamped > 0.01f)
        {
            int fillW = Math.Max(bounds.Height, (int)(bounds.Width * clamped));
            Rectangle fillRect = new(bounds.X, bounds.Y, Math.Min(bounds.Width, fillW), bounds.Height);
            using GraphicsPath fPath = FluentStyle.RoundedRectangle(fillRect, bounds.Height / 2f);
            using SolidBrush fb = new(fill);
            g.FillPath(fb, fPath);
        }
    }

    protected void DrawKpiBox(Graphics g, Rectangle bounds, string label, string value, string unit, float? ratio, Color? valColor, Theme theme)
    {
        if (bounds.Width <= 10 || bounds.Height <= 10)
            return;

        int pad = LogicalToDeviceUnits(8);

        // 1. Sleek mini-card tile container
        using (GraphicsPath kp = FluentStyle.RoundedRectangle(bounds, 6f))
        {
            Color tileBg = Color.FromArgb(theme.IsDark ? 22 : 12, theme.ForegroundColor);
            using SolidBrush kb = new(tileBg);
            g.FillPath(kb, kp);

            Color tileStroke = Color.FromArgb(theme.IsDark ? 36 : 20, theme.ForegroundColor);
            using Pen pen = new(tileStroke, 1f);
            g.DrawPath(pen, kp);
        }

        // 2. Metric label at top
        Rectangle lblRect = new(bounds.X + pad, bounds.Y + pad - 2, bounds.Width - (pad * 2), LogicalToDeviceUnits(12));
        TextRenderer.DrawText(g, label?.ToUpperInvariant() ?? string.Empty, FluentStyle.UiFontMini, lblRect, theme.MutedForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        // 3. Metric value and unit
        Color textValColor = valColor ?? theme.ForegroundColor;
        string displayVal = value ?? "--";

        Rectangle valRect = new(bounds.X + pad, bounds.Y + LogicalToDeviceUnits(16), bounds.Width - (pad * 2), LogicalToDeviceUnits(18));
        if (string.IsNullOrEmpty(unit))
        {
            TextRenderer.DrawText(g, displayVal, FluentStyle.MonoFontKpi, valRect, textValColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else
        {
            Size valSize = TextRenderer.MeasureText(displayVal, FluentStyle.MonoFontKpi, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            Rectangle actualValRect = new(bounds.X + pad, bounds.Y + LogicalToDeviceUnits(16), valSize.Width, LogicalToDeviceUnits(18));
            TextRenderer.DrawText(g, displayVal, FluentStyle.MonoFontKpi, actualValRect, textValColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            int unitX = bounds.X + pad + valSize.Width + LogicalToDeviceUnits(3);
            int unitWidth = Math.Max(0, bounds.Right - pad - unitX);
            if (unitWidth > 4)
            {
                Rectangle unitRect = new(unitX, bounds.Y + LogicalToDeviceUnits(18), unitWidth, LogicalToDeviceUnits(14));
                TextRenderer.DrawText(g, unit, FluentStyle.UiFontSmall, unitRect, theme.MutedForegroundColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }

        // 4. Mini progress indicator
        if (ratio.HasValue)
        {
            int barH = LogicalToDeviceUnits(3);
            int barY = bounds.Bottom - pad - barH + 2;
            int barW = bounds.Width - pad * 2;
            if (barW > 4 && barH > 0)
            {
                Rectangle barRect = new(bounds.X + pad, barY, barW, barH);
                Color barFill = valColor ?? theme.AccentColor;
                Color barBg = Color.FromArgb(40, theme.MutedForegroundColor);
                DrawProgressBar(g, barRect, ratio.Value, barFill, barBg);
            }
        }
    }

    protected int DrawExpandedSensorList(Graphics g, int startY, IReadOnlyList<(string Name, string Value)> items, Theme theme)
    {
        if (!_isExpanded || items == null || items.Count == 0)
            return startY;

        int pad = LogicalToDeviceUnits(14);
        int availWidth = Width - (pad * 2);

        // Subtle divider
        using (Pen divPen = new(theme.LineColor, 1f))
            g.DrawLine(divPen, pad, startY, Width - pad, startY);

        int titleY = startY + LogicalToDeviceUnits(8);
        DrawSectionTitle(g, "SENSORES DETALHADOS", pad, titleY, theme);

        int listY = titleY + LogicalToDeviceUnits(18);
        int rowH = LogicalToDeviceUnits(22);
        int cols = availWidth > LogicalToDeviceUnits(480) ? 2 : 1;
        int colW = cols == 2 ? (availWidth - LogicalToDeviceUnits(12)) / 2 : availWidth;

        int rowCount = (int)Math.Ceiling(items.Count / (double)cols);

        for (int i = 0; i < items.Count; i++)
        {
            int c = i % cols;
            int r = i / cols;
            int rx = pad + (c * (colW + LogicalToDeviceUnits(12)));
            int ry = listY + (r * rowH);

            if (ry + rowH > Height - LogicalToDeviceUnits(20))
                break;

            Rectangle rowRect = new(rx, ry, colW, rowH);
            if (r % 2 == 1)
            {
                using SolidBrush zb = new(Color.FromArgb(theme.IsDark ? 16 : 8, theme.ForegroundColor));
                g.FillRectangle(zb, rowRect);
            }

            var (name, val) = items[i];

            int split = (colW * 55) / 100;
            Rectangle nameRect = new(rx + 4, ry, split - 6, rowH);
            Rectangle valRect = new(rx + split, ry, colW - split - 4, rowH);

            TextRenderer.DrawText(g, name, FluentStyle.UiFontSmall, nameRect, theme.MutedForegroundColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(g, val, FluentStyle.MonoFontSmall, valRect, theme.ForegroundColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        return listY + (rowCount * rowH) + LogicalToDeviceUnits(24);
    }

    public virtual void ApplyTheme(Theme theme)
    {
        Invalidate();
    }
}
