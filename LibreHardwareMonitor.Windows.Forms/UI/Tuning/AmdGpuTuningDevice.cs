// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public sealed class AmdGpuTuningDevice : GenericGpuTuningDevice
{
    private readonly int _adapterIndex;

    public AmdGpuTuningDevice(IHardware hardware, int adapterIndex)
        : base(hardware, null)
    {
        _adapterIndex = adapterIndex;

        if (hardware.HardwareType != HardwareType.GpuAmd)
        {
            Capabilities.IsSupported = false;
            Capabilities.UnsupportedReason = "Dispositivo nao e uma placa de video AMD compativel.";
            return;
        }

        Capabilities.IsSupported = true;
        Capabilities.UnsupportedReason = string.Empty;

        // Limites de seguranca rigorosos
        Capabilities.CanAdjustCoreClock = true;
        Capabilities.MinCoreOffsetMhz = -300;
        Capabilities.MaxCoreOffsetMhz = 150;

        Capabilities.CanAdjustMemoryClock = true;
        Capabilities.MinMemoryOffsetMhz = -500;
        Capabilities.MaxMemoryOffsetMhz = 500;

        Capabilities.CanAdjustPowerLimit = true;
        Capabilities.MinPowerLimitPercent = 70;
        Capabilities.MaxPowerLimitPercent = 115;
        Capabilities.DefaultPowerLimitPercent = 100;

        Capabilities.CanAdjustTempLimit = true;
        Capabilities.MinTempLimitC = 65;
        Capabilities.MaxTempLimitC = 88;
        Capabilities.DefaultTempLimitC = 80;
    }

    public override bool ApplyTuning(GpuTuningState state, out string errorMessage)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (!Capabilities.IsSupported)
        {
            errorMessage = Capabilities.UnsupportedReason;
            return false;
        }

        int clampedCore = Clamp(state.CoreClockOffsetMhz, Capabilities.MinCoreOffsetMhz, Capabilities.MaxCoreOffsetMhz);
        int clampedMem = Clamp(state.MemoryClockOffsetMhz, Capabilities.MinMemoryOffsetMhz, Capabilities.MaxMemoryOffsetMhz);
        int clampedPower = Clamp(state.PowerLimitPercent, Capabilities.MinPowerLimitPercent, Capabilities.MaxPowerLimitPercent);
        int clampedTemp = Clamp(state.TempLimitC, Capabilities.MinTempLimitC, Capabilities.MaxTempLimitC);

        try
        {
            CurrentState.CoreClockOffsetMhz = clampedCore;
            CurrentState.MemoryClockOffsetMhz = clampedMem;
            CurrentState.PowerLimitPercent = clampedPower;
            CurrentState.TempLimitC = clampedTemp;
            CurrentState.ApplyOnStartup = state.ApplyOnStartup;

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Falha ao comunicar com o driver AMD: {ex.Message}";
            return false;
        }
    }

    public override bool ResetToStock(out string errorMessage)
    {
        try
        {
            CurrentState.CoreClockOffsetMhz = 0;
            CurrentState.MemoryClockOffsetMhz = 0;
            CurrentState.PowerLimitPercent = 100;
            CurrentState.TempLimitC = 80;

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Erro ao repor valores de fabrica: {ex.Message}";
            return false;
        }
    }

    private static int Clamp(int value, int min, int max)
    {
        return value < min ? min : (value > max ? max : value);
    }
}
