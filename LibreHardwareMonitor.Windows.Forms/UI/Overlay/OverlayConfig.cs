// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

public enum OverlayAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomRight,
    Custom
}

public enum OverlayLayout
{
    VerticalList,
    HorizontalBar,
    CompactGrid
}

public enum OverlayThemePreset
{
    FluentDark,
    MinimalistPure,
    CyberMatrix,
    ClassicRtss,
    Custom
}

public sealed class OverlayMetricItem
{
    public OverlayMetricItem(string id, string defaultTitle, bool enabled = true, int order = 0, bool isCustom = false)
    {
        Id = id;
        DefaultTitle = defaultTitle;
        CustomTitle = defaultTitle;
        Enabled = enabled;
        Order = order;
        IsCustom = isCustom;
    }

    public string Id { get; }
    public string DefaultTitle { get; }
    public string CustomTitle { get; set; }
    public bool Enabled { get; set; }
    public int Order { get; set; }
    public bool IsCustom { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(CustomTitle) ? DefaultTitle : CustomTitle;
}

public sealed class OverlayConfig
{
    private const string Prefix = "overlay.";

    public bool Enabled { get; set; }
    public Keys Hotkey { get; set; } = Keys.Alt | Keys.R;
    public Keys LockHotkey { get; set; } = Keys.Alt | Keys.L;
    public bool ClickThrough { get; set; } = true;
    public OverlayAnchor Anchor { get; set; } = OverlayAnchor.TopLeft;
    public Point CustomPosition { get; set; } = new Point(20, 20);
    public OverlayLayout Layout { get; set; } = OverlayLayout.VerticalList;
    public OverlayThemePreset ThemePreset { get; set; } = OverlayThemePreset.FluentDark;

    public int BackgroundOpacity { get; set; } = 80;
    public float FontSize { get; set; } = 10f;
    public int CornerRadius { get; set; } = 8;
    public int BorderThickness { get; set; } = 1;
    public int Compactness { get; set; } = 1;

    public Color BackgroundColor { get; set; } = Color.FromArgb(24, 24, 28);
    public Color BorderColor { get; set; } = Color.FromArgb(60, 60, 70);
    public Color LabelColor { get; set; } = Color.FromArgb(175, 180, 195);
    public Color ValueColor { get; set; } = Color.FromArgb(245, 245, 250);

    public bool ColorBySeverity { get; set; } = true;
    public bool ShowProgressBars { get; set; } = true;
    public bool ShowTextShadow { get; set; } = true;
    public bool UseMonospaceNumbers { get; set; } = true;

    public List<OverlayMetricItem> MetricItems { get; } = new();

    public OverlayConfig()
    {
        InitializeDefaultMetrics();
    }

    private void InitializeDefaultMetrics()
    {
        MetricItems.Clear();
        int order = 0;
        MetricItems.Add(new OverlayMetricItem("cpu_temp", "CPU Temp", true, order++));
        MetricItems.Add(new OverlayMetricItem("cpu_load", "CPU Carga", true, order++));
        MetricItems.Add(new OverlayMetricItem("cpu_power", "CPU Potência", false, order++));
        MetricItems.Add(new OverlayMetricItem("cpu_clock", "CPU Clock", false, order++));
        MetricItems.Add(new OverlayMetricItem("gpu_temp", "GPU Temp", true, order++));
        MetricItems.Add(new OverlayMetricItem("gpu_load", "GPU Carga", true, order++));
        MetricItems.Add(new OverlayMetricItem("gpu_power", "GPU Potência", false, order++));
        MetricItems.Add(new OverlayMetricItem("gpu_clock", "GPU Clock", false, order++));
        MetricItems.Add(new OverlayMetricItem("vram_used", "VRAM Usada", false, order++));
        MetricItems.Add(new OverlayMetricItem("ram_used", "RAM Usada", true, order++));
        MetricItems.Add(new OverlayMetricItem("fan_rpm", "Ventoinha", false, order++));
        MetricItems.Add(new OverlayMetricItem("net_speed", "Rede D/U", false, order++));
        MetricItems.Add(new OverlayMetricItem("system_time", "Hora", true, order++));
    }

    public void ApplyPreset(OverlayThemePreset preset)
    {
        ThemePreset = preset;
        switch (preset)
        {
            case OverlayThemePreset.FluentDark:
                BackgroundOpacity = 80;
                CornerRadius = 8;
                BorderThickness = 1;
                BackgroundColor = Color.FromArgb(24, 24, 28);
                BorderColor = Color.FromArgb(60, 60, 72);
                LabelColor = Color.FromArgb(175, 180, 195);
                ValueColor = Color.FromArgb(245, 245, 250);
                ShowTextShadow = true;
                break;

            case OverlayThemePreset.MinimalistPure:
                BackgroundOpacity = 0;
                CornerRadius = 0;
                BorderThickness = 0;
                BackgroundColor = Color.Black;
                BorderColor = Color.Transparent;
                LabelColor = Color.FromArgb(215, 220, 230);
                ValueColor = Color.White;
                ShowTextShadow = true;
                break;

            case OverlayThemePreset.CyberMatrix:
                BackgroundOpacity = 85;
                CornerRadius = 2;
                BorderThickness = 1;
                BackgroundColor = Color.FromArgb(10, 16, 12);
                BorderColor = Color.FromArgb(0, 200, 120);
                LabelColor = Color.FromArgb(0, 180, 100);
                ValueColor = Color.FromArgb(0, 255, 150);
                ShowTextShadow = true;
                break;

            case OverlayThemePreset.ClassicRtss:
                BackgroundOpacity = 50;
                CornerRadius = 0;
                BorderThickness = 0;
                BackgroundColor = Color.Black;
                BorderColor = Color.Transparent;
                LabelColor = Color.FromArgb(255, 165, 0);
                ValueColor = Color.White;
                ShowTextShadow = true;
                break;
        }
    }

    public bool HasCustomSensor(string sensorId)
    {
        return MetricItems.Any(m => m.Id == sensorId && m.Enabled);
    }

    public void ToggleCustomSensor(string sensorId, string sensorName)
    {
        OverlayMetricItem existing = MetricItems.FirstOrDefault(m => m.Id == sensorId);
        if (existing != null)
        {
            existing.Enabled = !existing.Enabled;
        }
        else
        {
            int order = MetricItems.Count;
            MetricItems.Add(new OverlayMetricItem(sensorId, sensorName, true, order, true));
        }
    }

    public void Load(PersistentSettings settings)
    {
        if (settings == null)
            return;

        Enabled = settings.GetValue(Prefix + "enabled", Enabled);
        Hotkey = GlobalHotkeyManager.ParseKeyCombo(settings.GetValue(Prefix + "hotkey", GlobalHotkeyManager.FormatKeyCombo(Hotkey)));
        LockHotkey = GlobalHotkeyManager.ParseKeyCombo(settings.GetValue(Prefix + "lock_hotkey", GlobalHotkeyManager.FormatKeyCombo(LockHotkey)));
        ClickThrough = settings.GetValue(Prefix + "click_through", ClickThrough);

        if (Enum.TryParse(settings.GetValue(Prefix + "anchor", Anchor.ToString()), out OverlayAnchor anchor))
            Anchor = anchor;
        if (Enum.TryParse(settings.GetValue(Prefix + "layout", Layout.ToString()), out OverlayLayout layout))
            Layout = layout;
        if (Enum.TryParse(settings.GetValue(Prefix + "theme", ThemePreset.ToString()), out OverlayThemePreset preset))
            ThemePreset = preset;

        CustomPosition = new Point(
            settings.GetValue(Prefix + "pos_x", CustomPosition.X),
            settings.GetValue(Prefix + "pos_y", CustomPosition.Y));

        BackgroundOpacity = Math.Clamp(settings.GetValue(Prefix + "opacity", BackgroundOpacity), 0, 100);
        FontSize = Math.Clamp(settings.GetValue(Prefix + "font_size", FontSize), 8f, 20f);
        CornerRadius = Math.Clamp(settings.GetValue(Prefix + "radius", CornerRadius), 0, 16);
        BorderThickness = Math.Clamp(settings.GetValue(Prefix + "border", BorderThickness), 0, 3);
        Compactness = Math.Clamp(settings.GetValue(Prefix + "compactness", Compactness), 0, 2);

        BackgroundColor = Color.FromArgb(settings.GetValue(Prefix + "bg_color", BackgroundColor.ToArgb()));
        BorderColor = Color.FromArgb(settings.GetValue(Prefix + "border_color", BorderColor.ToArgb()));
        LabelColor = Color.FromArgb(settings.GetValue(Prefix + "label_color", LabelColor.ToArgb()));
        ValueColor = Color.FromArgb(settings.GetValue(Prefix + "value_color", ValueColor.ToArgb()));

        ColorBySeverity = settings.GetValue(Prefix + "severity", ColorBySeverity);
        ShowProgressBars = settings.GetValue(Prefix + "progress_bars", ShowProgressBars);
        ShowTextShadow = settings.GetValue(Prefix + "text_shadow", ShowTextShadow);
        UseMonospaceNumbers = settings.GetValue(Prefix + "monospace", UseMonospaceNumbers);

        string savedMetrics = settings.GetValue(Prefix + "metrics", string.Empty);
        if (!string.IsNullOrEmpty(savedMetrics))
        {
            string[] items = savedMetrics.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in items)
            {
                string[] parts = item.Split('|');
                if (parts.Length >= 3)
                {
                    string id = parts[0];
                    bool en = parts[1] == "1";
                    string custom = parts[2];
                    OverlayMetricItem m = MetricItems.FirstOrDefault(x => x.Id == id);
                    if (m != null)
                    {
                        m.Enabled = en;
                        m.CustomTitle = custom;
                    }
                    else if (parts.Length >= 4 && parts[3] == "custom")
                    {
                        MetricItems.Add(new OverlayMetricItem(id, custom, en, MetricItems.Count, true));
                    }
                }
            }
        }
    }

    public void Save(PersistentSettings settings)
    {
        if (settings == null)
            return;

        settings.SetValue(Prefix + "enabled", Enabled);
        settings.SetValue(Prefix + "hotkey", GlobalHotkeyManager.FormatKeyCombo(Hotkey));
        settings.SetValue(Prefix + "lock_hotkey", GlobalHotkeyManager.FormatKeyCombo(LockHotkey));
        settings.SetValue(Prefix + "click_through", ClickThrough);
        settings.SetValue(Prefix + "anchor", Anchor.ToString());
        settings.SetValue(Prefix + "layout", Layout.ToString());
        settings.SetValue(Prefix + "theme", ThemePreset.ToString());

        settings.SetValue(Prefix + "pos_x", CustomPosition.X);
        settings.SetValue(Prefix + "pos_y", CustomPosition.Y);

        settings.SetValue(Prefix + "opacity", BackgroundOpacity);
        settings.SetValue(Prefix + "font_size", FontSize);
        settings.SetValue(Prefix + "radius", CornerRadius);
        settings.SetValue(Prefix + "border", BorderThickness);
        settings.SetValue(Prefix + "compactness", Compactness);

        settings.SetValue(Prefix + "bg_color", BackgroundColor.ToArgb());
        settings.SetValue(Prefix + "border_color", BorderColor.ToArgb());
        settings.SetValue(Prefix + "label_color", LabelColor.ToArgb());
        settings.SetValue(Prefix + "value_color", ValueColor.ToArgb());

        settings.SetValue(Prefix + "severity", ColorBySeverity);
        settings.SetValue(Prefix + "progress_bars", ShowProgressBars);
        settings.SetValue(Prefix + "text_shadow", ShowTextShadow);
        settings.SetValue(Prefix + "monospace", UseMonospaceNumbers);

        string serialized = string.Join(";", MetricItems.Select(m => $"{m.Id}|{(m.Enabled ? "1" : "0")}|{m.CustomTitle}|{(m.IsCustom ? "custom" : "std")}"));
        settings.SetValue(Prefix + "metrics", serialized);
    }
}
