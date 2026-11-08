// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public sealed class CpuTuningEngine : IDisposable
{
    private static readonly object _lock = new();
    private static CpuTuningEngine _instance;

    private readonly IComputer _computer;
    private readonly PersistentSettings _settings;
    private IntelRaplControl _rapl;
    private IHardware _cpuHardware;
    private bool _settingsLoaded;

    // Cache de ultimos valores validos para evitar cintilacoes e garantir leituras estaveis
    private float? _lastTemp;
    private float? _lastPower;
    private float? _lastClock;
    private float? _lastLoad;
    private float? _lastVoltage;

    private readonly Timer _watchdogTimer;
    private readonly Timer _stabilityTimer;
    private CpuTuningState _preTestState;

    public CpuTuningCapabilities Capabilities { get; } = new();

    public CpuTuningState CurrentState { get; private set; }

    public bool HadDirtyBoot { get; private set; }

    public string DirtyBootMessage { get; private set; }

    public bool IsInTestMode { get; private set; }

    public int TestSecondsRemaining { get; private set; }

    public event EventHandler<int> WatchdogTick;

    public event EventHandler<string> WatchdogReverted;

    public event EventHandler TestConfirmed;

    public event EventHandler StateChanged;

    public CpuTuningEngine(IComputer computer, PersistentSettings settings)
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

        // The engine is created before Computer.Open(): the CPU (and the MSR driver) usually only appear
        // through HardwareAdded, so discovery and the startup profile are (re)done from there.
        _computer.HardwareAdded += _ => OnHardwareChanged();
        _computer.HardwareRemoved += _ => OnHardwareChanged();
        OnHardwareChanged();
    }

    private void OnHardwareChanged()
    {
        if (!DiscoverCpu())
            return;

        if (!_settingsLoaded && _cpuHardware != null)
        {
            _settingsLoaded = true;
            LoadSettings();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>Resolves the CPU and its capabilities. Returns false when nothing changed (same CPU as before).</summary>
    private bool DiscoverCpu()
    {
        IHardware cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu == _cpuHardware && _rapl != null)
            return false; // other hardware changed: keep the current state untouched

        _cpuHardware = cpu;

        if (_cpuHardware != null)
        {
            string name = _cpuHardware.Name ?? string.Empty;
            Capabilities.IsIntel = name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0;
            Capabilities.IsAmd = name.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // The MSR driver is only usable once the hardware layer is open: (re)try until it is available.
        if (_rapl == null || (!_rapl.IsAvailable && Capabilities.IsIntel))
        {
            _rapl?.Dispose();
            _rapl = new IntelRaplControl();
        }

        int defPl1 = 65;
        int defPl2 = 125;
        bool locked = true;

        if (Capabilities.IsIntel && _rapl.IsAvailable)
        {
            if (_rapl.ReadPowerLimits(out int readPl1, out int readPl2, out bool isLocked))
            {
                defPl1 = readPl1;
                defPl2 = readPl2;
                locked = isLocked;
            }
        }

        Capabilities.DefaultPl1Watts = defPl1;
        Capabilities.DefaultPl2Watts = defPl2;
        Capabilities.IsRaplLocked = locked;
        Capabilities.CanAdjustRapl = !locked && _rapl.IsAvailable;

        if (Capabilities.CanAdjustRapl)
        {
            Capabilities.RaplStatusMessage = "RAPL desbloqueado · PL1 e PL2 ajustáveis";
        }
        else if (Capabilities.IsIntel)
        {
            Capabilities.RaplStatusMessage = "RAPL bloqueado pela BIOS · só o plano de energia do Windows é ajustável";
        }
        else if (Capabilities.IsAmd)
        {
            Capabilities.RaplStatusMessage = "AMD Ryzen · só o plano de energia do Windows é ajustável";
        }
        else
        {
            Capabilities.RaplStatusMessage = "Só o plano de energia do Windows é ajustável";
        }

        CurrentState = CpuTuningState.CreateDefault(defPl1, defPl2);

        // Ler estado atual do Windows
        if (WindowsPowerPolicy.GetMaxProcessorState(out int currentMax))
            CurrentState.MaxProcessorStatePercent = currentMax;

        if (WindowsPowerPolicy.GetTurboBoostMode(out int currentBoost))
            CurrentState.TurboBoostMode = currentBoost;

        return true;
    }

    private void CheckDirtyBoot()
    {
        bool dirty = _settings.GetValue("tuning.cpu.dirty_boot", false);
        if (dirty)
        {
            HadDirtyBoot = true;
            DirtyBootMessage = "Detectado encerramento inesperado anterior. Por seguranca, os ajustes de CPU no arranque foram desativados.";
            _settings.SetValue("tuning.cpu.dirty_boot", false);
            _settings.SetValue("tuning.cpu.apply_on_startup", false);
        }
    }

    private void LoadSettings()
    {
        bool applyOnStartup = _settings.GetValue("tuning.cpu.apply_on_startup", false);
        int maxProc = _settings.GetValue("tuning.cpu.max_processor_percent", CurrentState.MaxProcessorStatePercent);
        int boost = _settings.GetValue("tuning.cpu.turbo_boost_mode", CurrentState.TurboBoostMode);
        int pl1 = _settings.GetValue("tuning.cpu.pl1_watts", CurrentState.Pl1Watts);
        int pl2 = _settings.GetValue("tuning.cpu.pl2_watts", CurrentState.Pl2Watts);

        CpuTuningState loaded = new()
        {
            MaxProcessorStatePercent = maxProc,
            TurboBoostMode = boost,
            Pl1Watts = pl1,
            Pl2Watts = pl2,
            ApplyOnStartup = applyOnStartup
        };

        if (applyOnStartup && !HadDirtyBoot && !loaded.IsStock(Capabilities.DefaultPl1Watts, Capabilities.DefaultPl2Watts))
        {
            _settings.SetValue("tuning.cpu.dirty_boot", true);
            if (ApplyTuningInternal(loaded, out _))
            {
                _stabilityTimer.Start();
            }
        }
    }

    public void SaveSettings()
    {
        _settings.SetValue("tuning.cpu.max_processor_percent", CurrentState.MaxProcessorStatePercent);
        _settings.SetValue("tuning.cpu.turbo_boost_mode", CurrentState.TurboBoostMode);
        _settings.SetValue("tuning.cpu.pl1_watts", CurrentState.Pl1Watts);
        _settings.SetValue("tuning.cpu.pl2_watts", CurrentState.Pl2Watts);
        _settings.SetValue("tuning.cpu.apply_on_startup", CurrentState.ApplyOnStartup);
    }

    private void StabilityTimer_Tick(object sender, EventArgs e)
    {
        _stabilityTimer.Stop();
        _settings.SetValue("tuning.cpu.dirty_boot", false);
    }

    public bool ApplyTuningInternal(CpuTuningState state, out string errorMessage)
    {
        errorMessage = null;

        // 1. Camada Universal: Windows Power Policy
        WindowsPowerPolicy.SetMaxProcessorState(state.MaxProcessorStatePercent);
        WindowsPowerPolicy.SetTurboBoostMode(state.TurboBoostMode);

        // 2. Camada Hardware Direct: Intel RAPL (se desbloqueado)
        if (Capabilities.CanAdjustRapl)
        {
            if (!_rapl.SetPowerLimits(state.Pl1Watts, state.Pl2Watts, out string raplErr))
            {
                errorMessage = raplErr;
            }
        }

        CurrentState = state.Clone();
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool StartWatchdogTest(CpuTuningState newState, out string errorMessage)
    {
        if (newState == null)
            throw new ArgumentNullException(nameof(newState));

        _preTestState = CurrentState.Clone();

        if (!ApplyTuningInternal(newState, out errorMessage))
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
            CancelTestAndRevert("Temporizador de 15 segundos expirou sem confirmacao. Perfil revertido.");
        }
        else
        {
            WatchdogTick?.Invoke(this, TestSecondsRemaining);
        }
    }

    public void ConfirmProfile()
    {
        if (!IsInTestMode)
            return;

        _watchdogTimer.Stop();
        IsInTestMode = false;

        SaveSettings();
        TestConfirmed?.Invoke(this, EventArgs.Empty);
        _preTestState = null;
    }

    public void CancelTestAndRevert(string reason = null)
    {
        _watchdogTimer.Stop();
        IsInTestMode = false;

        if (_preTestState != null)
        {
            ApplyTuningInternal(_preTestState, out _);
        }

        WatchdogReverted?.Invoke(this, reason ?? "Perfil de CPU revertido para os valores anteriores.");
        _preTestState = null;
    }

    public bool ResetToStock(out string errorMessage)
    {
        if (IsInTestMode)
        {
            _watchdogTimer.Stop();
            IsInTestMode = false;
        }

        CpuTuningState stock = CpuTuningState.CreateDefault(Capabilities.DefaultPl1Watts, Capabilities.DefaultPl2Watts);
        bool result = ApplyTuningInternal(stock, out errorMessage);
        if (result)
        {
            SaveSettings();
        }
        return result;
    }

    public CpuTuningTelemetry ReadTelemetry()
    {
        CpuTuningTelemetry telem = new();

        _cpuHardware ??= _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (_cpuHardware == null)
            return telem;

        // Leitura recursiva direta dos sensores sem invocar Update() na thread de interface.
        // O ciclo do BackgroundUpdater encarrega-se de atualizar o hardware com cadencia estavel.
        ReadSensorsRecursive(_cpuHardware, telem);

        // Fallback para voltagem VCore da Motherboard / Super I/O caso a CPU nao exponha sensor de tensao direto
        if (telem.VoltageV == null && _computer != null)
        {
            foreach (IHardware hw in _computer.Hardware)
            {
                if (hw.HardwareType == HardwareType.Motherboard || hw.HardwareType == HardwareType.SuperIO)
                {
                    ReadMotherboardVoltage(hw, telem);
                    if (telem.VoltageV != null)
                        break;
                }
            }
        }

        // Manter ultimos valores validos conhecidos para estabilidade total do utilizador
        if (telem.PackageTemperatureC.HasValue && telem.PackageTemperatureC.Value > 0f)
            _lastTemp = telem.PackageTemperatureC;
        else if (_lastTemp.HasValue)
            telem.PackageTemperatureC = _lastTemp;

        if (telem.PackagePowerWatts.HasValue && telem.PackagePowerWatts.Value > 0f)
            _lastPower = telem.PackagePowerWatts;
        else if (_lastPower.HasValue)
            telem.PackagePowerWatts = _lastPower;

        if (telem.CoreClockMhz.HasValue && telem.CoreClockMhz.Value > 0f)
            _lastClock = telem.CoreClockMhz;
        else if (_lastClock.HasValue)
            telem.CoreClockMhz = _lastClock;

        if (telem.TotalLoadPercent.HasValue)
            _lastLoad = telem.TotalLoadPercent;
        else if (_lastLoad.HasValue)
            telem.TotalLoadPercent = _lastLoad;

        if (telem.VoltageV.HasValue && telem.VoltageV.Value > 0f)
            _lastVoltage = telem.VoltageV;
        else if (_lastVoltage.HasValue)
            telem.VoltageV = _lastVoltage;

        return telem;
    }

    private static void ReadSensorsRecursive(IHardware hw, CpuTuningTelemetry telem)
    {
        foreach (ISensor sensor in hw.Sensors)
        {
            if (sensor.Value == null)
                continue;

            float val = sensor.Value.Value;

            switch (sensor.SensorType)
            {
                case SensorType.Temperature:
                    if (sensor.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0)
                        telem.PackageTemperatureC = val;
                    else if (sensor.Name.IndexOf("Core Max", StringComparison.OrdinalIgnoreCase) >= 0 && telem.PackageTemperatureC == null)
                        telem.PackageTemperatureC = val;
                    else if (sensor.Name.IndexOf("Tctl", StringComparison.OrdinalIgnoreCase) >= 0 && telem.PackageTemperatureC == null)
                        telem.PackageTemperatureC = val;
                    else if (telem.PackageTemperatureC == null && val > 0f)
                        telem.PackageTemperatureC = val;
                    break;

                case SensorType.Power:
                    if (sensor.Name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0)
                        telem.PackagePowerWatts = val;
                    else if (telem.PackagePowerWatts == null && val > 0f)
                        telem.PackagePowerWatts = val;
                    break;

                case SensorType.Clock:
                    if (sensor.Name.IndexOf("Bus", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        if (val > (telem.CoreClockMhz ?? 0f))
                            telem.CoreClockMhz = val;
                    }
                    break;

                case SensorType.Load:
                    if (sensor.Name.IndexOf("Total", StringComparison.OrdinalIgnoreCase) >= 0)
                        telem.TotalLoadPercent = val;
                    else if (telem.TotalLoadPercent == null)
                        telem.TotalLoadPercent = val;
                    break;

                case SensorType.Voltage:
                    if (sensor.Name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                        telem.VoltageV = val;
                    else if (telem.VoltageV == null && val > 0f)
                        telem.VoltageV = val;
                    break;
            }
        }

        foreach (IHardware sub in hw.SubHardware)
            ReadSensorsRecursive(sub, telem);
    }

    private static void ReadMotherboardVoltage(IHardware hw, CpuTuningTelemetry telem)
    {
        foreach (ISensor sensor in hw.Sensors)
        {
            if (sensor.SensorType == SensorType.Voltage && sensor.Value.HasValue)
            {
                if (sensor.Name.IndexOf("VCore", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sensor.Name.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    telem.VoltageV = sensor.Value.Value;
                    return;
                }
            }
        }

        foreach (IHardware sub in hw.SubHardware)
        {
            ReadMotherboardVoltage(sub, telem);
            if (telem.VoltageV != null)
                return;
        }
    }

    public void Dispose()
    {
        _watchdogTimer.Stop();
        _watchdogTimer.Dispose();
        _stabilityTimer.Stop();
        _stabilityTimer.Dispose();
        _rapl?.Dispose();
    }
}
