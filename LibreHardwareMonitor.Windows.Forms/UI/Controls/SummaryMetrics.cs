// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI.Controls;

/// <summary>A headline metric shown as a card in the summary strip.</summary>
public sealed class SummaryMetric
{
    public SummaryMetric(string title, IHardware hardware, ISensor sensor, Func<string> detail = null)
    {
        Title = title;
        Hardware = hardware;
        Sensor = sensor;
        Detail = detail ?? (() => hardware?.Name ?? string.Empty);
    }

    public string Title { get; }
    public IHardware Hardware { get; }
    public ISensor Sensor { get; }
    public Func<string> Detail { get; }
}

/// <summary>
/// Picks the most representative sensors (CPU, GPU, memory, power, voltage) from the detected hardware,
/// using robust recursive hardware discovery and flexible name matching.
/// </summary>
public static class SummaryMetrics
{
    public static IList<SummaryMetric> Resolve(IEnumerable<IHardware> hardware)
    {
        if (hardware == null)
            return Array.Empty<SummaryMetric>();

        List<IHardware> all = hardware.ToList();
        List<SummaryMetric> metrics = new();

        IHardware cpu = all.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        IHardware gpu = all.FirstOrDefault(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                        ?? all.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel);
        IHardware memory = all.FirstOrDefault(h => h.HardwareType == HardwareType.Memory && h.Name.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) < 0)
                           ?? all.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        IHardware motherboard = all.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);

        // 1. CPU Load
        if (cpu != null)
        {
            ISensor cpuLoad = FindSensor(cpu, SensorType.Load, "Total", "CPU Total", "CPU");
            Add(metrics, "Carga CPU", cpu, cpuLoad);
        }

        // 2. CPU Temperature
        if (cpu != null)
        {
            ISensor cpuTemp = FindSensor(cpu, SensorType.Temperature, "Package", "Core (Tctl/Tdie)", "Core Max", "Core Average", "Tdie", "Tctl", "CPU");
            Add(metrics, "Temp. CPU", cpu, cpuTemp);
        }

        // 3. GPU Load
        if (gpu != null)
        {
            ISensor gpuLoad = FindSensor(gpu, SensorType.Load, "Core", "D3D 3D", "GPU Core", "GPU");
            Add(metrics, "Carga GPU", gpu, gpuLoad);
        }

        // 4. GPU Temperature
        if (gpu != null)
        {
            ISensor gpuTemp = FindSensor(gpu, SensorType.Temperature, "Core", "GPU Core", "Hot Spot", "GPU");
            Add(metrics, "Temp. GPU", gpu, gpuTemp);
        }

        // 5. Memory (RAM)
        if (memory != null)
        {
            ISensor memLoad = FindSensor(memory, SensorType.Load, "Memory");
            ISensor used = FindExactSensor(memory, SensorType.Data, "Memory Used");
            ISensor available = FindExactSensor(memory, SensorType.Data, "Memory Available");
            Add(metrics, "Memória RAM", memory, memLoad, () => FormatMemoryDetail(memory, used, available));
        }

        // 6. CPU Power
        if (cpu != null)
        {
            ISensor cpuPower = FindSensor(cpu, SensorType.Power, "Package", "CPU Package", "Power");
            Add(metrics, "Consumo CPU", cpu, cpuPower);
        }

        // 7. CPU Voltage (Check CPU first, fallback to Motherboard SuperIO)
        if (cpu != null)
        {
            ISensor cpuVolt = FindSensor(cpu, SensorType.Voltage, "CPU Core", "VCore", "Core");
            if (cpuVolt == null && motherboard != null)
                cpuVolt = FindSensor(motherboard, SensorType.Voltage, "VCore", "CPU VCore", "CPU Core", "Core");

            if (cpuVolt != null)
                Add(metrics, "Voltagem CPU", cpu, cpuVolt);
        }

        return metrics;
    }

    private static void Add(List<SummaryMetric> metrics, string title, IHardware hardware, ISensor sensor, Func<string> detail = null)
    {
        if (sensor != null && hardware != null)
            metrics.Add(new SummaryMetric(title, hardware, sensor, detail));
    }

    private static IEnumerable<ISensor> EnumerateSensors(IHardware hardware)
    {
        if (hardware == null)
            yield break;

        foreach (ISensor s in hardware.Sensors)
            yield return s;

        if (hardware.SubHardware != null)
        {
            foreach (IHardware sub in hardware.SubHardware)
            {
                foreach (ISensor s in EnumerateSensors(sub))
                    yield return s;
            }
        }
    }

    private static ISensor FindExactSensor(IHardware hardware, SensorType type, string name)
    {
        return EnumerateSensors(hardware).FirstOrDefault(s => s.SensorType == type && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static ISensor FindSensor(IHardware hardware, SensorType type, params string[] preferredKeywords)
    {
        List<ISensor> ofType = EnumerateSensors(hardware).Where(s => s.SensorType == type).ToList();
        if (ofType.Count == 0)
            return null;

        foreach (string keyword in preferredKeywords)
        {
            ISensor match = ofType.FirstOrDefault(s => s.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match != null)
                return match;
        }

        return ofType.FirstOrDefault();
    }

    private static string FormatMemoryDetail(IHardware memory, ISensor used, ISensor available)
    {
        if (used?.Value is float u && available?.Value is float a)
        {
            // If values are reported in raw bytes (> 1MB), scale them
            float total = u + a;
            if (u > 1_000_000f)
            {
                const float toGb = 1024f * 1024f * 1024f;
                return $"{u / toGb:F1} GB / {total / toGb:F1} GB";
            }
            return $"{u:F1} GB / {total:F1} GB";
        }

        return memory?.Name ?? "RAM";
    }
}
