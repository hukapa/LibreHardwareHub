// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public class UpdateVisitor : IVisitor
{
    private readonly Dictionary<IHardware, long> _lastUpdateTicks = new();
    private long _currentTickMs;

    public bool LowPowerMode { get; set; }

    public bool ActiveFanControl { get; set; }

    public void VisitComputer(IComputer computer)
    {
        _currentTickMs = Environment.TickCount64;
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        if (ShouldUpdate(hardware, _currentTickMs))
        {
            hardware.Update();
            _lastUpdateTicks[hardware] = _currentTickMs;
        }

        foreach (IHardware subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }

    private bool ShouldUpdate(IHardware hardware, long now)
    {
        if (!_lastUpdateTicks.TryGetValue(hardware, out long lastUpdate))
            return true;

        long elapsedMs = now - lastUpdate;

        if (LowPowerMode)
        {
            return hardware.HardwareType switch
            {
                HardwareType.Storage or HardwareType.Battery => elapsedMs >= 60000,
                HardwareType.Memory or HardwareType.Network or HardwareType.Psu => elapsedMs >= 5000,
                _ => elapsedMs >= 2500
            };
        }

        return hardware.HardwareType switch
        {
            // Tier Lento: Atributos SMART de discos e bateria (15 segundos)
            HardwareType.Storage or HardwareType.Battery => elapsedMs >= 15000,

            // Tier Normal: Memoria, Rede e Fontes de Alimentacao (800 ms)
            HardwareType.Memory or HardwareType.Network or HardwareType.Psu => elapsedMs >= 800,

            // Tier Critico: CPU, GPU, Motherboard, Super I/O e Controladores de Ventoinha
            HardwareType.Cpu or HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel
                or HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.Cooler
                or HardwareType.EmbeddedController or HardwareType.PowerMonitor =>
                elapsedMs >= (ActiveFanControl ? 750 : 350),

            _ => elapsedMs >= 500
        };
    }

    public void Reset()
    {
        _lastUpdateTicks.Clear();
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}
