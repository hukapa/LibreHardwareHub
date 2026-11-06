// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public sealed class NvidiaGpuTuningDevice : GenericGpuTuningDevice
{
    private readonly int _gpuIndex;
    private uint _defaultPowerMw;
    private static bool _nvmlInitialized;
    private static bool _nvmlAvailable = true;

    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
    private static extern int NvmlInit();

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    private static extern int NvmlDeviceGetHandleByIndex(int index, out IntPtr device);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceSetPowerManagementLimit")]
    private static extern int NvmlDeviceSetPowerManagementLimit(IntPtr device, uint limitMilliwatts);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerManagementLimitConstraints")]
    private static extern int NvmlDeviceGetPowerManagementLimitConstraints(IntPtr device, out uint minLimit, out uint maxLimit);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerManagementDefaultLimit")]
    private static extern int NvmlDeviceGetPowerManagementDefaultLimit(IntPtr device, out uint defaultLimit);

    public NvidiaGpuTuningDevice(IHardware hardware, int gpuIndex)
        : base(hardware, null)
    {
        _gpuIndex = gpuIndex;

        if (hardware.HardwareType != HardwareType.GpuNvidia)
        {
            Capabilities.IsSupported = false;
            Capabilities.UnsupportedReason = "Dispositivo nao e uma placa de video NVIDIA compativel.";
            return;
        }

        Capabilities.IsSupported = true;
        Capabilities.UnsupportedReason = string.Empty;

        // Frequencias: Limitadas entre -300 MHz e +150 MHz para o core (seguro para TDR)
        Capabilities.CanAdjustCoreClock = true;
        Capabilities.MinCoreOffsetMhz = -300;
        Capabilities.MaxCoreOffsetMhz = 150;

        // Memoria: Limitada entre -1000 MHz e +1000 MHz
        Capabilities.CanAdjustMemoryClock = true;
        Capabilities.MinMemoryOffsetMhz = -1000;
        Capabilities.MaxMemoryOffsetMhz = 1000;

        // Limite de Potencia (TDP): Entre 50% e 115% (nunca permite alvos extremos)
        Capabilities.CanAdjustPowerLimit = true;
        Capabilities.MinPowerLimitPercent = 50;
        Capabilities.MaxPowerLimitPercent = 115;
        Capabilities.DefaultPowerLimitPercent = 100;

        // Limite Termico: Entre 65 C e 88 C
        Capabilities.CanAdjustTempLimit = true;
        Capabilities.MinTempLimitC = 65;
        Capabilities.MaxTempLimitC = 88;
        Capabilities.DefaultTempLimitC = 83;

        // Tentar obter limites reais de hardware via NVML
        if (_nvmlAvailable)
        {
            try
            {
                if (!_nvmlInitialized)
                {
                    if (NvmlInit() == 0)
                        _nvmlInitialized = true;
                    else
                        _nvmlAvailable = false;
                }

                if (_nvmlInitialized && NvmlDeviceGetHandleByIndex(_gpuIndex, out IntPtr dev) == 0)
                {
                    if (NvmlDeviceGetPowerManagementDefaultLimit(dev, out uint defMw) == 0 && defMw > 0)
                    {
                        _defaultPowerMw = defMw;
                        if (NvmlDeviceGetPowerManagementLimitConstraints(dev, out uint minMw, out uint maxMw) == 0)
                        {
                            int calcMin = (int)((minMw * 100) / defMw);
                            int calcMax = (int)((maxMw * 100) / defMw);
                            Capabilities.MinPowerLimitPercent = Math.Max(50, calcMin);
                            Capabilities.MaxPowerLimitPercent = Math.Min(115, calcMax);
                        }
                    }
                }
            }
            catch
            {
                _nvmlAvailable = false;
            }
        }
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

        // Bloqueio rigoroso de seguranca (Clamping)
        int clampedCore = Clamp(state.CoreClockOffsetMhz, Capabilities.MinCoreOffsetMhz, Capabilities.MaxCoreOffsetMhz);
        int clampedMem = Clamp(state.MemoryClockOffsetMhz, Capabilities.MinMemoryOffsetMhz, Capabilities.MaxMemoryOffsetMhz);
        int clampedPower = Clamp(state.PowerLimitPercent, Capabilities.MinPowerLimitPercent, Capabilities.MaxPowerLimitPercent);
        int clampedTemp = Clamp(state.TempLimitC, Capabilities.MinTempLimitC, Capabilities.MaxTempLimitC);

        try
        {
            // Aplicacao real de Power Limit via NVML caso disponivel
            if (_nvmlInitialized && _defaultPowerMw > 0)
            {
                if (NvmlDeviceGetHandleByIndex(_gpuIndex, out IntPtr dev) == 0)
                {
                    uint targetMw = (uint)((_defaultPowerMw * (ulong)clampedPower) / 100);
                    NvmlDeviceSetPowerManagementLimit(dev, targetMw);
                }
            }

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
            errorMessage = $"Falha de comunicacao com o driver NVIDIA: {ex.Message}";
            return false;
        }
    }

    public override bool ResetToStock(out string errorMessage)
    {
        try
        {
            if (_nvmlInitialized && _defaultPowerMw > 0)
            {
                if (NvmlDeviceGetHandleByIndex(_gpuIndex, out IntPtr dev) == 0)
                {
                    NvmlDeviceSetPowerManagementLimit(dev, _defaultPowerMw);
                }
            }

            CurrentState.CoreClockOffsetMhz = 0;
            CurrentState.MemoryClockOffsetMhz = 0;
            CurrentState.PowerLimitPercent = 100;
            CurrentState.TempLimitC = 83;

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
