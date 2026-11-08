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

    public const float MinTemperature = 20f;
    public const float MaxTemperature = 100f;

    /// <summary>The last point may not be later than this: the curve must reach 100 % by then.</summary>
    public const float FullSpeedByTemperature = 90f;

    public const int MaxPoints = 8;

    /// <summary>
    /// Returns a curve that is always safe, whatever came in (editor, config file, presets): at most
    /// <see cref="MaxPoints" /> points, temperatures inside 20-100 C and at least 1 C apart, duty between
    /// <paramref name="minPwm" /> and 100 %, never decreasing as the temperature rises, and ending at 100 %
    /// no later than <see cref="FullSpeedByTemperature" />.
    /// </summary>
    public static FanCurve Sanitize(IEnumerable<FanCurvePoint> input, float minPwm)
    {
        minPwm = Math.Max(0f, Math.Min(100f, minPwm));

        List<FanCurvePoint> sorted = (input ?? Enumerable.Empty<FanCurvePoint>())
            .Where(p => !float.IsNaN(p.Temperature) && !float.IsNaN(p.PwmPercent))
            .Select(p => new FanCurvePoint(
                (float)Math.Round(Math.Max(MinTemperature, Math.Min(MaxTemperature, p.Temperature))),
                (float)Math.Round(Math.Max(minPwm, Math.Min(100f, p.PwmPercent)))))
            .OrderBy(p => p.Temperature)
            .ToList();

        // One point per degree (the later one wins) and a limited number of points.
        List<FanCurvePoint> points = new();
        foreach (FanCurvePoint p in sorted)
        {
            if (points.Count > 0 && p.Temperature - points[points.Count - 1].Temperature < 1f)
                points[points.Count - 1] = p;
            else
                points.Add(p);
        }

        while (points.Count > MaxPoints)
            points.RemoveAt(points.Count / 2);

        if (points.Count < 2)
            return CreateBalanced();

        // Duty never decreases with temperature.
        float floor = minPwm;
        for (int i = 0; i < points.Count; i++)
        {
            floor = Math.Max(floor, points[i].PwmPercent);
            points[i] = new FanCurvePoint(points[i].Temperature, floor);
        }

        // Full speed has to be reached by the end of the curve.
        FanCurvePoint last = points[points.Count - 1];
        points[points.Count - 1] = new FanCurvePoint(Math.Min(last.Temperature, FullSpeedByTemperature), 100f);
        if (points.Count >= 2 && points[points.Count - 2].Temperature >= points[points.Count - 1].Temperature)
            points[points.Count - 2] = new FanCurvePoint(points[points.Count - 1].Temperature - 1f, points[points.Count - 2].PwmPercent);

        return new FanCurve(points);
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

        return points.Count > 0 ? Sanitize(points, 0f) : CreateBalanced();
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
