// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.DiskHealth;

/// <summary>
/// One drive: name and verdict badge, a row of four key figures (life, time powered on, data written,
/// temperature) and the reasons behind the verdict. Sizes its own height to the number of reasons.
/// </summary>
public sealed class DiskHealthCard : Control, IThemedControl
{
    private const int Pad = 16;
    private const int HeaderHeight = 40;
    private const int KpiHeight = 52;
    private const int LineHeight = 22;

    private readonly FluentBadge _badge;
    private DiskHealthReport _report;
    private Font _titleFont;
    private Font _detailFont;
    private Font _valueFont;

    public DiskHealthCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        _badge = new FluentBadge { Height = LogicalToDeviceUnits(24) };
        Controls.Add(_badge);
        Height = LogicalToDeviceUnits(Pad * 2 + HeaderHeight + KpiHeight + LineHeight);
    }

    private Font TitleFont => _titleFont ??= FluentStyle.CreateUiFont(FontStyle.Bold, 10.5f);
    private Font DetailFont => _detailFont ??= FluentStyle.CreateUiFont(FontStyle.Regular, 8.25f);
    private Font ValueFont => _valueFont ??= FluentStyle.CreateUiFont(FontStyle.Bold, 12f);

    public void SetReport(DiskHealthReport report)
    {
        _report = report;
        (string text, BadgeStyle style) = report.Level switch
        {
            DiskHealthLevel.Good => ("Bom", BadgeStyle.Success),
            DiskHealthLevel.Caution => ("Atenção", BadgeStyle.Warning),
            DiskHealthLevel.Bad => ("Mau", BadgeStyle.Danger),
            _ => ("Desconhecido", BadgeStyle.Neutral)
        };
        _badge.SetBadge(text, style);

        int lines = report.Findings.Count + (ShowsAllClear(report) ? 1 : 0);
        int height = LogicalToDeviceUnits(Pad * 2 + HeaderHeight + KpiHeight + 13 + (lines * LineHeight));
        if (Height != height)
            Height = height;

        PerformLayout();
        Invalidate();
    }

    public void ApplyTheme(Theme theme)
    {
        // The badge clears its corners with the parent's BackColor, so it must be the card surface.
        BackColor = theme.SurfaceColor;
        _badge.ApplyTheme(theme);
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        int pad = LogicalToDeviceUnits(Pad);
        _badge.Location = new Point(Width - pad - _badge.Width, pad + ((LogicalToDeviceUnits(HeaderHeight - 8) - _badge.Height) / 2));
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Theme theme = Theme.Current;
        e.Graphics.Clear(Parent?.BackColor ?? theme.BackgroundColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        float radius = Math.Max(4f, LogicalToDeviceUnits(theme.CornerRadius + 2));
        RectangleF bounds = new(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        FluentStyle.FillRounded(e.Graphics, theme.SurfaceColor, bounds, radius);
        FluentStyle.DrawRounded(e.Graphics, theme.SurfaceStrokeColor, bounds, radius);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_report == null)
            return;

        Theme theme = Theme.Current;
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        int pad = LogicalToDeviceUnits(Pad);
        int inner = Width - (pad * 2);
        int y = pad;

        // 1. Header: icon, name, description, badge (child control on the right).
        int icon = LogicalToDeviceUnits(20);
        g.DrawImage(FluentIcons.ForHardware(HardwareType.Storage), pad, y + LogicalToDeviceUnits(2), icon, icon);
        int textX = pad + icon + LogicalToDeviceUnits(10);
        int textWidth = Math.Max(0, _badge.Left - textX - LogicalToDeviceUnits(12));
        TextRenderer.DrawText(g, _report.Name, TitleFont, new Rectangle(textX, y, textWidth, TitleFont.Height), theme.ForegroundColor, flags);
        TextRenderer.DrawText(g, _report.Description, DetailFont, new Rectangle(textX, y + TitleFont.Height + LogicalToDeviceUnits(2), textWidth, DetailFont.Height), theme.MutedForegroundColor, flags);
        y += LogicalToDeviceUnits(HeaderHeight);

        // 2. Key figures.
        int columns = 4;
        int gap = LogicalToDeviceUnits(12);
        int column = (inner - (gap * (columns - 1))) / columns;
        // The bar follows the verdict, so a failing drive never shows a green bar.
        Color? lifeColor = _report.LifePercent switch
        {
            null => null,
            _ when _report.Level == DiskHealthLevel.Bad => theme.StateHotColor,
            _ when _report.Level == DiskHealthLevel.Caution => theme.StateWarmColor,
            <= 10 => theme.StateHotColor,
            <= 30 => theme.StateWarmColor,
            _ => theme.StateCoolColor
        };

        // SSD: wear reported by the drive. HDD: estimate from the sector counters.
        DrawKpi(g, theme, new Rectangle(pad, y, column, LogicalToDeviceUnits(KpiHeight)), _report.IsHdd ? "Saúde (estimada)" : "Vida (reportada pelo disco)",
            _report.LifePercent is int life ? $"{life}%" : "-", _report.LifePercent / 100f, lifeColor);
        DrawKpi(g, theme, new Rectangle(pad + (column + gap), y, column, LogicalToDeviceUnits(KpiHeight)), "Tempo ligado",
            _report.PowerOnHours is ulong hours ? DiskHealthFormat.Hours(hours) : "-", null, null,
            _report.PowerOnCount is ulong count ? $"{count} arranques" : null);
        string writesHint = _report.HostWritesBytes is null ? "Não reportado pelo disco"
            : _report.WritesEstimated ? "Média desde o arranque do Windows"
            : _report.HostReadsBytes is ulong read ? $"{DiskHealthFormat.Data(read)} lidos" : null;
        DrawKpi(g, theme, new Rectangle(pad + ((column + gap) * 2), y, column, LogicalToDeviceUnits(KpiHeight)),
            _report.WritesEstimated ? "Dados escritos (estimados)" : "Dados escritos",
            _report.HostWritesBytes is ulong written ? (_report.WritesEstimated ? "~" : string.Empty) + DiskHealthFormat.Data(written) : "-",
            null, null, writesHint);
        DrawKpi(g, theme, new Rectangle(pad + ((column + gap) * 3), y, column, LogicalToDeviceUnits(KpiHeight)), "Temperatura",
            _report.TemperatureC is int temp ? $"{temp} °C" : "-", null, null);
        y += LogicalToDeviceUnits(KpiHeight + 6);

        // 3. Reasons.
        using (Pen divider = new(theme.LineColor))
            g.DrawLine(divider, pad, y, Width - pad, y);
        y += LogicalToDeviceUnits(7);

        int dot = LogicalToDeviceUnits(6);
        int lineHeight = LogicalToDeviceUnits(LineHeight);
        if (ShowsAllClear(_report))
        {
            DrawDot(g, theme.StateCoolColor, pad, y, lineHeight, dot);
            TextRenderer.DrawText(g, "Sem problemas detetados nos dados SMART.", FluentStyle.UiFont,
                new Rectangle(pad + dot + LogicalToDeviceUnits(10), y, inner - dot, lineHeight), theme.ForegroundColor, flags | TextFormatFlags.VerticalCenter);
            y += lineHeight;
        }

        foreach (DiskFinding finding in _report.Findings)
        {
            Color color = finding.Severity switch
            {
                DiskFindingSeverity.Bad => theme.StateHotColor,
                DiskFindingSeverity.Caution => theme.StateWarmColor,
                _ => theme.MutedForegroundColor
            };
            DrawDot(g, color, pad, y, lineHeight, dot);
            Color text = finding.Severity == DiskFindingSeverity.Info ? theme.MutedForegroundColor : theme.ForegroundColor;
            TextRenderer.DrawText(g, finding.Text, FluentStyle.UiFont,
                new Rectangle(pad + dot + LogicalToDeviceUnits(10), y, inner - dot, lineHeight), text, flags | TextFormatFlags.VerticalCenter);
            y += lineHeight;
        }
    }

    private void DrawKpi(Graphics g, Theme theme, Rectangle area, string caption, string value, float? ratio, Color? barColor, string hint = null)
    {
        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, caption, DetailFont, new Rectangle(area.X, area.Y, area.Width, DetailFont.Height), theme.MutedForegroundColor, flags);
        int valueY = area.Y + DetailFont.Height + LogicalToDeviceUnits(2);
        TextRenderer.DrawText(g, value, ValueFont, new Rectangle(area.X, valueY, area.Width, ValueFont.Height), theme.ForegroundColor, flags);
        int belowY = valueY + ValueFont.Height + LogicalToDeviceUnits(4);

        if (ratio is float r && barColor is Color color)
        {
            int barHeight = LogicalToDeviceUnits(4);
            RectangleF track = new(area.X, belowY, Math.Min(area.Width, LogicalToDeviceUnits(140)), barHeight);
            FluentStyle.FillRounded(g, theme.LineColor, track, barHeight / 2f);
            float fill = track.Width * Math.Clamp(r, 0f, 1f);
            if (fill >= barHeight)
                FluentStyle.FillRounded(g, color, new RectangleF(track.X, track.Y, fill, barHeight), barHeight / 2f);
        }
        else if (!string.IsNullOrEmpty(hint))
        {
            TextRenderer.DrawText(g, hint, DetailFont, new Rectangle(area.X, belowY - LogicalToDeviceUnits(3), area.Width, DetailFont.Height), theme.MutedForegroundColor, flags);
        }
    }

    /// <summary>The green line is shown for a readable drive with nothing above an informative note.</summary>
    private static bool ShowsAllClear(DiskHealthReport report) =>
        report.Level == DiskHealthLevel.Good && report.Findings.All(f => f.Severity == DiskFindingSeverity.Info);

    private static void DrawDot(Graphics g, Color color, int x, int y, int lineHeight, int size)
    {
        using SolidBrush brush = new(color);
        g.FillEllipse(brush, x, y + ((lineHeight - size) / 2), size, size);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _detailFont?.Dispose();
            _valueFont?.Dispose();
        }

        base.Dispose(disposing);
    }
}
