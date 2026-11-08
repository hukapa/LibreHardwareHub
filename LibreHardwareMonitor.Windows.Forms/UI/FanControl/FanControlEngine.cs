// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>
/// Drives the fan channels. Safety rules that apply to every managed channel regardless of its own source:
/// a global thermal emergency, a lost temperature sensor, stalled-fan detection and per-role minimums.
/// Channels that are not managed stay under the BIOS (or the user's explicit tree / HTTP commands).
/// </summary>
public sealed class FanControlEngine
{
    // Global thermal limits (all managed channels go to 100 % above these, released with hysteresis).
    private const float CpuEmergencyC = 90f;
    private const float CpuEmergencyReleaseC = 82f;
    private const float GpuEmergencyC = 85f;
    private const float GpuEmergencyReleaseC = 77f;
    private const long SensorStaleMs = 5000;

    private const string DirtyKey = "fancontrol.dirty_boot";

    private static readonly List<IControl> _registeredControls = new();
    private static readonly object _lock = new();
    private static FanControlEngine _instance;

    private readonly IComputer _computer;
    private readonly PersistentSettings _settings;
    private readonly SynchronizationContext _ui;
    private readonly object _sync = new();
    private readonly List<FanChannelRuntime> _channels = new();
    private readonly Dictionary<string, ISensor> _tempSensorsById = new(StringComparer.OrdinalIgnoreCase);
    private ISensor _primaryCpuTempSensor;
    private ISensor _primaryGpuTempSensor;
    private long _lastUpdateTicks;
    private readonly long _startMs = Environment.TickCount64;
    private long _cpuLastValidMs;
    private long _gpuLastValidMs;
    private bool _globalEmergency;
    private bool _dirtyMarked;
    private string _alertSignature = string.Empty;

    /// <summary>Snapshot of the channels (the list may grow on another thread when hardware is added).</summary>
    public IReadOnlyList<FanChannelRuntime> Channels { get { lock (_sync) return _channels.ToArray(); } }

    public IComputer Computer => _computer;

    public GlobalFanPreset ActivePreset { get; private set; } = GlobalFanPreset.DefaultBios;

    public bool HasActiveControls { get { lock (_sync) return _channels.Any(c => c.IsManaged || c.IsIdentifying || c.InEmergencyOverride); } }

    public bool HasEmergencyAlert { get { lock (_sync) return _channels.Any(c => c.InEmergencyOverride); } }

    public float LastCpuTemp { get; private set; } = 40.0f;

    public float LastGpuTemp { get; private set; } = 40.0f;

    /// <summary>True when the previous session ended without restoring the fans (killed, crash, power loss).</summary>
    public bool PreviousSessionUnclean { get; private set; }

    /// <summary>"Channel: alert" lines for every channel that currently has an alert.</summary>
    public IReadOnlyList<string> CurrentAlerts { get; private set; } = Array.Empty<string>();

    /// <summary>Raised on the UI thread.</summary>
    public event EventHandler ChannelsChanged;

    /// <summary>Raised on the UI thread.</summary>
    public event EventHandler PresetChanged;

    /// <summary>Raised on the UI thread when the set of alerts changes.</summary>
    public event EventHandler AlertsChanged;

    public FanControlEngine(IComputer computer, PersistentSettings settings)
    {
        _computer = computer ?? throw new ArgumentNullException(nameof(computer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _ui = SynchronizationContext.Current;
        _instance = this;

        CheckPreviousSession();
        RegisterDeadManSwitch();
        DiscoverChannels();

        computer.HardwareAdded += _ => DiscoverChannels();
        computer.HardwareRemoved += _ => DiscoverChannels();

        LoadSettings();
    }

    private void Raise(EventHandler handler)
    {
        if (handler == null)
            return;

        if (_ui != null)
            _ui.Post(_ => handler(this, EventArgs.Empty), null);
        else
            handler(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------ safety net

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

        _instance?.ClearDirtyMarker();
    }

    private static long BootStampTicks() => (DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64)).Ticks;

    /// <summary>
    /// The Super I/O keeps the last manual duty if the app is killed. A marker with the boot time is written
    /// to disk as soon as the app takes manual control, so the next start can warn (a reboot clears the state).
    /// </summary>
    private void CheckPreviousSession()
    {
        if (long.TryParse(_settings.GetValue(DirtyKey, "0"), out long stamp) && stamp != 0)
            PreviousSessionUnclean = Math.Abs(BootStampTicks() - stamp) < TimeSpan.FromMinutes(2).Ticks;

        _settings.SetValue(DirtyKey, "0");
    }

    private void UpdateDirtyMarker()
    {
        bool holds = _channels.Any(c => c.HoldsManualControl);
        if (holds == _dirtyMarked)
            return;

        _dirtyMarked = holds;
        _settings.SetValue(DirtyKey, holds ? BootStampTicks().ToString(CultureInfo.InvariantCulture) : "0");
        FlushSettings();
    }

    private void ClearDirtyMarker()
    {
        if (!_dirtyMarked)
            return;

        _dirtyMarked = false;
        _settings.SetValue(DirtyKey, "0");
        FlushSettings();
    }

    private void FlushSettings()
    {
        try
        {
            _settings.Save(Path.ChangeExtension(Application.ExecutablePath, ".config"));
        }
        catch
        {
            // Best effort: the marker is a warning aid, not a requirement.
        }
    }

    // ------------------------------------------------------------------ discovery

    public void DiscoverChannels()
    {
        bool modified = false;

        lock (_sync)
        {
            List<IControl> discoveredControls = new();
            List<ISensor> discoveredRpmSensors = new();

            ISensor previousCpu = _primaryCpuTempSensor;
            ISensor previousGpu = _primaryGpuTempSensor;
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
                        _tempSensorsById[sensor.Identifier.ToString()] = sensor;

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

            // A newly found sensor starts "fresh" so it is not reported lost before its first reading.
            long now = Environment.TickCount64;
            if (!ReferenceEquals(previousCpu, _primaryCpuTempSensor))
                _cpuLastValidMs = now;
            if (!ReferenceEquals(previousGpu, _primaryGpuTempSensor))
                _gpuLastValidMs = now;

            foreach (IControl control in discoveredControls)
            {
                lock (_lock)
                {
                    if (!_registeredControls.Contains(control))
                        _registeredControls.Add(control);
                }

                string id = control.Identifier.ToString();
                if (!_channels.Any(c => c.Control.Identifier.ToString() == id))
                {
                    FanChannelConfig config = LoadChannelConfig(id, control);
                    ISensor matchingRpm = FindMatchingRpmSensor(control, discoveredRpmSensors);
                    _channels.Add(new FanChannelRuntime(control, config, matchingRpm));
                    modified = true;
                }
            }
        }

        if (modified)
            Raise(ChannelsChanged);
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

    /// <summary>
    /// Generic first guess for the role. Only a GPU is assigned automatically (the hardware says so);
    /// everything else stays "unidentified" until the user confirms, because only the user knows what is plugged in.
    /// </summary>
    public static FanChannelRole SuggestRole(IControl control)
    {
        IHardware hw = control.Sensor?.Hardware;
        if (hw?.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
            return FanChannelRole.Gpu;

        return FanChannelRole.Unassigned;
    }

    /// <summary>Hint shown next to an unidentified channel, derived from the header name when the board names it.</summary>
    public static string RoleHint(IControl control)
    {
        string name = control.Sensor?.Name ?? string.Empty;
        if (name.IndexOf("pump", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("aio", StringComparison.OrdinalIgnoreCase) >= 0)
            return "O nome indica uma bomba";
        if (name.IndexOf("cpu", StringComparison.OrdinalIgnoreCase) >= 0)
            return "O nome indica o cooler do CPU";
        if (name.IndexOf("sys", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("case", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("chassis", StringComparison.OrdinalIgnoreCase) >= 0)
            return "O nome indica uma ventoinha da caixa";

        return string.Empty;
    }

    // ------------------------------------------------------------------ control loop

    public void Update()
    {
        bool alertsChanged = false;

        lock (_sync)
        {
            long now = Environment.TickCount64;
            float deltaSeconds = _lastUpdateTicks > 0 ? (float)(now - _lastUpdateTicks) / 1000f : 1.0f;
            _lastUpdateTicks = now;

            ResolveTemperatures(now);
            UpdateGlobalEmergency(now);

            foreach (FanChannelRuntime channel in _channels)
            {
                ResolveMissingRpmSensor(channel);
                float? targetTemp = ResolveChannelTemperature(channel.Config.TemperatureSource, now);
                channel.Update(targetTemp, now, deltaSeconds, _globalEmergency);
            }

            UpdateDirtyMarker();

            List<string> alerts = _channels.Where(c => c.Alert != null).Select(c => c.DisplayName + ": " + c.Alert).ToList();
            string signature = string.Join("|", alerts);
            if (signature != _alertSignature)
            {
                _alertSignature = signature;
                CurrentAlerts = alerts;
                alertsChanged = true;
            }
        }

        if (alertsChanged)
            Raise(AlertsChanged);
    }

    private void ResolveTemperatures(long now)
    {
        if (_primaryCpuTempSensor?.Value != null)
        {
            LastCpuTemp = _primaryCpuTempSensor.Value.Value;
            _cpuLastValidMs = now;
        }

        if (_primaryGpuTempSensor?.Value != null)
        {
            LastGpuTemp = _primaryGpuTempSensor.Value.Value;
            _gpuLastValidMs = now;
        }
    }

    private bool CpuFresh(long now) => _primaryCpuTempSensor != null && now - Math.Max(_cpuLastValidMs, _startMs) <= SensorStaleMs;

    private bool GpuFresh(long now) => _primaryGpuTempSensor != null && now - Math.Max(_gpuLastValidMs, _startMs) <= SensorStaleMs;

    /// <summary>Any fresh CPU/GPU reading above its limit puts every managed channel at 100 %.</summary>
    private void UpdateGlobalEmergency(long now)
    {
        float? cpu = CpuFresh(now) ? LastCpuTemp : null;
        float? gpu = GpuFresh(now) ? LastGpuTemp : null;

        bool hot = cpu >= CpuEmergencyC || gpu >= GpuEmergencyC;
        bool cool = (!cpu.HasValue || cpu <= CpuEmergencyReleaseC) && (!gpu.HasValue || gpu <= GpuEmergencyReleaseC);

        if (hot)
            _globalEmergency = true;
        else if (cool)
            _globalEmergency = false;
    }

    public float? ResolveChannelTemperature(string source) => ResolveChannelTemperature(source, Environment.TickCount64);

    /// <summary>
    /// Temperature for a channel's source, or null when it cannot be trusted (sensor missing or stale),
    /// which makes the channel run at full speed.
    /// </summary>
    private float? ResolveChannelTemperature(string source, long now)
    {
        bool cpuExists = _primaryCpuTempSensor != null;
        bool gpuExists = _primaryGpuTempSensor != null;
        bool cpuFresh = CpuFresh(now);
        bool gpuFresh = GpuFresh(now);

        if (string.Equals(source, FanChannelConfig.SourceCpu, StringComparison.OrdinalIgnoreCase))
            return cpuFresh ? LastCpuTemp : null;

        if (string.Equals(source, FanChannelConfig.SourceGpu, StringComparison.OrdinalIgnoreCase))
            return gpuFresh ? LastGpuTemp : null;

        if (source != null && _tempSensorsById.TryGetValue(source, out ISensor s) && s.Value != null)
            return s.Value.Value;

        // Max(CPU, GPU) and unknown sources: every sensor that exists must be fresh.
        if (!cpuExists && !gpuExists)
            return null;
        if ((cpuExists && !cpuFresh) || (gpuExists && !gpuFresh))
            return null;

        return Math.Max(cpuExists ? LastCpuTemp : float.MinValue, gpuExists ? LastGpuTemp : float.MinValue);
    }

    // ------------------------------------------------------------------ commands (UI thread)

    /// <summary>Runs a configuration change under the engine lock, so the background update never sees it half-done.</summary>
    public void Modify(Action change)
    {
        lock (_sync)
            change();
    }

    /// <summary>True when the Fan Control engine drives this control (the tree menu and HTTP API must not write to it).</summary>
    public bool IsManaged(IControl control)
    {
        lock (_sync)
            return _channels.Any(c => ReferenceEquals(c.Control, control) && (c.IsManaged || c.IsIdentifying));
    }

    public static bool IsControlManaged(IControl control) => _instance?.IsManaged(control) ?? false;

    public void Identify(FanChannelRuntime channel)
    {
        lock (_sync)
            channel.BeginIdentify(Environment.TickCount64);
    }

    public void CancelIdentify(FanChannelRuntime channel)
    {
        lock (_sync)
            channel.CancelIdentify();
    }

    public void ApplyPreset(GlobalFanPreset preset)
    {
        lock (_sync)
        {
            ActivePreset = preset;

            foreach (FanChannelRuntime channel in _channels)
            {
                // Presets never touch pumps, unidentified or unused channels (only "BIOS" resets everything).
                if (preset == GlobalFanPreset.DefaultBios)
                {
                    channel.Config.Mode = FanChannelMode.DefaultBios;
                    channel.ResetToDefault();
                    continue;
                }

                if (!FanRolePolicy.FollowsGlobalPresets(channel.Config.Role))
                    continue;

                switch (preset)
                {
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
                }
            }

            SaveSettings();
        }

        Raise(PresetChanged);
    }

    /// <summary>Number of channels a global preset would change (for the confirmation text).</summary>
    public int CountPresetTargets()
    {
        lock (_sync)
            return _channels.Count(c => FanRolePolicy.FollowsGlobalPresets(c.Config.Role));
    }

    public void SetCustomMode()
    {
        bool changed = false;
        lock (_sync)
        {
            if (ActivePreset != GlobalFanPreset.Custom)
            {
                ActivePreset = GlobalFanPreset.Custom;
                changed = true;
            }

            SaveSettings();
        }

        if (changed)
            Raise(PresetChanged);
    }

    public void SaveSettings()
    {
        lock (_sync)
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
                _settings.SetValue(prefix + "name", ch.Config.Name ?? string.Empty);
                _settings.SetValue(prefix + "role", (int)ch.Config.Role);
                _settings.SetValue(prefix + "fanCount", ch.Config.FanCount);
            }
        }
    }

    private FanChannelConfig LoadChannelConfig(string channelId, IControl control)
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

        config.Name = _settings.GetValue(prefix + "name", string.Empty);
        config.FanCount = Math.Max(1, _settings.GetValue(prefix + "fanCount", 1));

        // Channels saved before roles existed stay unidentified; the engine then leaves them to the BIOS.
        FanChannelRole role = (FanChannelRole)_settings.GetValue(prefix + "role", (int)FanChannelRole.Unassigned);
        config.Role = role == FanChannelRole.Unassigned ? SuggestRole(control) : role;

        return config;
    }

    private void LoadSettings()
    {
        ActivePreset = (GlobalFanPreset)_settings.GetValue("fancontrol.preset", (int)GlobalFanPreset.DefaultBios);
    }
}
