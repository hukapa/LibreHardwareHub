// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public sealed class FanCurve
{
    public List<FanCurvePoint> Points { get; set; } = new();

    public FanCurve()
    {
    }

    public FanCurve(IEnumerable<FanCurvePoint> points)
    {
        Points = points.OrderBy(p => p.Temperature).ToList();
    }

    public float Evaluate(float temperature)
    {
        if (Points == null || Points.Count == 0)
            return 50f;

        if (Points.Count == 1)
            return Points[0].PwmPercent;

        if (temperature <= Points[0].Temperature)
            return Points[0].PwmPercent;

        if (temperature >= Points[Points.Count - 1].Temperature)
            return Points[Points.Count - 1].PwmPercent;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            FanCurvePoint p1 = Points[i];
            FanCurvePoint p2 = Points[i + 1];

            if (temperature >= p1.Temperature && temperature <= p2.Temperature)
            {
                float range = p2.Temperature - p1.Temperature;
                if (range <= 0.001f)
                    return p2.PwmPercent;

                float factor = (temperature - p1.Temperature) / range;
                return p1.PwmPercent + factor * (p2.PwmPercent - p1.PwmPercent);
            }
        }

        return Points[Points.Count - 1].PwmPercent;
    }

    public string Serialize()
    {
        if (Points == null || Points.Count == 0)
            return string.Empty;

        return string.Join(";", Points.Select(p =>
            string.Format(CultureInfo.InvariantCulture, "{0:F1}:{1:F1}", p.Temperature, p.PwmPercent)));
    }

    public static FanCurve Deserialize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return CreateBalanced();

        List<FanCurvePoint> points = new();
        string[] pairs = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(':');
            if (parts.Length == 2 &&
                float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float temp) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float pwm))
            {
                points.Add(new FanCurvePoint(temp, pwm));
            }
        }

        return points.Count > 0 ? new FanCurve(points) : CreateBalanced();
    }

    public static FanCurve CreateSilent() => new([
        new(35f, 25f),
        new(50f, 35f),
        new(65f, 50f),
        new(75f, 70f),
        new(85f, 85f),
        new(90f, 100f)
    ]);

    public static FanCurve CreateBalanced() => new([
        new(35f, 35f),
        new(50f, 45f),
        new(65f, 65f),
        new(75f, 85f),
        new(85f, 100f)
    ]);

    public static FanCurve CreatePerformance() => new([
        new(35f, 50f),
        new(50f, 65f),
        new(65f, 85f),
        new(75f, 100f)
    ]);
}
