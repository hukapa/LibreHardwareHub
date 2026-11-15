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

        IHardware cpu = SensorLookup.First(all, HardwareType.Cpu);
        IHardware gpu = SensorLookup.PrimaryGpu(all);
        IHardware memory = SensorLookup.PhysicalMemory(all);
        IHardware motherboard = SensorLookup.First(all, HardwareType.Motherboard);

        // 1. CPU Load
        if (cpu != null)
        {
            ISensor cpuLoad = SensorLookup.FindOrFirst(cpu, SensorType.Load, "Total", "CPU Total", "CPU");
            Add(metrics, "Carga CPU", cpu, cpuLoad);
        }

        // 2. CPU Temperature
        if (cpu != null)
        {
            ISensor cpuTemp = SensorLookup.FindOrFirst(cpu, SensorType.Temperature, "Package", "Core (Tctl/Tdie)", "Core Max", "Core Average", "Tdie", "Tctl", "CPU");
            Add(metrics, "Temp. CPU", cpu, cpuTemp);
        }

        // 3. GPU Load
        if (gpu != null)
        {
            ISensor gpuLoad = SensorLookup.FindOrFirst(gpu, SensorType.Load, "Core", "D3D 3D", "GPU Core", "GPU");
            Add(metrics, "Carga GPU", gpu, gpuLoad);
        }

        // 4. GPU Temperature
        if (gpu != null)
        {
            ISensor gpuTemp = SensorLookup.FindOrFirst(gpu, SensorType.Temperature, "Core", "GPU Core", "Hot Spot", "GPU");
            Add(metrics, "Temp. GPU", gpu, gpuTemp);
        }

        // 5. Memory (RAM)
        if (memory != null)
        {
            ISensor memLoad = SensorLookup.FindOrFirst(memory, SensorType.Load, "Memory");
            ISensor used = SensorLookup.Find(memory, SensorType.Data, "Memory Used");
            ISensor available = SensorLookup.Find(memory, SensorType.Data, "Memory Available");
            Add(metrics, "Memória RAM", memory, memLoad, () => FormatMemoryDetail(memory, used, available));
        }

        // 6. CPU Power
        if (cpu != null)
        {
            ISensor cpuPower = SensorLookup.FindOrFirst(cpu, SensorType.Power, "Package", "CPU Package", "Power");
            Add(metrics, "Consumo CPU", cpu, cpuPower);
        }

        // 7. CPU Voltage (Check CPU first, fallback to Motherboard SuperIO)
        if (cpu != null)
        {
            ISensor cpuVolt = SensorLookup.FindOrFirst(cpu, SensorType.Voltage, "CPU Core", "VCore", "Core");
            if (cpuVolt == null && motherboard != null)
                cpuVolt = SensorLookup.FindOrFirst(motherboard, SensorType.Voltage, "VCore", "CPU VCore", "CPU Core", "Core");

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
