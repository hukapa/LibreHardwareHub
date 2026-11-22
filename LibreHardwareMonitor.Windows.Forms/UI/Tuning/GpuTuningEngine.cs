// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public sealed class GpuTuningEngine : IDisposable
{
    private static readonly object _lock = new();
    private static GpuTuningEngine _instance;

    private readonly IComputer _computer;
    private readonly PersistentSettings _settings;
    private readonly List<IGpuTuningDevice> _devices = new();

    private readonly Timer _watchdogTimer;
    private readonly Timer _stabilityTimer;
    private IGpuTuningDevice _activeTestDevice;
    private GpuTuningState _preTestState;

    public IReadOnlyList<IGpuTuningDevice> Devices => _devices;

    public bool HadDirtyBoot { get; private set; }

    public string DirtyBootMessage { get; private set; }

    public bool IsInTestMode { get; private set; }

    public int TestSecondsRemaining { get; private set; }

    public event EventHandler DevicesChanged;

    public event EventHandler<int> WatchdogTick;

    public event EventHandler<string> WatchdogReverted;

    public event EventHandler TestConfirmed;

    public GpuTuningEngine(IComputer computer, PersistentSettings settings)
    {
        _computer = computer ?? throw new ArgumentNullException(nameof(computer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        lock (_lock)
        {
            _instance = this;
        }

        _watchdogTimer = new Timer { Interval = 1000 };
        _watchdogTimer.Tick += WatchdogTimer_Tick;

        _stabilityTimer = new Timer { Interval = 120000 }; // 120 segundos para confirmar estabilidade
        _stabilityTimer.Tick += StabilityTimer_Tick;

        RegisterSafetyEvents();
        CheckDirtyBoot();
        DiscoverDevices();

        computer.HardwareAdded += _ => DiscoverDevices();
        computer.HardwareRemoved += _ => DiscoverDevices();
    }

    private void RegisterSafetyEvents()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreEmergencyStock();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreEmergencyStock();

        try
        {
            Microsoft.Win32.SystemEvents.SessionEnded += (_, _) => RestoreEmergencyStock();
        }
        catch
        {
        }
    }

    private static void RestoreEmergencyStock()
    {
        lock (_lock)
        {
            if (_instance != null && _instance.IsInTestMode)
            {
                _instance.CancelTestAndRevert("Encerramento inesperado durante o teste.");
            }
        }
    }

    private void CheckDirtyBoot()
    {
        bool dirty = _settings.GetValue("tuning.gpu.dirty_boot", false);
        if (dirty)
        {
            HadDirtyBoot = true;
            DirtyBootMessage = "Detectado encerramento inesperado anterior. Por seguranca, os perfis de overclock no arranque foram desativados.";
            _settings.SetValue("tuning.gpu.dirty_boot", false);
            _settings.SetValue("tuning.gpu.apply_on_startup", false);
        }
    }

    public void DiscoverDevices()
    {
        lock (_lock)
        {
            _devices.Clear();
            int nvidiaIndex = 0;
            int amdIndex = 0;

            foreach (IHardware hw in _computer.Hardware)
            {
                if (hw.HardwareType == HardwareType.GpuNvidia)
                {
                    _devices.Add(new NvidiaGpuTuningDevice(hw, nvidiaIndex++));
                }
                else if (hw.HardwareType == HardwareType.GpuAmd)
                {
                    _devices.Add(new AmdGpuTuningDevice(hw, amdIndex++));
                }
                else if (hw.HardwareType == HardwareType.GpuIntel)
                {
                    _devices.Add(new GenericGpuTuningDevice(hw, "GPU Intel integrada sem suporte a overclock/undervolt pelo driver."));
                }
            }

            LoadSettings();
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void LoadSettings()
    {
        bool applyOnStartup = _settings.GetValue("tuning.gpu.apply_on_startup", false);

        foreach (IGpuTuningDevice device in _devices)
        {
            if (!device.Capabilities.IsSupported)
                continue;

            string prefix = $"tuning.gpu.{device.DeviceId}.";
            int core = _settings.GetValue(prefix + "core_offset", 0);
            int mem = _settings.GetValue(prefix + "mem_offset", 0);
            int power = _settings.GetValue(prefix + "power_limit", device.Capabilities.DefaultPowerLimitPercent);
            int temp = _settings.GetValue(prefix + "temp_limit", device.Capabilities.DefaultTempLimitC);

            GpuTuningState state = new()
            {
                CoreClockOffsetMhz = core,
                MemoryClockOffsetMhz = mem,
                PowerLimitPercent = power,
                TempLimitC = temp,
                ApplyOnStartup = applyOnStartup
            };

            if (applyOnStartup && !HadDirtyBoot && !state.IsStock())
            {
                _settings.SetValue("tuning.gpu.dirty_boot", true);
                if (device.ApplyTuning(state, out _))
                {
                    _stabilityTimer.Start();
                }
            }
        }
    }

    public void SaveSettings(IGpuTuningDevice device)
    {
        if (device == null)
            return;

        string prefix = $"tuning.gpu.{device.DeviceId}.";
        _settings.SetValue(prefix + "core_offset", device.CurrentState.CoreClockOffsetMhz);
        _settings.SetValue(prefix + "mem_offset", device.CurrentState.MemoryClockOffsetMhz);
        _settings.SetValue(prefix + "power_limit", device.CurrentState.PowerLimitPercent);
        _settings.SetValue(prefix + "temp_limit", device.CurrentState.TempLimitC);
        _settings.SetValue("tuning.gpu.apply_on_startup", device.CurrentState.ApplyOnStartup);
    }

    private void StabilityTimer_Tick(object sender, EventArgs e)
    {
        _stabilityTimer.Stop();
        _settings.SetValue("tuning.gpu.dirty_boot", false);
    }

    public bool StartWatchdogTest(IGpuTuningDevice device, GpuTuningState newState, out string errorMessage)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));
        if (newState == null)
            throw new ArgumentNullException(nameof(newState));

        if (!device.Capabilities.IsSupported)
        {
            errorMessage = device.Capabilities.UnsupportedReason;
            return false;
        }

        _activeTestDevice = device;
        _preTestState = device.CurrentState.Clone();

        if (!device.ApplyTuning(newState, out errorMessage))
        {
            return false;
        }

        IsInTestMode = true;
        TestSecondsRemaining = 15;
        _watchdogTimer.Start();

        WatchdogTick?.Invoke(this, TestSecondsRemaining);
        return true;
    }

    private void WatchdogTimer_Tick(object sender, EventArgs e)
    {
        TestSecondsRemaining--;

        if (TestSecondsRemaining <= 0)
        {
            _watchdogTimer.Stop();
            CancelTestAndRevert("O temporizador de teste de 15 segundos expirou sem confirmacao. Perfil revertido.");
        }
        else
        {
            WatchdogTick?.Invoke(this, TestSecondsRemaining);
        }
    }

    public void ConfirmProfile()
    {
        if (!IsInTestMode || _activeTestDevice == null)
            return;

        _watchdogTimer.Stop();
        IsInTestMode = false;

        SaveSettings(_activeTestDevice);
        TestConfirmed?.Invoke(this, EventArgs.Empty);

        _activeTestDevice = null;
        _preTestState = null;
    }

    public void CancelTestAndRevert(string reason = null)
    {
        _watchdogTimer.Stop();
        IsInTestMode = false;

        if (_activeTestDevice != null && _preTestState != null)
        {
            _activeTestDevice.ApplyTuning(_preTestState, out _);
        }

        WatchdogReverted?.Invoke(this, reason ?? "Perfil revertido para os valores anteriores.");

        _activeTestDevice = null;
        _preTestState = null;
    }

    public bool ResetDeviceToStock(IGpuTuningDevice device, out string errorMessage)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));

        if (IsInTestMode && _activeTestDevice == device)
        {
            _watchdogTimer.Stop();
            IsInTestMode = false;
        }

        bool result = device.ResetToStock(out errorMessage);
        if (result)
        {
            SaveSettings(device);
        }
        return result;
    }

    public void Dispose()
    {
        // The ProcessExit / SessionEnded safety hooks stay attached on purpose: they must still restore the stock
        // state after the window was closed, and this engine lives as long as the process.
        _watchdogTimer.Stop();
        _watchdogTimer.Dispose();
        _stabilityTimer.Stop();
        _stabilityTimer.Dispose();
    }
}
