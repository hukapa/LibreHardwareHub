// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI;

/// <summary>
/// Single place for picking representative hardware/sensors and formatting their values, shared by the
/// Bento cards, the KPI summary and the overlay so they always agree (and never re-implement the rules).
/// Allocation-free lookups: plain loops, no LINQ.
/// </summary>
public static class SensorLookup
{
    public static bool IsGpu(HardwareType type) => type is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    public static IHardware First(IEnumerable<IHardware> hardware, HardwareType type)
    {
        if (hardware != null)
        {
            foreach (IHardware hw in hardware)
            {
                if (hw.HardwareType == type)
                    return hw;
            }
        }

        return null;
    }

    /// <summary>The discrete GPU when there is one; the integrated GPU only as a fallback.</summary>
    public static IHardware PrimaryGpu(IEnumerable<IHardware> hardware)
    {
        return First(hardware, HardwareType.GpuNvidia) ?? First(hardware, HardwareType.GpuAmd) ?? First(hardware, HardwareType.GpuIntel);
    }

    /// <summary>The physical memory device (the one that is not the "Virtual Memory" device).</summary>
    public static IHardware PhysicalMemory(IEnumerable<IHardware> hardware)
    {
        IHardware fallback = null;
        if (hardware != null)
        {
            foreach (IHardware hw in hardware)
            {
                if (hw.HardwareType != HardwareType.Memory)
                    continue;

                // DIMM devices only carry SPD timings/temperatures; the usage sensors live on the "Total Memory" device.
                if (Find(hw, SensorType.Data, "Memory Used") != null && !IsVirtualMemory(hw))
                    return hw;

                fallback ??= hw;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Adds the sensors of <paramref name="hardware" /> and of all its sub-hardware to <paramref name="target" />,
    /// optionally only those of one <paramref name="type" />.
    /// </summary>
    public static void Collect(IHardware hardware, List<ISensor> target, SensorType? type = null)
    {
        if (hardware == null)
            return;

        foreach (ISensor s in hardware.Sensors)
        {
            if (type == null || s.SensorType == type)
                target.Add(s);
        }

        foreach (IHardware sub in hardware.SubHardware)
            Collect(sub, target, type);
    }

    /// <summary>
    /// First sensor of <paramref name="type" /> whose name contains one of the keywords (in priority order).
    /// Returns null when no keyword matches; use <see cref="FindOrFirst" /> to fall back to any sensor of the type.
    /// </summary>
    public static ISensor Find(IHardware hardware, SensorType type, params string[] keywords)
    {
        if (hardware == null)
            return null;

        foreach (string keyword in keywords)
        {
            ISensor match = FindOne(hardware, type, keyword);
            if (match != null)
                return match;
        }

        return null;
    }

    public static ISensor FindOrFirst(IHardware hardware, SensorType type, params string[] keywords)
    {
        return hardware == null ? null : Find(hardware, type, keywords) ?? FindOne(hardware, type, null);
    }

    private static ISensor FindOne(IHardware hardware, SensorType type, string keyword)
    {
        foreach (ISensor s in hardware.Sensors)
        {
            if (s.SensorType == type && (keyword == null || Contains(s.Name, keyword)))
                return s;
        }

        foreach (IHardware sub in hardware.SubHardware)
        {
            ISensor match = FindOne(sub, type, keyword);
            if (match != null)
                return match;
        }

        return null;
    }

    private static bool Contains(string text, string keyword) => text != null && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

    // ---- Representative sensors: the one place that decides which sensor stands for "the CPU temperature" and so on ----
    // Every card, summary, tuning page, fan page and the overlay asks here, so they can never disagree (a GPU temperature
    // card that read "Hot Spot" while captioned "GPU Core" was exactly that disagreement).

    private static readonly string[] CpuTemperatureKeywords = { "Package", "Tctl/Tdie", "Tdie", "Tctl", "Core Max", "Core Average" };
    private static readonly string[] CpuTemperatureExcluded = { "Distance to TjMax", "TjMax" };
    private static readonly string[] GpuTemperatureKeywords = { "GPU Core", "Core" };
    private static readonly string[] GpuTemperatureExcluded = { "Hot Spot", "Hotspot", "Memory", "Junction", "VR", "Limit" };
    private static readonly string[] GpuHotSpotKeywords = { "Hot Spot", "Hotspot" };
    private static readonly string[] GpuLoadKeywords = { "GPU Core", "Core", "D3D 3D" };
    private static readonly string[] GpuLoadExcluded = { "Memory", "Video", "Bus", "Copy", "Decode", "Encode", "Controller" };
    private static readonly string[] GpuClockKeywords = { "GPU Core", "Core" };
    private static readonly string[] GpuClockExcluded = { "Memory", "Shader" };

    /// <summary>CPU package temperature: Package, Tctl/Tdie, Core Max or Core Average; never a "Distance to TjMax" reading.</summary>
    public static ISensor CpuTemperature(IHardware cpu) => FindPreferred(cpu, SensorType.Temperature, CpuTemperatureKeywords, CpuTemperatureExcluded);

    /// <summary>GPU core temperature. "Hot Spot", memory, junction, VR and limit sensors are only used when nothing else exists.</summary>
    public static ISensor GpuCoreTemperature(IHardware gpu) => FindPreferred(gpu, SensorType.Temperature, GpuTemperatureKeywords, GpuTemperatureExcluded);

    /// <summary>GPU hot spot temperature; null when the GPU has none (it is never a stand-in for the core temperature).</summary>
    public static ISensor GpuHotSpotTemperature(IHardware gpu) => Find(gpu, SensorType.Temperature, GpuHotSpotKeywords);

    /// <summary>GPU core load, not the load of a secondary engine (memory controller, video, bus, copy).</summary>
    public static ISensor GpuCoreLoad(IHardware gpu) => FindPreferred(gpu, SensorType.Load, GpuLoadKeywords, GpuLoadExcluded);

    /// <summary>GPU core clock, not the memory or shader clock.</summary>
    public static ISensor GpuCoreClock(IHardware gpu) => FindPreferred(gpu, SensorType.Clock, GpuClockKeywords, GpuClockExcluded);

    /// <summary>The CPU "Bus Speed" (100 MHz) is a clock sensor too, but never a core clock.</summary>
    public static bool IsBusClock(ISensor sensor) => sensor != null && Contains(sensor.Name, "Bus");

    /// <summary>Highest CPU core clock, ignoring the bus speed.</summary>
    public static float? CpuPeakClock(IHardware cpu)
    {
        float? peak = null;
        if (cpu == null)
            return null;

        foreach (ISensor s in cpu.Sensors)
        {
            if (s.SensorType == SensorType.Clock && !IsBusClock(s) && s.Value > peak.GetValueOrDefault())
                peak = s.Value;
        }

        return peak;
    }

    /// <summary>The "Virtual Memory" device (page file), which must never be mixed up with the physical memory.</summary>
    public static bool IsVirtualMemory(IHardware hardware) => hardware != null && Contains(hardware.Name, "Virtual");

    /// <summary>True when the sensor name contains one of the keywords.</summary>
    public static bool NameContains(ISensor sensor, params string[] keywords)
    {
        if (sensor == null)
            return false;

        foreach (string keyword in keywords)
        {
            if (Contains(sensor.Name, keyword))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Sensor of <paramref name="type" /> matching the keywords in priority order while skipping the excluded names; then any
    /// non-excluded sensor of the type; and only as a last resort an excluded one (so a GPU that exposes nothing but a
    /// "Hot Spot" still shows a value, and the caller can caption it with <see cref="ISensor.Name" />).
    /// </summary>
    private static ISensor FindPreferred(IHardware hardware, SensorType type, string[] keywords, string[] excluded)
    {
        if (hardware == null)
            return null;

        foreach (string keyword in keywords)
        {
            ISensor match = FindFiltered(hardware, type, keyword, excluded);
            if (match != null)
                return match;
        }

        return FindFiltered(hardware, type, null, excluded) ?? FindOne(hardware, type, null);
    }

    private static ISensor FindFiltered(IHardware hardware, SensorType type, string keyword, string[] excluded)
    {
        foreach (ISensor s in hardware.Sensors)
        {
            if (s.SensorType == type && (keyword == null || Contains(s.Name, keyword)) && !IsExcluded(s.Name, excluded))
                return s;
        }

        foreach (IHardware sub in hardware.SubHardware)
        {
            ISensor match = FindFiltered(sub, type, keyword, excluded);
            if (match != null)
                return match;
        }

        return null;
    }

    private static bool IsExcluded(string name, string[] excluded)
    {
        foreach (string word in excluded)
        {
            if (Contains(name, word))
                return true;
        }

        return false;
    }

    /// <summary>Normalizes a memory/data reading to gigabytes whatever unit the driver reports it in.</summary>
    public static float? ToGigabytes(float? value)
    {
        if (!value.HasValue)
            return null;

        float v = value.Value;
        if (v > 100_000_000f) // raw bytes
            return v / (1024f * 1024f * 1024f);
        if (v > 50_000f) // kilobytes
            return v / (1024f * 1024f);
        if (v > 100f) // megabytes
            return v / 1024f;

        return v; // already gigabytes
    }

    public static string FormatThroughput(float bytesPerSecond)
    {
        if (bytesPerSecond < 1024f * 1024f)
            return $"{Math.Max(0f, bytesPerSecond) / 1024f:F0} KB/s";

        return $"{bytesPerSecond / (1024f * 1024f):F1} MB/s";
    }

    /// <summary>Value with its unit, at the precision that makes sense for the sensor type.</summary>
    public static string Format(ISensor sensor)
    {
        if (sensor?.Value is not float v)
            return "--";

        return sensor.SensorType switch
        {
            SensorType.Voltage => $"{v:F3} V",
            SensorType.Current => $"{v:F2} A",
            SensorType.Power => $"{v:F1} W",
            SensorType.Clock => $"{v:F0} MHz",
            SensorType.Temperature => $"{v:F1} °C",
            SensorType.Load or SensorType.Humidity => $"{v:F1} %",
            SensorType.Control or SensorType.Level => $"{v:F0} %",
            SensorType.Frequency => $"{v:F0} Hz",
            SensorType.Fan => $"{v:F0} RPM",
            SensorType.Flow => $"{v:F0} L/h",
            SensorType.Data => $"{ToGigabytes(v).GetValueOrDefault():F1} GB",
            SensorType.Throughput => FormatThroughput(v),
            SensorType.TimeSpan => TimeSpan.FromSeconds(v).ToString("g"),
            SensorType.Timing => $"{v:F0} ns",
            SensorType.Energy => $"{v:F0} mWh",
            SensorType.Noise => $"{v:F0} dBA",
            SensorType.Factor => $"{v:F2}",
            _ => $"{v:F1}"
        };
    }

    /// <summary>Portuguese group caption for a sensor type (used to group the detailed lists).</summary>
    public static string GroupName(SensorType type)
    {
        return type switch
        {
            SensorType.Temperature => "Temperaturas",
            SensorType.Load => "Cargas",
            SensorType.Clock or SensorType.Frequency => "Frequências",
            SensorType.Voltage => "Tensões",
            SensorType.Current => "Correntes",
            SensorType.Power or SensorType.Energy => "Potência",
            SensorType.Fan or SensorType.Flow => "Ventoinhas",
            SensorType.Control => "Controlos",
            SensorType.Level => "Níveis",
            SensorType.Data => "Dados",
            SensorType.Throughput => "Débito",
            SensorType.Timing or SensorType.TimeSpan => "Temporizações",
            _ => "Outros"
        };
    }
}
