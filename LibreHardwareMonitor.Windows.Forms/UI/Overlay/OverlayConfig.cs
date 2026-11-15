// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>Everything the user can customize on the HUD, plus its persistence.</summary>
public sealed class OverlayConfig
{
    private const string Prefix = "overlay.";

    public const int LoadWarnPercent = 70;
    public const int LoadCritPercent = 90;

    public OverlayConfig()
    {
        ResetMetrics();
        ResetAppearance();
    }

    public bool Enabled { get; set; }
    public Keys Hotkey { get; set; } = Keys.Alt | Keys.R;
    public Keys LockHotkey { get; set; } = Keys.Alt | Keys.L;
    public bool ClickThrough { get; set; } = true;

    public OverlayAnchor Anchor { get; set; }
    public Point CustomPosition { get; set; } = new(20, 20);

    /// <summary>Index into <see cref="Screen.AllScreens" />; -1 is the primary screen.</summary>
    public int ScreenIndex { get; set; } = -1;

    /// <summary>Distance to the screen edges for the fixed anchors, in pixels.</summary>
    public int Margin { get; set; }

    public OverlayLayout Layout { get; set; }
    public int GridColumns { get; set; }
    public OverlayThemePreset ThemePreset { get; set; }

    public int BackgroundOpacity { get; set; }
    public float FontSize { get; set; }
    public int CornerRadius { get; set; }
    public int BorderThickness { get; set; }

    /// <summary>Inner padding and gap between readings, in pixels.</summary>
    public int Spacing { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BorderColor { get; set; }
    public Color LabelColor { get; set; }
    public Color ValueColor { get; set; }

    public int TempWarnC { get; set; }
    public int TempCritC { get; set; }
    public Color CoolColor { get; set; }
    public Color WarmColor { get; set; }
    public Color HotColor { get; set; }

    public bool ColorBySeverity { get; set; }
    public bool ShowProgressBars { get; set; }
    public bool ShowTextShadow { get; set; }
    public bool UseMonospaceNumbers { get; set; }
    public bool ShowLabels { get; set; }

    /// <summary>The readings in display order.</summary>
    public List<OverlayMetricItem> MetricItems { get; } = new();

    /// <summary>Restores every visual option (not the metric selection, hotkeys or position).</summary>
    public void ResetAppearance()
    {
        Layout = OverlayLayout.VerticalList;
        GridColumns = 2;
        FontSize = 10f;
        Spacing = 6;
        Margin = 20;
        TempWarnC = 70;
        TempCritC = 85;
        CoolColor = Color.FromArgb(80, 220, 110);
        WarmColor = Color.FromArgb(255, 190, 40);
        HotColor = Color.FromArgb(255, 75, 75);
        ColorBySeverity = true;
        ShowProgressBars = true;
        UseMonospaceNumbers = true;
        ShowLabels = true;
        ApplyPreset(OverlayThemePreset.FluentDark);

        foreach (OverlayMetricItem item in MetricItems)
        {
            item.CustomLabelColor = null;
            item.CustomValueColor = null;
            item.UseDynamicColor = true;
            item.ShowBar = true;
        }
    }

    public void ResetMetrics()
    {
        MetricItems.Clear();
        MetricItems.Add(new OverlayMetricItem("cpu_temp", "CPU Temp"));
        MetricItems.Add(new OverlayMetricItem("cpu_load", "CPU Carga"));
        MetricItems.Add(new OverlayMetricItem("cpu_power", "CPU Potência", false));
        MetricItems.Add(new OverlayMetricItem("cpu_clock", "CPU Clock", false));
        MetricItems.Add(new OverlayMetricItem("gpu_temp", "GPU Temp"));
        MetricItems.Add(new OverlayMetricItem("gpu_load", "GPU Carga"));
        MetricItems.Add(new OverlayMetricItem("gpu_power", "GPU Potência", false));
        MetricItems.Add(new OverlayMetricItem("gpu_clock", "GPU Clock", false));
        MetricItems.Add(new OverlayMetricItem("gpu_fan", "GPU Ventoinha", false));
        MetricItems.Add(new OverlayMetricItem("vram_used", "VRAM Usada", false));
        MetricItems.Add(new OverlayMetricItem("ram_used", "RAM Usada"));
        MetricItems.Add(new OverlayMetricItem("fan_rpm", "Ventoinha", false));
        MetricItems.Add(new OverlayMetricItem("net_speed", "Rede D/U", false));
        MetricItems.Add(new OverlayMetricItem("system_time", "Hora"));
    }

    public void ApplyPreset(OverlayThemePreset preset)
    {
        ThemePreset = preset;
        ShowTextShadow = true;
        switch (preset)
        {
            case OverlayThemePreset.FluentDark:
                SetFrame(80, 8, 1, Color.FromArgb(24, 24, 28), Color.FromArgb(60, 60, 72));
                LabelColor = Color.FromArgb(175, 180, 195);
                ValueColor = Color.FromArgb(245, 245, 250);
                break;

            case OverlayThemePreset.MinimalistPure:
                SetFrame(0, 0, 0, Color.Black, Color.Black);
                LabelColor = Color.FromArgb(215, 220, 230);
                ValueColor = Color.White;
                break;

            case OverlayThemePreset.CyberMatrix:
                SetFrame(85, 2, 1, Color.FromArgb(10, 16, 12), Color.FromArgb(0, 200, 120));
                LabelColor = Color.FromArgb(0, 180, 100);
                ValueColor = Color.FromArgb(0, 255, 150);
                break;

            case OverlayThemePreset.ClassicRtss:
                SetFrame(50, 0, 0, Color.Black, Color.Black);
                LabelColor = Color.FromArgb(255, 165, 0);
                ValueColor = Color.White;
                break;
        }
    }

    private void SetFrame(int opacity, int radius, int border, Color background, Color borderColor)
    {
        BackgroundOpacity = opacity;
        CornerRadius = radius;
        BorderThickness = border;
        BackgroundColor = background;
        BorderColor = borderColor;
    }

    public OverlayMetricItem Find(string id)
    {
        foreach (OverlayMetricItem item in MetricItems)
        {
            if (item.Id == id)
                return item;
        }

        return null;
    }

    public bool HasCustomSensor(string sensorId) => Find(sensorId) is { Enabled: true };

    /// <summary>Shows/hides a sensor on the HUD, adding it as a custom reading the first time.</summary>
    public void ToggleCustomSensor(string sensorId, string sensorName)
    {
        OverlayMetricItem existing = Find(sensorId);
        if (existing != null)
            existing.Enabled = !existing.Enabled;
        else
            MetricItems.Add(new OverlayMetricItem(sensorId, sensorName, true, true));
    }

    /// <summary>Moves a reading up (-1) or down (+1) in the display order.</summary>
    public bool Move(OverlayMetricItem item, int delta)
    {
        int index = MetricItems.IndexOf(item);
        int target = index + delta;
        if (index < 0 || target < 0 || target >= MetricItems.Count)
            return false;

        (MetricItems[index], MetricItems[target]) = (MetricItems[target], MetricItems[index]);
        return true;
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

        CustomPosition = new Point(settings.GetValue(Prefix + "pos_x", CustomPosition.X), settings.GetValue(Prefix + "pos_y", CustomPosition.Y));
        ScreenIndex = settings.GetValue(Prefix + "screen", ScreenIndex);
        Margin = Math.Clamp(settings.GetValue(Prefix + "margin", Margin), 0, 200);
        GridColumns = Math.Clamp(settings.GetValue(Prefix + "grid_columns", GridColumns), 2, 4);

        BackgroundOpacity = Math.Clamp(settings.GetValue(Prefix + "opacity", BackgroundOpacity), 0, 100);
        FontSize = Math.Clamp(settings.GetValue(Prefix + "font_size", FontSize), 8f, 28f);
        CornerRadius = Math.Clamp(settings.GetValue(Prefix + "radius", CornerRadius), 0, 16);
        BorderThickness = Math.Clamp(settings.GetValue(Prefix + "border", BorderThickness), 0, 4);
        Spacing = Math.Clamp(settings.GetValue(Prefix + "spacing", Spacing), 0, 16);

        BackgroundColor = Color.FromArgb(settings.GetValue(Prefix + "bg_color", BackgroundColor.ToArgb()));
        BorderColor = Color.FromArgb(settings.GetValue(Prefix + "border_color", BorderColor.ToArgb()));
        LabelColor = Color.FromArgb(settings.GetValue(Prefix + "label_color", LabelColor.ToArgb()));
        ValueColor = Color.FromArgb(settings.GetValue(Prefix + "value_color", ValueColor.ToArgb()));

        TempWarnC = Math.Clamp(settings.GetValue(Prefix + "temp_warn", TempWarnC), 30, 110);
        TempCritC = Math.Clamp(settings.GetValue(Prefix + "temp_crit", TempCritC), 30, 110);
        CoolColor = Color.FromArgb(settings.GetValue(Prefix + "cool_color", CoolColor.ToArgb()));
        WarmColor = Color.FromArgb(settings.GetValue(Prefix + "warm_color", WarmColor.ToArgb()));
        HotColor = Color.FromArgb(settings.GetValue(Prefix + "hot_color", HotColor.ToArgb()));

        ColorBySeverity = settings.GetValue(Prefix + "severity", ColorBySeverity);
        ShowProgressBars = settings.GetValue(Prefix + "progress_bars", ShowProgressBars);
        ShowTextShadow = settings.GetValue(Prefix + "text_shadow", ShowTextShadow);
        UseMonospaceNumbers = settings.GetValue(Prefix + "monospace", UseMonospaceNumbers);
        ShowLabels = settings.GetValue(Prefix + "labels", ShowLabels);

        LoadMetrics(settings.GetValue(Prefix + "metrics", string.Empty));
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
        settings.SetValue(Prefix + "screen", ScreenIndex);
        settings.SetValue(Prefix + "margin", Margin);
        settings.SetValue(Prefix + "grid_columns", GridColumns);

        settings.SetValue(Prefix + "opacity", BackgroundOpacity);
        settings.SetValue(Prefix + "font_size", FontSize);
        settings.SetValue(Prefix + "radius", CornerRadius);
        settings.SetValue(Prefix + "border", BorderThickness);
        settings.SetValue(Prefix + "spacing", Spacing);

        settings.SetValue(Prefix + "bg_color", BackgroundColor.ToArgb());
        settings.SetValue(Prefix + "border_color", BorderColor.ToArgb());
        settings.SetValue(Prefix + "label_color", LabelColor.ToArgb());
        settings.SetValue(Prefix + "value_color", ValueColor.ToArgb());

        settings.SetValue(Prefix + "temp_warn", TempWarnC);
        settings.SetValue(Prefix + "temp_crit", TempCritC);
        settings.SetValue(Prefix + "cool_color", CoolColor.ToArgb());
        settings.SetValue(Prefix + "warm_color", WarmColor.ToArgb());
        settings.SetValue(Prefix + "hot_color", HotColor.ToArgb());

        settings.SetValue(Prefix + "severity", ColorBySeverity);
        settings.SetValue(Prefix + "progress_bars", ShowProgressBars);
        settings.SetValue(Prefix + "text_shadow", ShowTextShadow);
        settings.SetValue(Prefix + "monospace", UseMonospaceNumbers);
        settings.SetValue(Prefix + "labels", ShowLabels);

        settings.SetValue(Prefix + "metrics", SaveMetrics());
    }

    // One reading per ';', fields split by '|':
    // id | enabled | title | custom/std | x | y | label color | value color | dynamic | bar | format
    private string SaveMetrics()
    {
        StringBuilder sb = new();
        foreach (OverlayMetricItem m in MetricItems)
        {
            if (sb.Length > 0)
                sb.Append(';');

            string title = (m.CustomTitle ?? string.Empty).Replace('|', '/').Replace(';', ',');
            sb.Append(m.Id).Append('|').Append(m.Enabled ? '1' : '0').Append('|').Append(title).Append('|')
              .Append(m.IsCustom ? "custom" : "std").Append('|').Append(m.GridX).Append('|').Append(m.GridY).Append('|')
              .Append(m.CustomLabelColor?.ToArgb().ToString() ?? string.Empty).Append('|')
              .Append(m.CustomValueColor?.ToArgb().ToString() ?? string.Empty).Append('|')
              .Append(m.UseDynamicColor ? '1' : '0').Append('|').Append(m.ShowBar ? '1' : '0').Append('|').Append(m.DisplayFormat);
        }

        return sb.ToString();
    }

    private void LoadMetrics(string saved)
    {
        if (string.IsNullOrEmpty(saved))
            return;

        // The saved sequence is the display order; built-in readings added in a newer version go to the end.
        List<OverlayMetricItem> ordered = new();
        foreach (string entry in saved.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] p = entry.Split('|');
            if (p.Length < 3)
                continue;

            OverlayMetricItem m = Find(p[0]);
            if (m == null && p.Length >= 4 && p[3] == "custom")
                m = new OverlayMetricItem(p[0], p[2], true, true);
            if (m == null || ordered.Contains(m))
                continue;

            m.Enabled = p[1] == "1";
            m.CustomTitle = p[2];
            if (p.Length >= 6 && int.TryParse(p[4], out int x) && int.TryParse(p[5], out int y))
            {
                m.GridX = x;
                m.GridY = y;
            }

            if (p.Length >= 8)
            {
                m.CustomLabelColor = int.TryParse(p[6], out int label) ? Color.FromArgb(label) : null;
                m.CustomValueColor = int.TryParse(p[7], out int value) ? Color.FromArgb(value) : null;
            }

            if (p.Length >= 10)
            {
                m.UseDynamicColor = p[8] != "0";
                m.ShowBar = p[9] != "0";
            }

            if (p.Length >= 11 && int.TryParse(p[10], out int format))
                m.DisplayFormat = format;

            ordered.Add(m);
        }

        foreach (OverlayMetricItem m in MetricItems)
        {
            if (!ordered.Contains(m))
                ordered.Add(m);
        }

        MetricItems.Clear();
        MetricItems.AddRange(ordered);
    }
}
