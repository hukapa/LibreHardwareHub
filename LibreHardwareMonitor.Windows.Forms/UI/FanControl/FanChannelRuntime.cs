// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public sealed class FanChannelRuntime
{
    private const float HardThermalThresholdC = 90.0f;
    private const float HardThermalReleaseC = 82.0f;
    private const float SensorFailureFallbackPwm = 75.0f;

    private readonly List<(long TimeMs, float Temp)> _tempHistory = new();

    public IControl Control { get; }

    public ISensor RpmSensor { get; set; }

    public FanChannelConfig Config { get; }

    public string ChannelName => Control.Sensor != null ? Control.Sensor.Name : Control.Identifier.ToString();

    public string HardwareName => Control.Sensor?.Hardware != null ? Control.Sensor.Hardware.Name : "Controller";

    public float LastAppliedPwm { get; private set; }

    public float LastEffectiveTemp { get; private set; } = 40.0f;

    public bool InEmergencyOverride { get; private set; }

    public long LastUpdateTimeMs { get; private set; }

    public FanChannelRuntime(IControl control, FanChannelConfig config, ISensor rpmSensor = null)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        Config = config ?? new FanChannelConfig { ChannelId = control.Identifier.ToString() };
        RpmSensor = rpmSensor;
        LastAppliedPwm = control.SoftwareValue;
    }

    public void Update(float? rawTemp, long nowMs, float deltaSeconds)
    {
        LastUpdateTimeMs = nowMs;

        // Fail-Safe 1: Ausencia ou corrupcao de sinal termico
        if (!rawTemp.HasValue || float.IsNaN(rawTemp.Value) || rawTemp.Value <= 0f)
        {
            if (Config.Mode != FanChannelMode.DefaultBios)
                ApplyPwm(SensorFailureFallbackPwm);
            return;
        }

        float currentTemp = rawTemp.Value;

        // Fail-Safe 2: Hard Thermal Override (>= 90 C forca 100% PWM com histerese de 8 C ate 82 C)
        if (currentTemp >= HardThermalThresholdC)
        {
            InEmergencyOverride = true;
            ApplyPwm(100.0f);
            return;
        }

        if (InEmergencyOverride)
        {
            if (currentTemp <= HardThermalReleaseC)
            {
                InEmergencyOverride = false;
            }
            else
            {
                ApplyPwm(100.0f);
                return;
            }
        }

        switch (Config.Mode)
        {
            case FanChannelMode.DefaultBios:
                if (Control.ControlMode != ControlMode.Default)
                    Control.SetDefault();
                LastAppliedPwm = 0f;
                break;

            case FanChannelMode.Fixed:
                ApplyPwm(Config.FixedPwm);
                break;

            case FanChannelMode.Curve:
                UpdateCurveMode(currentTemp, nowMs, deltaSeconds);
                break;
        }
    }

    private void UpdateCurveMode(float currentTemp, long nowMs, float deltaSeconds)
    {
        // 1. Manter janela de media movel para ignorar picos de boost
        _tempHistory.Add((nowMs, currentTemp));
        long windowStart = nowMs - (long)(Math.Max(Config.ResponseTimeSeconds, 1.0f) * 1000f);
        _tempHistory.RemoveAll(x => x.TimeMs < windowStart);

        float avgTemp = _tempHistory.Count > 0
            ? _tempHistory.Average(x => x.Temp)
            : currentTemp;

        // 2. Histerese assimetrica (Up vs Down)
        if (avgTemp >= LastEffectiveTemp + Config.HysteresisUpC)
        {
            LastEffectiveTemp = avgTemp;
        }
        else if (avgTemp <= LastEffectiveTemp - Config.HysteresisDownC)
        {
            LastEffectiveTemp = avgTemp;
        }

        // 3. Interpolacao na curva
        float targetPwm = Config.Curve.Evaluate(LastEffectiveTemp);

        // 4. Step Limits (Taxa maxima de variacao por segundo)
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

    public void ApplyPwm(float targetPwm)
    {
        float min = Control.MinSoftwareValue;
        float max = Control.MaxSoftwareValue > 0 ? Control.MaxSoftwareValue : 100f;
        float clamped = targetPwm < min ? min : (targetPwm > max ? max : targetPwm);

        try
        {
            Control.SetSoftware(clamped);
            LastAppliedPwm = clamped;
        }
        catch
        {
            // Falha de comunicacao de baixo nivel protegida
        }
    }

    public void ResetToDefault()
    {
        try
        {
            Control.SetDefault();
            InEmergencyOverride = false;
            LastAppliedPwm = 0f;
        }
        catch
        {
        }
    }
}
