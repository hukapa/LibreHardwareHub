// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public sealed class CpuTuningCapabilities
{
    public bool IsIntel { get; set; }

    public bool IsAmd { get; set; }

    public bool HasWindowsPowerControl { get; set; } = true;

    public bool CanAdjustRapl { get; set; }

    public bool IsRaplLocked { get; set; } = true;

    public string RaplStatusMessage { get; set; } = string.Empty;

    public int MinPl1Watts { get; set; } = 15;

    public int MaxPl1Watts { get; set; } = 253;

    public int DefaultPl1Watts { get; set; } = 65;

    public int MinPl2Watts { get; set; } = 15;

    public int MaxPl2Watts { get; set; } = 253;

    public int DefaultPl2Watts { get; set; } = 125;
}

public sealed class CpuTuningState
{
    public int MaxProcessorStatePercent { get; set; } = 100;

    public int TurboBoostMode { get; set; } = 1;

    public int Pl1Watts { get; set; } = 65;

    public int Pl2Watts { get; set; } = 125;

    public bool ApplyOnStartup { get; set; }

    public CpuTuningState Clone()
    {
        return new CpuTuningState
        {
            MaxProcessorStatePercent = MaxProcessorStatePercent,
            TurboBoostMode = TurboBoostMode,
            Pl1Watts = Pl1Watts,
            Pl2Watts = Pl2Watts,
            ApplyOnStartup = ApplyOnStartup
        };
    }

    public static CpuTuningState CreateDefault(int defaultPl1 = 65, int defaultPl2 = 125)
    {
        return new CpuTuningState
        {
            MaxProcessorStatePercent = 100,
            TurboBoostMode = 1, // Ativo / Padrao
            Pl1Watts = defaultPl1,
            Pl2Watts = defaultPl2,
            ApplyOnStartup = false
        };
    }

    public static CpuTuningState CreateEco(int defaultPl1)
    {
        return new CpuTuningState
        {
            MaxProcessorStatePercent = 85,
            TurboBoostMode = 0, // Desativado para silencio e temperaturas frias
            Pl1Watts = defaultPl1 > 20 ? defaultPl1 - 15 : defaultPl1,
            Pl2Watts = defaultPl1,
            ApplyOnStartup = false
        };
    }

    public static CpuTuningState CreateBalanced(int defaultPl1, int defaultPl2)
    {
        return new CpuTuningState
        {
            MaxProcessorStatePercent = 100,
            TurboBoostMode = 3, // Eficiente
            Pl1Watts = defaultPl1,
            Pl2Watts = defaultPl2,
            ApplyOnStartup = false
        };
    }

    public static CpuTuningState CreateMaxPerformance(int defaultPl1, int defaultPl2)
    {
        return new CpuTuningState
        {
            MaxProcessorStatePercent = 100,
            TurboBoostMode = 2, // Agressivo
            Pl1Watts = defaultPl2,
            Pl2Watts = defaultPl2,
            ApplyOnStartup = false
        };
    }

    public bool IsStock(int defaultPl1, int defaultPl2)
    {
        return MaxProcessorStatePercent == 100 &&
               TurboBoostMode == 1 &&
               Pl1Watts == defaultPl1 &&
               Pl2Watts == defaultPl2;
    }
}

public sealed class CpuTuningTelemetry
{
    public float? PackageTemperatureC { get; set; }

    public float? PackagePowerWatts { get; set; }

    public float? CoreClockMhz { get; set; }

    public float? TotalLoadPercent { get; set; }

    public float? VoltageV { get; set; }
}
