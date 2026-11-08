// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public sealed class FanControlEngine
{
    private static readonly List<IControl> _registeredControls = new();
    private static readonly object _lock = new();

    private readonly IComputer _computer;
    private readonly PersistentSettings _settings;
    private readonly List<FanChannelRuntime> _channels = new();
    private readonly Dictionary<string, ISensor> _tempSensorsById = new(StringComparer.OrdinalIgnoreCase);
    private ISensor _primaryCpuTempSensor;
    private ISensor _primaryGpuTempSensor;
    private long _lastUpdateTicks;

    public IReadOnlyList<FanChannelRuntime> Channels => _channels;

    public IComputer Computer => _computer;

    public GlobalFanPreset ActivePreset { get; private set; } = GlobalFanPreset.DefaultBios;

    public bool HasActiveControls => _channels.Any(c => c.Config.Mode != FanChannelMode.DefaultBios || c.InEmergencyOverride);

    public bool HasEmergencyAlert => _channels.Any(c => c.InEmergencyOverride);

    public float LastCpuTemp { get; private set; } = 40.0f;

    public float LastGpuTemp { get; private set; } = 40.0f;

    public event EventHandler ChannelsChanged;

    public event EventHandler PresetChanged;

    public FanControlEngine(IComputer computer, PersistentSettings settings)
    {
        _computer = computer ?? throw new ArgumentNullException(nameof(computer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        RegisterDeadManSwitch();
        DiscoverChannels();

        computer.HardwareAdded += _ => DiscoverChannels();
        computer.HardwareRemoved += _ => DiscoverChannels();

        LoadSettings();
    }

    private static void RegisterDeadManSwitch()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreAllControlsToDefault();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreAllControlsToDefault();
        try
        {
            Microsoft.Win32.SystemEvents.SessionEnded += (_, _) => RestoreAllControlsToDefault();
        }
        catch
        {
        }
    }

    public static void RestoreAllControlsToDefault()
    {
        lock (_lock)
        {
            foreach (IControl control in _registeredControls)
            {
                try
                {
                    control.SetDefault();
                }
                catch
                {
                }
            }
        }
    }

    public void DiscoverChannels()
    {
        lock (_lock)
        {
            List<IControl> discoveredControls = new();
            List<ISensor> discoveredRpmSensors = new();

            _tempSensorsById.Clear();
            _primaryCpuTempSensor = null;
            _primaryGpuTempSensor = null;

            void Collect(IHardware hardware)
            {
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Control && sensor.Control != null)
                        discoveredControls.Add(sensor.Control);
                    else if (sensor.SensorType == SensorType.Fan)
                        discoveredRpmSensors.Add(sensor);
                    else if (sensor.SensorType == SensorType.Temperature)
                    {
                        string sId = sensor.Identifier.ToString();
                        _tempSensorsById[sId] = sensor;

                        if (hardware.HardwareType == HardwareType.Cpu)
                        {
                            if (_primaryCpuTempSensor == null)
                                _primaryCpuTempSensor = sensor;
                            else if (sensor.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     sensor.Name.IndexOf("Core Max", StringComparison.OrdinalIgnoreCase) >= 0)
                                _primaryCpuTempSensor = sensor;
                        }
                        else if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                        {
                            if (_primaryGpuTempSensor == null)
                                _primaryGpuTempSensor = sensor;
                            else if (sensor.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     sensor.Name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0)
                                _primaryGpuTempSensor = sensor;
                        }
                    }
                }

                foreach (IHardware sub in hardware.SubHardware)
                    Collect(sub);
            }

            foreach (IHardware hw in _computer.Hardware)
                Collect(hw);

            bool modified = false;

            foreach (IControl control in discoveredControls)
            {
                if (!_registeredControls.Contains(control))
                    _registeredControls.Add(control);

                string id = control.Identifier.ToString();
                if (!_channels.Any(c => c.Control.Identifier.ToString() == id))
                {
                    FanChannelConfig config = LoadChannelConfig(id);
                    ISensor matchingRpm = FindMatchingRpmSensor(control, discoveredRpmSensors);
                    _channels.Add(new FanChannelRuntime(control, config, matchingRpm));
                    modified = true;
                }
            }

            if (modified)
                ChannelsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static ISensor FindMatchingRpmSensor(IControl control, List<ISensor> rpmSensors)
    {
        if (control.Sensor == null)
            return null;

        // Exact match only (same chip, same index): falling back to "any fan of the chip" showed the RPM
        // of a different fan.
        IHardware hw = control.Sensor.Hardware;
        return rpmSensors.FirstOrDefault(rpm => rpm.Hardware == hw && rpm.Index == control.Sensor.Index);
    }

    /// <summary>
    /// Super I/O chips only create a fan RPM sensor after its first non-zero reading, which is after the
    /// channels were discovered; keep looking until the matching sensor exists.
    /// </summary>
    private static void ResolveMissingRpmSensor(FanChannelRuntime channel)
    {
        IHardware hw = channel.Control.Sensor?.Hardware;
        if (channel.RpmSensor != null || hw == null)
            return;

        channel.RpmSensor = hw.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Fan && s.Index == channel.Control.Sensor.Index);
    }

    public void Update()
    {
        long now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        float deltaSeconds = _lastUpdateTicks > 0 ? (float)(now - _lastUpdateTicks) / 1000f : 1.0f;
        _lastUpdateTicks = now;

        ResolveTemperatures();

        foreach (FanChannelRuntime channel in _channels)
        {
            ResolveMissingRpmSensor(channel);
            float? targetTemp = ResolveChannelTemperature(channel.Config.TemperatureSource);
            channel.Update(targetTemp, now, deltaSeconds);
        }
    }

    private void ResolveTemperatures()
    {
        if (_primaryCpuTempSensor?.Value != null)
            LastCpuTemp = _primaryCpuTempSensor.Value.Value;

        if (_primaryGpuTempSensor?.Value != null)
            LastGpuTemp = _primaryGpuTempSensor.Value.Value;
    }

    public float? ResolveChannelTemperature(string source)
    {
        if (string.Equals(source, FanChannelConfig.SourceCpu, StringComparison.OrdinalIgnoreCase))
            return LastCpuTemp;

        if (string.Equals(source, FanChannelConfig.SourceGpu, StringComparison.OrdinalIgnoreCase))
            return LastGpuTemp;

        if (string.Equals(source, FanChannelConfig.SourceMaxCpuGpu, StringComparison.OrdinalIgnoreCase))
            return Math.Max(LastCpuTemp, LastGpuTemp);

        if (_tempSensorsById.TryGetValue(source, out ISensor s) && s.Value != null)
            return s.Value.Value;

        return Math.Max(LastCpuTemp, LastGpuTemp);
    }

    public void ApplyPreset(GlobalFanPreset preset)
    {
        ActivePreset = preset;

        foreach (FanChannelRuntime channel in _channels)
        {
            switch (preset)
            {
                case GlobalFanPreset.DefaultBios:
                    channel.Config.Mode = FanChannelMode.DefaultBios;
                    channel.ResetToDefault();
                    break;

                case GlobalFanPreset.Silent:
                    channel.Config.Mode = FanChannelMode.Curve;
                    channel.Config.Curve = FanCurve.CreateSilent();
                    break;

                case GlobalFanPreset.Balanced:
                    channel.Config.Mode = FanChannelMode.Curve;
                    channel.Config.Curve = FanCurve.CreateBalanced();
                    break;

                case GlobalFanPreset.Performance:
                    channel.Config.Mode = FanChannelMode.Curve;
                    channel.Config.Curve = FanCurve.CreatePerformance();
                    break;

                case GlobalFanPreset.Custom:
                    break;
            }
        }

        SaveSettings();
        PresetChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCustomMode()
    {
        if (ActivePreset != GlobalFanPreset.Custom)
        {
            ActivePreset = GlobalFanPreset.Custom;
            PresetChanged?.Invoke(this, EventArgs.Empty);
        }
        SaveSettings();
    }

    public void SaveSettings()
    {
        _settings.SetValue("fancontrol.preset", (int)ActivePreset);

        foreach (FanChannelRuntime ch in _channels)
        {
            string prefix = "fancontrol.ch." + ch.Control.Identifier.ToString() + ".";
            _settings.SetValue(prefix + "mode", (int)ch.Config.Mode);
            _settings.SetValue(prefix + "fixedPwm", ch.Config.FixedPwm.ToString(CultureInfo.InvariantCulture));
            _settings.SetValue(prefix + "source", ch.Config.TemperatureSource);
            _settings.SetValue(prefix + "curve", ch.Config.Curve.Serialize());
            _settings.SetValue(prefix + "respTime", ch.Config.ResponseTimeSeconds.ToString(CultureInfo.InvariantCulture));
            _settings.SetValue(prefix + "hystUp", ch.Config.HysteresisUpC.ToString(CultureInfo.InvariantCulture));
            _settings.SetValue(prefix + "hystDown", ch.Config.HysteresisDownC.ToString(CultureInfo.InvariantCulture));
            _settings.SetValue(prefix + "stepUp", ch.Config.MaxStepUpPercent.ToString(CultureInfo.InvariantCulture));
            _settings.SetValue(prefix + "stepDown", ch.Config.MaxStepDownPercent.ToString(CultureInfo.InvariantCulture));
        }
    }

    private FanChannelConfig LoadChannelConfig(string channelId)
    {
        string prefix = "fancontrol.ch." + channelId + ".";
        FanChannelConfig config = new() { ChannelId = channelId };

        config.Mode = (FanChannelMode)_settings.GetValue(prefix + "mode", (int)FanChannelMode.DefaultBios);
        config.TemperatureSource = _settings.GetValue(prefix + "source", FanChannelConfig.SourceMaxCpuGpu);

        string fixedStr = _settings.GetValue(prefix + "fixedPwm", "50.0");
        if (float.TryParse(fixedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float fixedPwm))
            config.FixedPwm = fixedPwm;

        string curveStr = _settings.GetValue(prefix + "curve", string.Empty);
        if (!string.IsNullOrEmpty(curveStr))
            config.Curve = FanCurve.Deserialize(curveStr);

        return config;
    }

    private void LoadSettings()
    {
        ActivePreset = (GlobalFanPreset)_settings.GetValue("fancontrol.preset", (int)GlobalFanPreset.DefaultBios);
    }
}
