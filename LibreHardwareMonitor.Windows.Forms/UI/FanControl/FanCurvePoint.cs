// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public readonly struct FanCurvePoint : IComparable<FanCurvePoint>, IEquatable<FanCurvePoint>
{
    public float Temperature { get; }
    public float PwmPercent { get; }

    public FanCurvePoint(float temperature, float pwmPercent)
    {
        Temperature = temperature;
        PwmPercent = pwmPercent < 0f ? 0f : (pwmPercent > 100f ? 100f : pwmPercent);
    }

    public int CompareTo(FanCurvePoint other) => Temperature.CompareTo(other.Temperature);

    public bool Equals(FanCurvePoint other) =>
        Math.Abs(Temperature - other.Temperature) < 0.001f &&
        Math.Abs(PwmPercent - other.PwmPercent) < 0.001f;

    public override bool Equals(object obj) => obj is FanCurvePoint other && Equals(other);

    public override int GetHashCode() => (Temperature.GetHashCode() * 397) ^ PwmPercent.GetHashCode();

    public override string ToString() => $"{Temperature:F0} C -> {PwmPercent:F0}%";
}
