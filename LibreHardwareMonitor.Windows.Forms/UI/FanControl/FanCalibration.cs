// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>What a calibration measured for one channel.</summary>
public sealed class FanCalibrationResult
{
    /// <summary>Lowest tested duty (%) at which the fan still rotated.</summary>
    public float MinSpinPwm { get; set; }

    public float MaxRpm { get; set; }

    /// <summary>RPM at <see cref="MinSpinPwm" />.</summary>
    public float MinRpm { get; set; }

    /// <summary>The speed follows the duty. False for pumps and DC fans that ignore PWM.</summary>
    public bool Responsive { get; set; }

    public DateTime When { get; set; }

    /// <summary>
    /// Duty the channel must never go below: a margin above the stall point. Zero when the channel does not
    /// react to PWM (the stall point is meaningless then). Capped so a curve stays meaningful.
    /// </summary>
    public float SafeFloorPwm => Responsive ? Math.Min(70f, MinSpinPwm + 5f) : 0f;

    public string Serialize() => string.Join("|",
        MinSpinPwm.ToString(CultureInfo.InvariantCulture),
        MaxRpm.ToString(CultureInfo.InvariantCulture),
        MinRpm.ToString(CultureInfo.InvariantCulture),
        Responsive ? "1" : "0",
        When.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));

    public static FanCalibrationResult Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string[] p = raw.Split('|');
        if (p.Length < 5 ||
            !float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float min) ||
            !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float max) ||
            !float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float minRpm) ||
            !long.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
            return null;

        // Never trust values read from disk: a corrupted file must not raise the floor above what makes sense.
        if (min < 0f || min > 100f || max < 0f || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            return null;

        return new FanCalibrationResult { MinSpinPwm = min, MaxRpm = max, MinRpm = minRpm, Responsive = p[3] == "1", When = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime() };
    }
}

public enum FanCalibrationState
{
    Idle,
    Running,
    Completed,
    Aborted
}

/// <summary>
/// Step sequence of one calibration: duty goes 100 % -> 80 -> 60 -> 45 -> 35 -> 30 -> 25 -> 20, each step is held to
/// let the fan settle and its RPM is averaged over the last seconds. It stops early when the fan stalls, then
/// returns to 100 % for a moment so the fan restarts. Pure logic, driven by the engine loop.
/// </summary>
public sealed class FanCalibrationRun
{
    public static readonly float[] Steps = { 100f, 80f, 60f, 45f, 35f, 30f, 25f, 20f };

    public const long SettleMs = 5000;
    public const long SampleMs = 2000;
    public const long RecoverMs = 3000;
    public const float SpinRpm = 50f;

    private readonly List<(float Pwm, float Rpm)> _results = new();
    private readonly List<float> _samples = new();
    private int _index;
    private long _stepStartMs;
    private bool _recovering;

    public FanCalibrationState State { get; private set; } = FanCalibrationState.Idle;

    public string AbortReason { get; private set; }

    public FanCalibrationResult Result { get; private set; }

    public IReadOnlyList<(float Pwm, float Rpm)> Measurements => _results;

    /// <summary>Duty the channel must be driven at right now.</summary>
    public float TargetPwm => State != FanCalibrationState.Running ? 0f : (_recovering ? 100f : Steps[_index]);

    public double Progress => State == FanCalibrationState.Completed ? 1.0 : (double)_results.Count / Steps.Length;

    public string StepText => State != FanCalibrationState.Running ? string.Empty
        : (_recovering ? "A recuperar a 100 %" : $"A medir a {Steps[_index]:0} % ({_index + 1} de {Steps.Length})");

    public void Start(long nowMs)
    {
        _results.Clear();
        _samples.Clear();
        _index = 0;
        _recovering = false;
        _stepStartMs = nowMs;
        State = FanCalibrationState.Running;
        AbortReason = null;
        Result = null;
    }

    public void Abort(string reason)
    {
        if (State != FanCalibrationState.Running)
            return;

        State = FanCalibrationState.Aborted;
        AbortReason = reason;
    }

    public void Tick(long nowMs, float? rpm)
    {
        if (State != FanCalibrationState.Running)
            return;

        long elapsed = nowMs - _stepStartMs;

        if (_recovering)
        {
            if (elapsed >= RecoverMs)
                Finish();

            return;
        }

        if (elapsed >= SettleMs - SampleMs && rpm.HasValue)
            _samples.Add(rpm.Value);

        if (elapsed < SettleMs)
            return;

        float average = _samples.Count > 0 ? _samples.Average() : 0f;
        _results.Add((Steps[_index], average));
        _samples.Clear();

        bool stalled = average < SpinRpm && _index > 0;
        bool last = _index + 1 >= Steps.Length;
        if (stalled || last)
        {
            // Back to full duty so a stalled fan restarts before control is handed back.
            _recovering = true;
            _stepStartMs = nowMs;
            return;
        }

        _index++;
        _stepStartMs = nowMs;
    }

    private void Finish()
    {
        float maxRpm = _results.Count > 0 ? _results[0].Rpm : 0f;
        if (maxRpm < SpinRpm)
        {
            State = FanCalibrationState.Aborted;
            AbortReason = "A ventoinha não rodou a 100 %";
            return;
        }

        List<(float Pwm, float Rpm)> spinning = _results.Where(r => r.Rpm >= SpinRpm).ToList();
        float minSpinPwm = spinning.Min(r => r.Pwm);
        float minRpm = spinning.Min(r => r.Rpm);

        Result = new FanCalibrationResult
        {
            MinSpinPwm = minSpinPwm,
            MaxRpm = maxRpm,
            MinRpm = minRpm,
            // The speed has to drop noticeably when the duty drops, otherwise PWM does nothing here.
            Responsive = (maxRpm - minRpm) / maxRpm >= 0.15f,
            When = DateTime.Now
        };
        State = FanCalibrationState.Completed;
    }
}
