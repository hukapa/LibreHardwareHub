// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System.Drawing;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

public enum OverlayAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
    Custom
}

public enum OverlayLayout
{
    VerticalList,
    HorizontalBar,
    CompactGrid,
    FreeCanvas
}

public enum OverlayThemePreset
{
    FluentDark,
    MinimalistPure,
    CyberMatrix,
    ClassicRtss,
    Custom
}

/// <summary>
/// One reading shown on the HUD. Built-in metrics use a fixed id ("cpu_temp"); custom ones use the
/// sensor identifier. The position of the item in <see cref="OverlayConfig.MetricItems" /> is its display order.
/// </summary>
public sealed class OverlayMetricItem
{
    public OverlayMetricItem(string id, string defaultTitle, bool enabled = true, bool isCustom = false)
    {
        Id = id;
        DefaultTitle = defaultTitle;
        CustomTitle = defaultTitle;
        Enabled = enabled;
        IsCustom = isCustom;
    }

    public string Id { get; }
    public string DefaultTitle { get; }
    public string CustomTitle { get; set; }
    public bool Enabled { get; set; }
    public bool IsCustom { get; }

    /// <summary>Null means "use the global color".</summary>
    public Color? CustomLabelColor { get; set; }

    /// <summary>Null means "use the global color".</summary>
    public Color? CustomValueColor { get; set; }

    /// <summary>Value color follows the temperature/load severity when the global option is on.</summary>
    public bool UseDynamicColor { get; set; } = true;

    public bool ShowBar { get; set; } = true;

    /// <summary>Position inside the HUD, only used by <see cref="OverlayLayout.FreeCanvas" />.</summary>
    public int GridX { get; set; } = 12;

    public int GridY { get; set; } = 10;

    /// <summary>Memory formats: 0 = "9,4 GB (60%)", 1 = "9,4 GB", 2 = "9,4 / 15,7 GB".</summary>
    public int DisplayFormat { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(CustomTitle) ? DefaultTitle : CustomTitle;
}
