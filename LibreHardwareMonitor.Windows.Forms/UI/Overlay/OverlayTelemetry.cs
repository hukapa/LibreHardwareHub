// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

public enum OverlaySeverity : byte
{
    None,
    Cool,
    Warm,
    Hot
}

/// <summary>What the HUD shows for one reading: the text, an optional 0..1 bar ratio and its severity.</summary>
public readonly struct OverlayReading
{
    public OverlayReading(string text, float? ratio = null, OverlaySeverity severity = OverlaySeverity.None)
    {
        Text = text;
        Ratio = ratio;
        Severity = severity;
    }

    public string Text { get; }
    public float? Ratio { get; }
    public OverlaySeverity Severity { get; }
}

/// <summary>
/// Feeds the HUD and its preview. The sensors behind each reading are resolved once and cached (they are
/// re-resolved every few seconds, since sensors can show up after their hardware), so a tick only reads
/// values. <see cref="Update" /> reports whether any visible text changed, letting the HUD skip redraws.
/// </summary>
public sealed class OverlayTelemetry
{
    private const int RebindTicks = 10;

    private readonly OverlayConfig _config;
    private readonly Dictionary<string, OverlayReading> _readings = new();
    private readonly Dictionary<string, ISensor> _custom = new();
    private readonly List<ISensor> _scratch = new();
    private readonly List<ISensor> _cpuClocks = new();
    private readonly List<ISensor> _fans = new();
    private readonly List<ISensor> _netDown = new();
    private readonly List<ISensor> _netUp = new();

    private ISensor _cpuTemp, _cpuLoad, _cpuPower;
    private ISensor _gpuTemp, _gpuLoad, _gpuPower, _gpuClock, _gpuFan, _gpuFanControl, _vramUsed;
    private ISensor _ramUsed, _ramAvailable, _ramLoad;
    private int _age = RebindTicks;

    public OverlayTelemetry(OverlayConfig config)
    {
        _config = config;
    }

    /// <summary>Forces the sensors to be resolved again on the next update (hardware or metric list changed).</summary>
    public void Invalidate() => _age = RebindTicks;

    public bool TryGet(string id, out OverlayReading reading) => _readings.TryGetValue(id, out reading);

    /// <summary>Reads the enabled metrics. Returns true when at least one displayed text changed.</summary>
    public bool Update(IComputer computer)
    {
        if (computer == null)
            return false;

        if (++_age >= RebindTicks)
        {
            Bind(computer);
            _age = 0;
        }

        bool changed = false;
        foreach (OverlayMetricItem item in _config.MetricItems)
        {
            if (!item.Enabled)
                continue;

            OverlayReading next = Read(item);
            if (!_readings.TryGetValue(item.Id, out OverlayReading previous) || previous.Text != next.Text || previous.Severity != next.Severity)
                changed = true;

            _readings[item.Id] = next;
        }

        return changed;
    }

    private void Bind(IComputer computer)
    {
        IList<IHardware> hardware = computer.Hardware;

        IHardware cpu = SensorLookup.First(hardware, HardwareType.Cpu);
        _cpuTemp = SensorLookup.CpuTemperature(cpu);
        _cpuLoad = SensorLookup.FindOrFirst(cpu, SensorType.Load, "Total");
        _cpuPower = SensorLookup.FindOrFirst(cpu, SensorType.Power, "Package");
        _cpuClocks.Clear();
        if (cpu != null)
        {
            foreach (ISensor s in cpu.Sensors)
            {
                if (s.SensorType == SensorType.Clock && !SensorLookup.IsBusClock(s))
                    _cpuClocks.Add(s);
            }
        }

        // Only the primary GPU feeds the GPU readings: with a discrete card plus an integrated one, reading
        // every GPU made the idle integrated GPU overwrite the values of the card actually rendering the game.
        IHardware gpu = SensorLookup.PrimaryGpu(hardware);
        _gpuTemp = SensorLookup.GpuCoreTemperature(gpu);
        _gpuLoad = SensorLookup.GpuCoreLoad(gpu);
        _gpuPower = SensorLookup.FindOrFirst(gpu, SensorType.Power, "Package", "GPU");
        _gpuClock = SensorLookup.GpuCoreClock(gpu);
        _gpuFan = SensorLookup.FindOrFirst(gpu, SensorType.Fan);
        _gpuFanControl = SensorLookup.FindOrFirst(gpu, SensorType.Control);
        _vramUsed = SensorLookup.Find(gpu, SensorType.Data, "Memory Used");

        IHardware memory = SensorLookup.PhysicalMemory(hardware);
        _ramUsed = SensorLookup.Find(memory, SensorType.Data, "Memory Used", "Used");
        _ramAvailable = SensorLookup.Find(memory, SensorType.Data, "Memory Available", "Available");
        _ramLoad = SensorLookup.Find(memory, SensorType.Load, "Memory");

        _scratch.Clear();
        _fans.Clear();
        _netDown.Clear();
        _netUp.Clear();
        foreach (IHardware hw in hardware)
        {
            SensorLookup.Collect(hw, _scratch);
            if (hw.HardwareType == HardwareType.Network)
            {
                Add(_netDown, SensorLookup.Find(hw, SensorType.Throughput, "Download"));
                Add(_netUp, SensorLookup.Find(hw, SensorType.Throughput, "Upload"));
            }
            else if (hw.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.Cooler or HardwareType.EmbeddedController)
            {
                SensorLookup.Collect(hw, _fans, SensorType.Fan);
            }
        }

        _custom.Clear();
        foreach (OverlayMetricItem item in _config.MetricItems)
        {
            if (!item.IsCustom)
                continue;

            foreach (ISensor s in _scratch)
            {
                if (s.Identifier.ToString() == item.Id)
                {
                    _custom[item.Id] = s;
                    break;
                }
            }
        }

        _scratch.Clear();
    }

    private static void Add(List<ISensor> list, ISensor sensor)
    {
        if (sensor != null)
            list.Add(sensor);
    }

    private OverlayReading Read(OverlayMetricItem item)
    {
        switch (item.Id)
        {
            case "cpu_temp": return Temperature(_cpuTemp);
            case "cpu_load": return Load(_cpuLoad);
            case "cpu_power": return Plain(_cpuPower, "F0", " W");
            case "cpu_clock": return Plain(Highest(_cpuClocks), "F0", " MHz");
            case "gpu_temp": return Temperature(_gpuTemp);
            case "gpu_load": return Load(_gpuLoad);
            case "gpu_power": return Plain(_gpuPower, "F0", " W");
            case "gpu_clock": return Plain(_gpuClock, "F0", " MHz");
            case "gpu_fan": return _gpuFan?.Value != null ? Plain(_gpuFan, "F0", " RPM") : Plain(_gpuFanControl, "F0", " %");
            case "vram_used": return Gigabytes(_vramUsed);
            case "ram_used": return Memory(item.DisplayFormat);
            case "fan_rpm": return Plain(FirstSpinning(_fans), "F0", " RPM");
            case "net_speed": return new OverlayReading($"D {SensorLookup.FormatThroughput(Sum(_netDown))}  U {SensorLookup.FormatThroughput(Sum(_netUp))}");
            case "system_time": return new OverlayReading(DateTime.Now.ToString("HH:mm:ss"));
        }

        return _custom.TryGetValue(item.Id, out ISensor sensor) ? Custom(sensor) : new OverlayReading("--");
    }

    private OverlayReading Custom(ISensor sensor)
    {
        return sensor.SensorType switch
        {
            SensorType.Temperature => Temperature(sensor),
            SensorType.Load => Load(sensor),
            _ => new OverlayReading(SensorLookup.Format(sensor))
        };
    }

    private OverlayReading Temperature(ISensor sensor)
    {
        if (sensor?.Value is not float t)
            return new OverlayReading("-- °C");

        OverlaySeverity severity = t >= _config.TempCritC ? OverlaySeverity.Hot : t >= _config.TempWarnC ? OverlaySeverity.Warm : OverlaySeverity.Cool;
        return new OverlayReading($"{t:F0} °C", t / 100f, severity);
    }

    private static OverlayReading Load(ISensor sensor)
    {
        if (sensor?.Value is not float load)
            return new OverlayReading("-- %");

        return new OverlayReading($"{load:F0} %", load / 100f, LoadSeverity(load));
    }

    private static OverlaySeverity LoadSeverity(float percent)
    {
        return percent >= OverlayConfig.LoadCritPercent ? OverlaySeverity.Hot
            : percent >= OverlayConfig.LoadWarnPercent ? OverlaySeverity.Warm
            : OverlaySeverity.None;
    }

    private static OverlayReading Plain(ISensor sensor, string format, string unit)
    {
        return new OverlayReading(sensor?.Value is float v ? v.ToString(format) + unit : "--");
    }

    private static OverlayReading Gigabytes(ISensor sensor)
    {
        return new OverlayReading(SensorLookup.ToGigabytes(sensor?.Value) is float gb ? $"{gb:F1} GB" : "--");
    }

    private OverlayReading Memory(int format)
    {
        if (SensorLookup.ToGigabytes(_ramUsed?.Value) is not float used)
            return new OverlayReading("--");

        float total = used + SensorLookup.ToGigabytes(_ramAvailable?.Value).GetValueOrDefault();
        float percent = _ramLoad?.Value ?? (total > 0.1f ? used / total * 100f : 0f);
        string text = format switch
        {
            1 => $"{used:F1} GB",
            2 when total > 0.1f => $"{used:F1} / {total:F1} GB",
            _ => $"{used:F1} GB ({percent:F0}%)"
        };

        return new OverlayReading(text, percent / 100f, LoadSeverity(percent));
    }

    private static ISensor Highest(List<ISensor> sensors)
    {
        ISensor best = null;
        foreach (ISensor s in sensors)
        {
            if (s.Value.HasValue && (best == null || s.Value.Value > best.Value.GetValueOrDefault()))
                best = s;
        }

        return best;
    }

    private static ISensor FirstSpinning(List<ISensor> sensors)
    {
        foreach (ISensor s in sensors)
        {
            if (s.Value > 0)
                return s;
        }

        return null;
    }

    private static float Sum(List<ISensor> sensors)
    {
        float total = 0f;
        foreach (ISensor s in sensors)
            total += s.Value.GetValueOrDefault();

        return total;
    }
}
