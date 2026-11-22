// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public sealed class GpuTuningCapabilities
{
    public bool IsSupported { get; set; }

    public string UnsupportedReason { get; set; } = string.Empty;

    public bool CanAdjustCoreClock { get; set; }

    public int MinCoreOffsetMhz { get; set; } = -300;

    public int MaxCoreOffsetMhz { get; set; } = 150;

    public bool CanAdjustMemoryClock { get; set; }

    public int MinMemoryOffsetMhz { get; set; } = -1000;

    public int MaxMemoryOffsetMhz { get; set; } = 1000;

    public bool CanAdjustPowerLimit { get; set; }

    public int MinPowerLimitPercent { get; set; } = 50;

    public int MaxPowerLimitPercent { get; set; } = 115;

    public int DefaultPowerLimitPercent { get; set; } = 100;

    public bool CanAdjustTempLimit { get; set; }

    public int MinTempLimitC { get; set; } = 65;

    public int MaxTempLimitC { get; set; } = 88;

    public int DefaultTempLimitC { get; set; } = 83;
}

public sealed class GpuTuningState
{
    public int CoreClockOffsetMhz { get; set; }

    public int MemoryClockOffsetMhz { get; set; }

    public int PowerLimitPercent { get; set; } = 100;

    public int TempLimitC { get; set; } = 83;

    public bool ApplyOnStartup { get; set; }

    public GpuTuningState Clone()
    {
        return new GpuTuningState
        {
            CoreClockOffsetMhz = CoreClockOffsetMhz,
            MemoryClockOffsetMhz = MemoryClockOffsetMhz,
            PowerLimitPercent = PowerLimitPercent,
            TempLimitC = TempLimitC,
            ApplyOnStartup = ApplyOnStartup
        };
    }

    public static GpuTuningState CreateDefault()
    {
        return new GpuTuningState
        {
            CoreClockOffsetMhz = 0,
            MemoryClockOffsetMhz = 0,
            PowerLimitPercent = 100,
            TempLimitC = 83,
            ApplyOnStartup = false
        };
    }

    public bool IsStock()
    {
        return CoreClockOffsetMhz == 0 &&
               MemoryClockOffsetMhz == 0 &&
               PowerLimitPercent == 100 &&
               TempLimitC == 83;
    }
}

public sealed class GpuTuningTelemetry
{
    public float? CoreClockMhz { get; set; }

    public float? MemoryClockMhz { get; set; }

    public float? TemperatureC { get; set; }

    public float? PowerWatts { get; set; }

    public float? PowerPercent { get; set; }

    public float? LoadPercent { get; set; }

    // Names of the sensors behind the readings, so the page captions what it really shows.
    public string CoreClockSource { get; set; }

    public string TemperatureSource { get; set; }

    public string LoadSource { get; set; }
}
