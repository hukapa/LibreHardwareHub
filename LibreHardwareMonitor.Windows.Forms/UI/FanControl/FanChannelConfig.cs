// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public sealed class FanChannelConfig
{
    public const string SourceMaxCpuGpu = "max_cpu_gpu";
    public const string SourceCpu = "cpu";
    public const string SourceGpu = "gpu";

    public string ChannelId { get; set; } = string.Empty;

    public FanChannelMode Mode { get; set; } = FanChannelMode.DefaultBios;

    /// <summary>User label ("Bomba AIO", "Frente da caixa"); empty = use the sensor name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Role chosen by the user; <see cref="FanChannelRole.Unassigned" /> = not confirmed yet.</summary>
    public FanChannelRole Role { get; set; } = FanChannelRole.Unassigned;

    /// <summary>How many fans share this channel (splitters/hubs). Informative.</summary>
    public int FanCount { get; set; } = 1;

    public float FixedPwm { get; set; } = 50f;

    public FanCurve Curve { get; set; } = FanCurve.CreateBalanced();

    public string TemperatureSource { get; set; } = SourceMaxCpuGpu;

    public float ResponseTimeSeconds { get; set; } = 3.0f;

    public float HysteresisUpC { get; set; } = 1.5f;

    public float HysteresisDownC { get; set; } = 3.0f;

    public float MaxStepUpPercent { get; set; } = 15.0f;

    public float MaxStepDownPercent { get; set; } = 5.0f;
}
