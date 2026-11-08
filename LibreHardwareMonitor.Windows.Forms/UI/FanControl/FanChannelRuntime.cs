// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>
/// One controllable fan channel and the safety logic around it. The fail-safe order is:
/// identify override, "not managed" (BIOS keeps control), sensor lost, thermal emergency, stalled fan,
/// then the user's mode (fixed / curve). A managed channel never receives less than the role minimum.
/// </summary>
public sealed class FanChannelRuntime
{
    private const float HardThermalThresholdC = 90.0f;
    private const float HardThermalReleaseC = 82.0f;
    private const float StallMinPwm = 35.0f;
    private const float StallRpmThreshold = 50.0f;
    private const long StallConfirmMs = 8000;

    /// <summary>How long "Identify" keeps the channel at 100 %.</summary>
    public const int IdentifyDurationMs = 8000;

    private readonly List<(long TimeMs, float Temp)> _tempHistory = new();
    private bool _holdsManualControl;
    private bool _hasSeenRpm;
    private long _stallSinceMs;
    private long _identifyUntilMs;

    public IControl Control { get; }

    public ISensor RpmSensor { get; set; }

    public FanChannelConfig Config { get; }

    /// <summary>Raw sensor name from the hardware ("Fan #1").</summary>
    public string ChannelName => Control.Sensor != null ? Control.Sensor.Name : Control.Identifier.ToString();

    public string HardwareName => Control.Sensor?.Hardware != null ? Control.Sensor.Hardware.Name : "Controller";

    /// <summary>Name the user gave, or the hardware name.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Config.Name) ? ChannelName : Config.Name.Trim();

    public float LastAppliedPwm { get; private set; }

    public float LastEffectiveTemp { get; private set; } = 40.0f;

    public bool InEmergencyOverride { get; private set; }

    /// <summary>No usable temperature for the channel's source: the channel runs at 100 %.</summary>
    public bool SensorLost { get; private set; }

    /// <summary>The fan reported ~0 RPM while the duty was high enough to spin it.</summary>
    public bool Stalled { get; private set; }

    public long LastUpdateTimeMs { get; private set; }

    /// <summary>The app is currently holding this channel in software (manual) control.</summary>
    public bool HoldsManualControl => _holdsManualControl;

    /// <summary>True when the Fan Control engine drives the channel (not the BIOS, not the user's tree menu).</summary>
    public bool IsManaged =>
        Config.Mode != FanChannelMode.DefaultBios && FanRolePolicy.AllowsMode(Config.Role, Config.Mode);

    public bool IsIdentifying => _identifyUntilMs > 0;

    public long IdentifyRemainingMs(long nowMs) => _identifyUntilMs > nowMs ? _identifyUntilMs - nowMs : 0;

    /// <summary>Short alert text for the UI, or null when everything is fine.</summary>
    public string Alert
    {
        get
        {
            if (InEmergencyOverride)
                return "Emergência térmica · 100 %";

            if (Stalled)
                return FanRolePolicy.IsCritical(Config.Role) ? "Parou de rodar · verifique já a ligação" : "Parou de rodar";

            if (SensorLost && IsManaged)
                return "Sem leitura de temperatura · 100 %";

            return null;
        }
    }

    public FanChannelRuntime(IControl control, FanChannelConfig config, ISensor rpmSensor = null)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        Config = config ?? new FanChannelConfig { ChannelId = control.Identifier.ToString() };
        RpmSensor = rpmSensor;
        LastAppliedPwm = control.SoftwareValue;
    }

    /// <summary>Puts the channel at 100 % for <see cref="IdentifyDurationMs" /> so the user can hear which fan it is.</summary>
    public void BeginIdentify(long nowMs) => _identifyUntilMs = nowMs + IdentifyDurationMs;

    public void CancelIdentify() => _identifyUntilMs = 1; // expires on the next update

    public void Update(float? rawTemp, long nowMs, float deltaSeconds, bool globalEmergency)
    {
        LastUpdateTimeMs = nowMs;
        UpdateStall(nowMs);

        // Identify: always safe (raising the speed), allowed on any channel, then back to what it was.
        if (_identifyUntilMs > 0)
        {
            if (nowMs < _identifyUntilMs)
            {
                WritePwm(100f);
                return;
            }

            _identifyUntilMs = 0;
            if (!IsManaged)
                ReleaseToBios();
        }

        // Not managed (BIOS mode, unidentified, pump on a curve, nothing connected): the app does not write.
        if (!IsManaged)
        {
            InEmergencyOverride = false;
            SensorLost = false;
            ReleaseToBios();
            return;
        }

        // Fail-safe 1: no usable temperature for the source -> full speed.
        SensorLost = !rawTemp.HasValue || float.IsNaN(rawTemp.Value) || rawTemp.Value <= 0f;
        if (SensorLost)
        {
            ApplyPwm(100.0f);
            return;
        }

        float currentTemp = rawTemp.Value;

        // Fail-safe 2: thermal emergency (global CPU/GPU limits, or this channel's own source >= 90 C,
        // released with hysteresis at 82 C).
        if (globalEmergency || currentTemp >= HardThermalThresholdC || (InEmergencyOverride && currentTemp > HardThermalReleaseC))
        {
            InEmergencyOverride = true;
            ApplyPwm(100.0f);
            return;
        }

        InEmergencyOverride = false;

        // Fail-safe 3: a stalled fan gets full duty (it may unstick) and raises an alert.
        if (Stalled)
        {
            ApplyPwm(100.0f);
            return;
        }

        switch (Config.Mode)
        {
            case FanChannelMode.Fixed:
                ApplyPwm(Config.FixedPwm);
                break;

            case FanChannelMode.Curve:
                UpdateCurveMode(currentTemp, nowMs, deltaSeconds);
                break;
        }
    }

    private void UpdateStall(long nowMs)
    {
        float? rpm = RpmSensor?.Value;

        if (rpm.HasValue && rpm.Value > StallRpmThreshold)
        {
            _hasSeenRpm = true;
            _stallSinceMs = 0;
            Stalled = false;
            return;
        }

        // Only a channel that has been seen spinning can "stall": empty headers read 0 RPM forever.
        if (!_hasSeenRpm || !rpm.HasValue)
        {
            _stallSinceMs = 0;
            Stalled = false;
            return;
        }

        float duty = _holdsManualControl ? LastAppliedPwm : (Control.Sensor?.Value ?? 0f);
        if (duty >= StallMinPwm)
        {
            if (_stallSinceMs == 0)
                _stallSinceMs = nowMs;
            else if (nowMs - _stallSinceMs >= StallConfirmMs)
                Stalled = true;
        }
        else
        {
            _stallSinceMs = 0;
            Stalled = false;
        }
    }

    private void UpdateCurveMode(float currentTemp, long nowMs, float deltaSeconds)
    {
        // 1. Moving average window to ignore short boost spikes.
        _tempHistory.Add((nowMs, currentTemp));
        long windowStart = nowMs - (long)(Math.Max(Config.ResponseTimeSeconds, 1.0f) * 1000f);

        while (_tempHistory.Count > 0 && _tempHistory[0].TimeMs < windowStart)
            _tempHistory.RemoveAt(0);

        float sum = 0f;
        int count = _tempHistory.Count;
        for (int i = 0; i < count; i++)
            sum += _tempHistory[i].Temp;

        float avgTemp = count > 0 ? sum / count : currentTemp;

        // 2. Asymmetric hysteresis.
        if (avgTemp >= LastEffectiveTemp + Config.HysteresisUpC)
            LastEffectiveTemp = avgTemp;
        else if (avgTemp <= LastEffectiveTemp - Config.HysteresisDownC)
            LastEffectiveTemp = avgTemp;

        // 3. Curve interpolation.
        float targetPwm = Config.Curve.Evaluate(LastEffectiveTemp);

        // 4. Step limits (maximum change per second).
        if (deltaSeconds > 0f && LastAppliedPwm > 0f)
        {
            float maxUp = Config.MaxStepUpPercent * deltaSeconds;
            float maxDown = Config.MaxStepDownPercent * deltaSeconds;

            if (targetPwm > LastAppliedPwm + maxUp)
                targetPwm = LastAppliedPwm + maxUp;
            else if (targetPwm < LastAppliedPwm - maxDown)
                targetPwm = LastAppliedPwm - maxDown;
        }

        ApplyPwm(targetPwm);
    }

    /// <summary>Applies a duty clamped to the hardware range and to the role minimum.</summary>
    public void ApplyPwm(float targetPwm)
    {
        float min = Math.Max(Control.MinSoftwareValue, FanRolePolicy.MinimumPwm(Config.Role));
        WritePwm(targetPwm, min);
    }

    private void WritePwm(float targetPwm, float min = 0f)
    {
        float max = Control.MaxSoftwareValue > 0 ? Control.MaxSoftwareValue : 100f;
        min = Math.Max(min, Control.MinSoftwareValue);
        float clamped = targetPwm < min ? min : (targetPwm > max ? max : targetPwm);

        try
        {
            Control.SetSoftware(clamped);
            _holdsManualControl = true;
            LastAppliedPwm = clamped;
        }
        catch
        {
            // Low-level communication failure: keep going, the next cycle retries.
        }
    }

    /// <summary>Gives control back to the BIOS, but only if this app was the one that took it.</summary>
    private void ReleaseToBios()
    {
        if (!_holdsManualControl)
            return;

        try
        {
            Control.SetDefault();
        }
        catch
        {
        }

        _holdsManualControl = false;
        LastAppliedPwm = 0f;
        _tempHistory.Clear();
    }

    public void ResetToDefault()
    {
        try
        {
            Control.SetDefault();
        }
        catch
        {
        }

        _holdsManualControl = false;
        _identifyUntilMs = 0;
        InEmergencyOverride = false;
        SensorLost = false;
        LastAppliedPwm = 0f;
        _tempHistory.Clear();
    }
}
