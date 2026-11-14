// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Dashboard;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// Lightweight in-game HUD overlay form using Win32 UpdateLayeredWindow
/// for per-pixel alpha transparency, WS_EX_NOACTIVATE to prevent focus stealing,
/// and WS_EX_TRANSPARENT for zero input lag click-through.
/// </summary>
public sealed class OverlayForm : Form
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;
    private const int ULW_ALPHA = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
        public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private readonly OverlayConfig _config;
    private readonly Dictionary<string, (string Value, float? Ratio, Color? SeverityColor)> _telemetry = new();
    private bool _isDragging;
    private Point _dragStartPoint;

    private Font _labelFont;
    private Font _valueFont;

    private Bitmap _cachedBitmap;
    private Graphics _cachedGraphics;
    private readonly SolidBrush _paintBrush = new(Color.White);
    private readonly Pen _paintPen = new(Color.White, 1f);

    public OverlayForm(OverlayConfig config)
    {
        _config = config;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        UpdateFonts();
        ApplyClickThrough();
    }

    public bool IsDraggableMode => !_config.ClickThrough;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WsExLayered | WsExToolWindow | WsExNoActivate;
            if (_config != null && _config.ClickThrough)
                cp.ExStyle |= WsExTransparent;
            return cp;
        }
    }

    public void ApplyClickThrough()
    {
        if (!IsHandleCreated || _config == null)
            return;

        IntPtr currentExStyle = IntPtr.Size == 8
            ? GetWindowLongPtr64(Handle, GwlExStyle)
            : new IntPtr(GetWindowLong32(Handle, GwlExStyle));

        long style = currentExStyle.ToInt64();
        if (_config.ClickThrough)
            style |= WsExTransparent;
        else
            style &= ~WsExTransparent;

        if (IntPtr.Size == 8)
            SetWindowLongPtr64(Handle, GwlExStyle, new IntPtr(style));
        else
            SetWindowLong32(Handle, GwlExStyle, (int)style);
    }

    public void UpdateFonts()
    {
        _labelFont?.Dispose();
        _valueFont?.Dispose();

        float size = _config?.FontSize ?? 10f;
        _labelFont = FluentStyle.CreateUiFont(FontStyle.Regular, size);

        if (_config?.UseMonospaceNumbers == true)
            _valueFont = FluentStyle.CreateMonoFont(size, FontStyle.Bold);
        else
            _valueFont = FluentStyle.CreateUiFont(FontStyle.Bold, size);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            UpdatePosition();
            RenderLayeredOverlay();
        }
    }

    public void RecalculateBounds()
    {
        UpdatePosition();
        RenderLayeredOverlay();
    }

    public void UpdatePosition()
    {
        Screen screenObj = Screen.PrimaryScreen ?? (Screen.AllScreens.Length > 0 ? Screen.AllScreens[0] : null);
        Rectangle screen = screenObj?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Point pos = _config.Anchor switch
        {
            OverlayAnchor.TopCenter => new Point(screen.X + ((screen.Width - Width) / 2), screen.Y + 20),
            OverlayAnchor.TopRight => new Point(screen.Right - Width - 20, screen.Y + 20),
            OverlayAnchor.BottomLeft => new Point(screen.X + 20, screen.Bottom - Height - 20),
            OverlayAnchor.BottomRight => new Point(screen.Right - Width - 20, screen.Bottom - Height - 20),
            OverlayAnchor.Custom => _config.CustomPosition,
            _ => new Point(screen.X + 20, screen.Y + 20)
        };

        Location = pos;
    }

    public void UpdateTelemetry(Computer computer)
    {
        if (!Visible || computer == null)
            return;

        List<ISensor> allSensors = new();
        foreach (IHardware hw in computer.Hardware)
            CollectHardwareSensors(hw, allSensors);

        foreach (IHardware hw in computer.Hardware)
        {
            if (hw.HardwareType == HardwareType.Cpu)
            {
                ISensor temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature &&
                    (s.Name.Contains("Package") || s.Name.Contains("Tctl") || s.Name.Contains("Core Max")))
                    ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                if (temp != null)
                    SetMetric("cpu_temp", FormatTemp(temp.Value), temp.Value / 100f, SeverityForTemp(temp.Value));

                ISensor load = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"));
                if (load != null)
                    SetMetric("cpu_load", $"{load.Value.GetValueOrDefault():F0} %", load.Value / 100f, SeverityForLoad(load.Value));

                ISensor power = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name.Contains("Package"))
                    ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power);
                if (power != null)
                    SetMetric("cpu_power", $"{power.Value.GetValueOrDefault():F0} W", null, null);

                ISensor clock = hw.Sensors.Where(s => s.SensorType == SensorType.Clock && !s.Name.Contains("Bus"))
                    .OrderByDescending(s => s.Value.GetValueOrDefault())
                    .FirstOrDefault() ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock);
                if (clock != null)
                    SetMetric("cpu_clock", $"{clock.Value.GetValueOrDefault():F0} MHz", null, null);
            }
            else if (hw.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            {
                ISensor temp = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Core"))
                               ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
                if (temp != null)
                    SetMetric("gpu_temp", FormatTemp(temp.Value), temp.Value / 100f, SeverityForTemp(temp.Value));

                ISensor load = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && (s.Name.Contains("Core") || s.Name.Contains("D3D 3D")))
                               ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load);
                if (load != null)
                    SetMetric("gpu_load", $"{load.Value.GetValueOrDefault():F0} %", load.Value / 100f, SeverityForLoad(load.Value));

                ISensor power = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && (s.Name.Contains("Package") || s.Name.Contains("GPU")))
                                ?? hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power);
                if (power != null)
                    SetMetric("gpu_power", $"{power.Value.GetValueOrDefault():F0} W", null, null);

                ISensor clock = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core"));
                if (clock != null)
                    SetMetric("gpu_clock", $"{clock.Value.GetValueOrDefault():F0} MHz", null, null);

                ISensor vram = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Memory Used"));
                if (vram != null)
                {
                    float? vramGb = BentoCardBase.ToGigabytes(vram.Value);
                    SetMetric("vram_used", $"{vramGb.GetValueOrDefault():F1} GB", null, null);
                }
            }
            else if (hw.HardwareType == HardwareType.Memory)
            {
                ISensor used = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Used"));
                ISensor avail = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("Available"));
                ISensor load = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Memory"));
                if (used != null)
                {
                    float? usedGb = BentoCardBase.ToGigabytes(used.Value);
                    float? availGb = BentoCardBase.ToGigabytes(avail?.Value);
                    float totalGb = usedGb.GetValueOrDefault() + availGb.GetValueOrDefault();
                    float pct = load?.Value ?? (totalGb > 0.1f ? (usedGb.GetValueOrDefault() / totalGb * 100f) : 0f);

                    var ramItem = _config.MetricItems.FirstOrDefault(m => m.Id == "ram_used");
                    int fmt = ramItem?.DisplayFormat ?? 0;
                    string ramText = fmt switch
                    {
                        1 => $"{usedGb.GetValueOrDefault():F1} GB",
                        2 when totalGb > 0.1f => $"{usedGb.GetValueOrDefault():F1} / {totalGb:F1} GB",
                        _ => $"{usedGb.GetValueOrDefault():F1} GB ({pct:F0}%)"
                    };

                    SetMetric("ram_used", ramText, pct / 100f, SeverityForLoad(pct));
                }
            }
            else if (hw.HardwareType == HardwareType.Network)
            {
                ISensor dl = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Throughput && s.Name.Contains("Download"));
                ISensor ul = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Throughput && s.Name.Contains("Upload"));
                if (dl != null || ul != null)
                {
                    float d = dl?.Value.GetValueOrDefault() ?? 0;
                    float u = ul?.Value.GetValueOrDefault() ?? 0;
                    SetMetric("net_speed", $"D:{FormatThroughput(d)} U:{FormatThroughput(u)}", null, null);
                }
            }
        }

        // Fan RPM from any SuperIO or Motherboard sensor
        ISensor fanSensor = allSensors.FirstOrDefault(s => s.SensorType == SensorType.Fan && s.Value > 0);
        if (fanSensor != null)
            SetMetric("fan_rpm", $"{fanSensor.Value.GetValueOrDefault():F0} RPM", null, null);

        foreach (OverlayMetricItem customItem in _config.MetricItems.Where(m => m.IsCustom && m.Enabled))
        {
            ISensor sensor = allSensors.FirstOrDefault(s => s.Identifier.ToString() == customItem.Id);
            if (sensor != null && sensor.Value.HasValue)
                SetMetric(customItem.Id, $"{sensor.Value.Value:F1}", null, null);
        }

        SetMetric("system_time", DateTime.Now.ToString("HH:mm:ss"), null, null);

        RenderLayeredOverlay();
    }

    private static void CollectHardwareSensors(IHardware hw, List<ISensor> allSensors)
    {
        if (hw == null)
            return;

        allSensors.AddRange(hw.Sensors);
        if (hw.SubHardware != null)
        {
            foreach (IHardware sub in hw.SubHardware)
                CollectHardwareSensors(sub, allSensors);
        }
    }

    private void SetMetric(string id, string text, float? ratio, Color? color)
    {
        _telemetry[id] = (text, ratio, color);
    }

    private static string FormatTemp(float? temp)
    {
        if (!temp.HasValue)
            return "-- °C";
        return $"{temp.Value:F0} °C";
    }

    private static string FormatThroughput(float bytesPerSec)
    {
        if (bytesPerSec < 1024 * 1024)
            return $"{bytesPerSec / 1024f:F0} K/s";
        return $"{bytesPerSec / (1024f * 1024f):F1} M/s";
    }

    private Color? SeverityForTemp(float? temp)
    {
        if (!_config.ColorBySeverity || !temp.HasValue)
            return null;
        if (temp.Value >= _config.TempCritC)
            return _config.HotColor;
        if (temp.Value >= _config.TempWarnC)
            return _config.WarmColor;
        return _config.CoolColor;
    }

    private Color? SeverityForLoad(float? load)
    {
        if (!_config.ColorBySeverity || !load.HasValue)
            return null;
        if (load.Value >= 90f)
            return _config.HotColor;
        if (load.Value >= 70f)
            return _config.WarmColor;
        return null;
    }

    public void RenderLayeredOverlay()
    {
        if (!IsHandleCreated || !Visible)
            return;

        var activeItems = _config.MetricItems.Where(m => m.Enabled).OrderBy(m => m.Order).ToList();
        if (activeItems.Count == 0)
        {
            Size = new Size(100, 30);
            return;
        }

        int padding = _config.Compactness switch { 0 => 4, 2 => 10, _ => 6 };
        int itemHeight = Math.Max(18, (int)(_config.FontSize * 1.8f) + padding);

        // Pre-measure all active items using offscreen graphics for zero wrapping and accurate bounds
        List<(OverlayMetricItem Item, string ValueText, float? Ratio, Color? Severity, int Width, int Height)> measured = new(activeItems.Count);
        using (Bitmap probeBmp = new(1, 1))
        using (Graphics probeG = Graphics.FromImage(probeBmp))
        {
            foreach (OverlayMetricItem item in activeItems)
            {
                _telemetry.TryGetValue(item.Id, out var data);
                string valText = string.IsNullOrEmpty(data.Value) ? "--" : data.Value;
                SizeF lblSize = probeG.MeasureString(item.DisplayName, _labelFont, PointF.Empty, FluentStyle.FormatNearCenter);
                SizeF valSize = probeG.MeasureString(valText, _valueFont, PointF.Empty, FluentStyle.FormatFarCenter);
                int reqW = (int)Math.Ceiling(lblSize.Width + 14 + valSize.Width);
                measured.Add((item, valText, data.Ratio, data.SeverityColor, Math.Max(80, reqW), itemHeight));
            }
        }

        int totalWidth, totalHeight;

        if (_config.Layout == OverlayLayout.FreeCanvas)
        {
            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < measured.Count; i++)
            {
                var m = measured[i];
                int right = m.Item.GridX + m.Width;
                int bottom = m.Item.GridY + m.Height;
                if (right > maxX) maxX = right;
                if (bottom > maxY) maxY = bottom;
            }
            totalWidth = Math.Max(100, maxX + padding);
            totalHeight = Math.Max(24, maxY + padding);
        }
        else if (_config.Layout == OverlayLayout.HorizontalBar)
        {
            int sumWidths = 0;
            for (int i = 0; i < measured.Count; i++)
                sumWidths += measured[i].Width;

            totalWidth = sumWidths + (padding * (measured.Count + 1));
            totalHeight = itemHeight + (padding * 2);
        }
        else if (_config.Layout == OverlayLayout.CompactGrid)
        {
            int col1W = 0;
            int col2W = 0;
            for (int i = 0; i < measured.Count; i++)
            {
                if (i % 2 == 0) col1W = Math.Max(col1W, measured[i].Width);
                else col2W = Math.Max(col2W, measured[i].Width);
            }
            int cols = measured.Count > 1 ? 2 : 1;
            int rows = (int)Math.Ceiling(measured.Count / (double)cols);
            totalWidth = (cols == 2 ? col1W + col2W : col1W) + (padding * (cols + 1));
            totalHeight = (rows * itemHeight) + (padding * 2);
        }
        else // VerticalList
        {
            int maxItemW = 0;
            for (int i = 0; i < measured.Count; i++)
                maxItemW = Math.Max(maxItemW, measured[i].Width);

            totalWidth = maxItemW + (padding * 2);
            totalHeight = (measured.Count * itemHeight) + (padding * 2);
        }

        int w = Math.Max(80, totalWidth);
        int h = Math.Max(24, totalHeight);

        if (w != Width || h != Height)
        {
            Size = new Size(w, h);
            UpdatePosition();
        }

        // Cache persistent bitmap to eliminate GC allocation pressure
        if (_cachedBitmap == null || _cachedBitmap.Width != w || _cachedBitmap.Height != h)
        {
            _cachedGraphics?.Dispose();
            _cachedBitmap?.Dispose();
            _cachedBitmap = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            _cachedGraphics = Graphics.FromImage(_cachedBitmap);
            _cachedGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            _cachedGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        }

        _cachedGraphics.Clear(Color.Transparent);

        int alpha = (int)(_config.BackgroundOpacity / 100f * 255);
        Color bg = Color.FromArgb(alpha, _config.BackgroundColor);

        RectangleF boundsF = new(0, 0, w - 1, h - 1);
        if (_config.CornerRadius > 0)
        {
            using GraphicsPath path = FluentStyle.RoundedRectangle(boundsF, _config.CornerRadius);
            if (alpha > 0)
            {
                _paintBrush.Color = bg;
                _cachedGraphics.FillPath(_paintBrush, path);
            }

            if (_config.BorderThickness > 0 && _config.BorderColor != Color.Transparent)
            {
                _paintPen.Color = _config.BorderColor;
                _paintPen.Width = _config.BorderThickness;
                _cachedGraphics.DrawPath(_paintPen, path);
            }
        }
        else
        {
            if (alpha > 0)
            {
                _paintBrush.Color = bg;
                _cachedGraphics.FillRectangle(_paintBrush, boundsF);
            }

            if (_config.BorderThickness > 0 && _config.BorderColor != Color.Transparent)
            {
                _paintPen.Color = _config.BorderColor;
                _paintPen.Width = _config.BorderThickness;
                _cachedGraphics.DrawRectangle(_paintPen, 0, 0, w - 1, h - 1);
            }
        }

        // Render each metric row with custom colors and zero-allocation brushes
        if (_config.Layout == OverlayLayout.FreeCanvas)
        {
            for (int i = 0; i < measured.Count; i++)
            {
                var m = measured[i];
                Rectangle itemRect = new(m.Item.GridX, m.Item.GridY, m.Width, m.Height);
                DrawMetricRow(_cachedGraphics, m.Item, m.ValueText, m.Ratio, m.Severity, itemRect);
            }
        }
        else if (_config.Layout == OverlayLayout.HorizontalBar)
        {
            int curX = padding;
            for (int i = 0; i < measured.Count; i++)
            {
                var m = measured[i];
                Rectangle itemRect = new(curX, padding, m.Width, itemHeight);
                DrawMetricRow(_cachedGraphics, m.Item, m.ValueText, m.Ratio, m.Severity, itemRect);
                curX += m.Width + padding;
            }
        }
        else if (_config.Layout == OverlayLayout.CompactGrid)
        {
            int col1W = 0;
            for (int i = 0; i < measured.Count; i += 2)
                col1W = Math.Max(col1W, measured[i].Width);

            for (int i = 0; i < measured.Count; i++)
            {
                var m = measured[i];
                int col = i % 2;
                int row = i / 2;
                int rx = col == 0 ? padding : padding + col1W + padding;
                Rectangle itemRect = new(rx, padding + (row * itemHeight), m.Width, itemHeight);
                DrawMetricRow(_cachedGraphics, m.Item, m.ValueText, m.Ratio, m.Severity, itemRect);
            }
        }
        else // VerticalList
        {
            int fullW = w - (padding * 2);
            for (int i = 0; i < measured.Count; i++)
            {
                var m = measured[i];
                Rectangle itemRect = new(padding, padding + (i * itemHeight), fullW, itemHeight);
                DrawMetricRow(_cachedGraphics, m.Item, m.ValueText, m.Ratio, m.Severity, itemRect);
            }
        }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hBitmap = _cachedBitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBitmap = SelectObject(memDc, hBitmap);

        try
        {
            BLENDFUNCTION blend = new()
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA
            };

            POINT topPos = new(Location.X, Location.Y);
            SIZE size = new(w, h);
            POINT pointZero = new(0, 0);

            UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref pointZero, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, oldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private void DrawMetricRow(Graphics g, OverlayMetricItem item, string valueText, float? ratio, Color? severityColor, Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        Color labelColor = item.CustomLabelColor ?? _config.LabelColor;
        Color valColor = (item.UseDynamicColor && severityColor.HasValue)
            ? severityColor.Value
            : (item.CustomValueColor ?? _config.ValueColor);

        string displayLabel = item.DisplayName ?? string.Empty;
        string displayVal = valueText ?? "--";

        SizeF lblSize = g.MeasureString(displayLabel, _labelFont, PointF.Empty, FluentStyle.FormatNearCenter);
        SizeF valSize = g.MeasureString(displayVal, _valueFont, PointF.Empty, FluentStyle.FormatFarCenter);

        int lblW = (int)Math.Ceiling(lblSize.Width);
        int valW = (int)Math.Ceiling(valSize.Width);

        Rectangle labelRect = new(rect.X, rect.Y, Math.Min(rect.Width, lblW + 4), rect.Height);
        Rectangle valRect = new(rect.Right - valW - 2, rect.Y, valW + 2, rect.Height);

        if (_config.ShowTextShadow)
        {
            _paintBrush.Color = Color.FromArgb(180, Color.Black);
            Rectangle labelShadow = new(labelRect.X + 1, labelRect.Y + 1, labelRect.Width, labelRect.Height);
            Rectangle valShadow = new(valRect.X + 1, valRect.Y + 1, valRect.Width, valRect.Height);
            g.DrawString(displayLabel, _labelFont, _paintBrush, labelShadow, FluentStyle.FormatNearCenter);
            g.DrawString(displayVal, _valueFont, _paintBrush, valShadow, FluentStyle.FormatFarCenter);
        }

        _paintBrush.Color = labelColor;
        g.DrawString(displayLabel, _labelFont, _paintBrush, labelRect, FluentStyle.FormatNearCenter);

        _paintBrush.Color = valColor;
        g.DrawString(displayVal, _valueFont, _paintBrush, valRect, FluentStyle.FormatFarCenter);

        if (_config.ShowProgressBars && item.ShowBar && ratio.HasValue)
        {
            int barH = Math.Max(2, (int)(_config.FontSize * 0.2f));
            int barY = rect.Bottom - barH - 1;

            _paintBrush.Color = Color.FromArgb(50, labelColor);
            g.FillRectangle(_paintBrush, rect.X, barY, rect.Width, barH);

            float fillW = Math.Max(0, Math.Min(rect.Width, rect.Width * ratio.Value));
            _paintBrush.Color = valColor;
            g.FillRectangle(_paintBrush, rect.X, barY, fillW, barH);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_config.ClickThrough && e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragStartPoint = e.Location;
            Capture = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging)
        {
            Point screenPos = PointToScreen(e.Location);
            Location = new Point(screenPos.X - _dragStartPoint.X, screenPos.Y - _dragStartPoint.Y);
            RenderLayeredOverlay();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            Capture = false;
            _config.Anchor = OverlayAnchor.Custom;
            _config.CustomPosition = Location;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cachedGraphics?.Dispose();
            _cachedBitmap?.Dispose();
            _paintBrush?.Dispose();
            _paintPen?.Dispose();
            _labelFont?.Dispose();
            _valueFont?.Dispose();
        }
        base.Dispose(disposing);
    }
}
