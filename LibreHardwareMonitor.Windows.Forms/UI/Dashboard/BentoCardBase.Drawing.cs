// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>Drawing helpers shared by the card summaries: KPI tiles, section captions, usage bars and badges.</summary>
public abstract partial class BentoCardBase
{
    /// <summary>Splits <paramref name="area" /> into a 2-column grid of KPI tiles and returns tile <paramref name="index" />.</summary>
    protected Rectangle KpiTile(Rectangle area, int index, int tileHeightLogical = 48)
    {
        int gapX = LogicalToDeviceUnits(10);
        int gapY = LogicalToDeviceUnits(8);
        int width = (area.Width - gapX) / 2;
        int height = LogicalToDeviceUnits(tileHeightLogical);
        return new Rectangle(area.X + (index % 2 * (width + gapX)), area.Y + (index / 2 * (height + gapY)), width, height);
    }

    /// <summary>Theme color for a value against a warning and a critical threshold.</summary>
    protected static Color? StateColor(Theme theme, float? value, float warn, float critical)
    {
        if (!value.HasValue)
            return null;

        return value.Value >= critical ? theme.StateHotColor : value.Value >= warn ? theme.StateWarmColor : theme.StateCoolColor;
    }

    /// <summary>Sets the header badge from a value and its thresholds.</summary>
    protected void SetBadge(float? value, float warn, float critical, string normal, string warning, string criticalText)
    {
        if (!value.HasValue)
            return;

        (BadgeText, BadgeColor) = value.Value >= critical ? (criticalText, Color.FromArgb(235, 60, 60))
            : value.Value >= warn ? (warning, Color.FromArgb(240, 180, 30))
            : (normal, Color.FromArgb(70, 200, 100));
    }

    protected void DrawSectionTitle(Graphics g, string title, Rectangle area, int y, Theme theme, string rightText = null)
    {
        Rectangle bounds = new(area.X, y, area.Width, LogicalToDeviceUnits(14));
        TextRenderer.DrawText(g, title, FluentStyle.UiFontSmallBold, bounds, theme.MutedForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (rightText != null)
        {
            TextRenderer.DrawText(g, rightText, FluentStyle.MonoFont, bounds, theme.ForegroundColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Usage bar under a section title; the fill turns warm/hot past the given ratios.</summary>
    protected void DrawUsageBar(Graphics g, Theme theme, Rectangle area, int y, float ratio, float warn, float critical)
    {
        Rectangle bounds = new(area.X, y, area.Width, LogicalToDeviceUnits(8));
        Color fill = ratio > critical ? theme.StateHotColor : ratio > warn ? theme.StateWarmColor : theme.AccentColor;
        DrawProgressBar(g, bounds, ratio, fill, Color.FromArgb(40, theme.MutedForegroundColor));
    }

    protected void DrawProgressBar(Graphics g, Rectangle bounds, float ratio, Color fill, Color background)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        FluentStyle.FillRounded(g, background, bounds, bounds.Height / 2f);
        float clamped = Math.Clamp(ratio, 0f, 1f);
        if (clamped > 0.01f)
        {
            int width = Math.Min(bounds.Width, Math.Max(bounds.Height, (int)(bounds.Width * clamped)));
            FluentStyle.FillRounded(g, fill, new Rectangle(bounds.X, bounds.Y, width, bounds.Height), bounds.Height / 2f);
        }
    }

    protected void DrawKpiBox(Graphics g, Rectangle bounds, string label, string value, string unit, float? ratio, Color? valueColor, Theme theme)
    {
        if (bounds.Width <= 10 || bounds.Height <= 10)
            return;

        int pad = LogicalToDeviceUnits(8);
        FluentStyle.FillRounded(g, Color.FromArgb(theme.IsDark ? 22 : 12, theme.ForegroundColor), bounds, LogicalToDeviceUnits(6));
        FluentStyle.DrawRounded(g, Color.FromArgb(theme.IsDark ? 36 : 20, theme.ForegroundColor), bounds, LogicalToDeviceUnits(6));

        Rectangle labelBounds = new(bounds.X + pad, bounds.Y + pad - 2, bounds.Width - (pad * 2), LogicalToDeviceUnits(12));
        TextRenderer.DrawText(g, label.ToUpperInvariant(), FluentStyle.UiFontMini, labelBounds, theme.MutedForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        string text = value ?? "--";
        Size valueSize = TextRenderer.MeasureText(text, FluentStyle.MonoFontKpi, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        Rectangle valueBounds = new(bounds.X + pad, bounds.Y + LogicalToDeviceUnits(16), Math.Min(valueSize.Width, bounds.Width - (pad * 2)), LogicalToDeviceUnits(18));
        TextRenderer.DrawText(g, text, FluentStyle.MonoFontKpi, valueBounds, valueColor ?? theme.ForegroundColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        int unitX = valueBounds.Right + LogicalToDeviceUnits(3);
        if (!string.IsNullOrEmpty(unit) && bounds.Right - pad - unitX > 4)
        {
            TextRenderer.DrawText(g, unit, FluentStyle.UiFontSmall, new Rectangle(unitX, bounds.Y + LogicalToDeviceUnits(18), bounds.Right - pad - unitX, LogicalToDeviceUnits(14)),
                theme.MutedForegroundColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        if (ratio.HasValue)
        {
            int barHeight = LogicalToDeviceUnits(3);
            Rectangle bar = new(bounds.X + pad, bounds.Bottom - pad - barHeight + 2, bounds.Width - (pad * 2), barHeight);
            DrawProgressBar(g, bar, ratio.Value, valueColor ?? theme.AccentColor, Color.FromArgb(40, theme.MutedForegroundColor));
        }
    }
}
