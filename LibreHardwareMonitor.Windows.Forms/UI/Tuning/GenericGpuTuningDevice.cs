// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public class GenericGpuTuningDevice : IGpuTuningDevice
{
    private readonly IHardware _hardware;
    private readonly GpuTuningCapabilities _capabilities;
    private GpuTuningState _currentState;

    public string Name => _hardware.Name;

    public string DeviceId => _hardware.Identifier.ToString();

    public HardwareType GpuType => _hardware.HardwareType;

    public IHardware Hardware => _hardware;

    public GpuTuningCapabilities Capabilities => _capabilities;

    public GpuTuningState CurrentState => _currentState;

    public GenericGpuTuningDevice(IHardware hardware, string unsupportedReason = null)
    {
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _currentState = GpuTuningState.CreateDefault();

        string reason = unsupportedReason ??
            (hardware.HardwareType == HardwareType.GpuIntel
                ? "GPU Intel sem suporte a ajuste de frequencias pelo driver em modo utilizador."
                : "Dispositivo sem suporte a overclock/undervolt pelo driver de video.");

        _capabilities = new GpuTuningCapabilities
        {
            IsSupported = false,
            UnsupportedReason = reason,
            CanAdjustCoreClock = false,
            CanAdjustMemoryClock = false,
            CanAdjustPowerLimit = false,
            CanAdjustTempLimit = false
        };
    }

    private float? _lastCoreClock;
    private float? _lastMemoryClock;
    private float? _lastTemperature;
    private float? _lastPower;
    private float? _lastLoad;

    public virtual GpuTuningTelemetry ReadTelemetry()
    {
        GpuTuningTelemetry telemetry = new();

        if (_hardware == null)
            return telemetry;

        // Core clock, temperature and load come from the same selectors as the cards and the overlay (SensorLookup):
        // the core temperature is never the hot spot and the load never a secondary engine.
        ISensor coreClock = SensorLookup.GpuCoreClock(_hardware);
        ISensor temperature = SensorLookup.GpuCoreTemperature(_hardware);
        ISensor load = SensorLookup.GpuCoreLoad(_hardware);
        telemetry.CoreClockMhz = coreClock?.Value;
        telemetry.CoreClockSource = coreClock?.Name;
        telemetry.TemperatureC = temperature?.Value;
        telemetry.TemperatureSource = temperature?.Name;
        telemetry.LoadPercent = load?.Value;
        telemetry.LoadSource = load?.Name;
        telemetry.MemoryClockMhz = SensorLookup.Find(_hardware, SensorType.Clock, "GPU Memory", "Memory")?.Value;

        foreach (ISensor sensor in _hardware.Sensors)
        {
            // The page shows the last power reading of the device, as before.
            if (sensor.SensorType == SensorType.Power && sensor.Value != null)
                telemetry.PowerWatts = sensor.Value.Value;
        }

        if (telemetry.CoreClockMhz.HasValue && telemetry.CoreClockMhz.Value > 0f)
            _lastCoreClock = telemetry.CoreClockMhz;
        else if (_lastCoreClock.HasValue)
            telemetry.CoreClockMhz = _lastCoreClock;

        if (telemetry.MemoryClockMhz.HasValue && telemetry.MemoryClockMhz.Value > 0f)
            _lastMemoryClock = telemetry.MemoryClockMhz;
        else if (_lastMemoryClock.HasValue)
            telemetry.MemoryClockMhz = _lastMemoryClock;

        if (telemetry.TemperatureC.HasValue && telemetry.TemperatureC.Value > 0f)
            _lastTemperature = telemetry.TemperatureC;
        else if (_lastTemperature.HasValue)
            telemetry.TemperatureC = _lastTemperature;

        if (telemetry.PowerWatts.HasValue && telemetry.PowerWatts.Value > 0f)
            _lastPower = telemetry.PowerWatts;
        else if (_lastPower.HasValue)
            telemetry.PowerWatts = _lastPower;

        if (telemetry.LoadPercent.HasValue)
            _lastLoad = telemetry.LoadPercent;
        else if (_lastLoad.HasValue)
            telemetry.LoadPercent = _lastLoad;

        return telemetry;
    }

    public virtual bool ApplyTuning(GpuTuningState state, out string errorMessage)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (!_capabilities.IsSupported)
        {
            errorMessage = _capabilities.UnsupportedReason;
            return false;
        }

        _currentState = state.Clone();
        errorMessage = null;
        return true;
    }

    public virtual bool ResetToStock(out string errorMessage)
    {
        _currentState = GpuTuningState.CreateDefault();
        errorMessage = null;
        return true;
    }
}
