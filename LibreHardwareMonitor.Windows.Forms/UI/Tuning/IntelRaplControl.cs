// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using LibreHardwareMonitor.PawnIo;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public sealed class IntelRaplControl : IDisposable
{
    private const uint MSR_RAPL_POWER_UNIT = 0x606;
    private const uint MSR_PKG_POWER_LIMIT = 0x610;

    private readonly IntelMsr _msr;
    private double _powerUnit = 0.125; // Predefinicao tipica: 1/8 Watt
    private bool _initialized;

    public bool IsAvailable => _msr != null && _msr.IsLoaded;

    public bool IsLocked { get; private set; } = true;

    public double PowerUnit => _powerUnit;

    public IntelRaplControl()
    {
        try
        {
            _msr = new IntelMsr();
            if (_msr.IsLoaded)
            {
                Initialize();
            }
        }
        catch
        {
            _msr = null;
        }
    }

    private void Initialize()
    {
        if (_msr.ReadMsr(MSR_RAPL_POWER_UNIT, out uint eax, out _))
        {
            uint powerUnitsRaw = eax & 0xF;
            _powerUnit = 1.0 / (1 << (int)powerUnitsRaw);
            _initialized = true;
        }

        if (_msr.ReadMsr(MSR_PKG_POWER_LIMIT, out ulong limitVal))
        {
            // Bit 63 e o Package Lock Bit
            IsLocked = (limitVal & (1UL << 63)) != 0;
        }
    }

    public bool ReadPowerLimits(out int pl1Watts, out int pl2Watts, out bool isLocked)
    {
        pl1Watts = 65;
        pl2Watts = 125;
        isLocked = true;

        if (!IsAvailable || !_initialized)
            return false;

        if (!_msr.ReadMsr(MSR_PKG_POWER_LIMIT, out ulong limitVal))
            return false;

        IsLocked = (limitVal & (1UL << 63)) != 0;
        isLocked = IsLocked;

        ulong pl1Raw = limitVal & 0x7FFF;
        ulong pl2Raw = (limitVal >> 32) & 0x7FFF;

        pl1Watts = (int)Math.Round(pl1Raw * _powerUnit);
        pl2Watts = (int)Math.Round(pl2Raw * _powerUnit);

        return true;
    }

    public bool SetPowerLimits(int pl1Watts, int pl2Watts, out string errorMessage)
    {
        if (!IsAvailable)
        {
            errorMessage = "Driver PawnIO Intel MSR nao carregado.";
            return false;
        }

        if (!_msr.ReadMsr(MSR_PKG_POWER_LIMIT, out ulong currentVal))
        {
            errorMessage = "Falha ao ler registo MSR 0x610 da CPU.";
            return false;
        }

        // Se o bit 63 estiver ativo, a BIOS do fabricante bloqueou a escrita de RAPL
        if ((currentVal & (1UL << 63)) != 0)
        {
            IsLocked = true;
            errorMessage = "O limite RAPL esta bloqueado pela BIOS do fabricante (Lock Bit ativo).";
            return false;
        }

        // Clamping rigoroso de seguranca (15W a 253W)
        int clampedPl1 = pl1Watts < 15 ? 15 : (pl1Watts > 253 ? 253 : pl1Watts);
        int clampedPl2 = pl2Watts < clampedPl1 ? clampedPl1 : (pl2Watts > 253 ? 253 : pl2Watts);

        ulong pl1Raw = (ulong)Math.Round(clampedPl1 / _powerUnit) & 0x7FFF;
        ulong pl2Raw = (ulong)Math.Round(clampedPl2 / _powerUnit) & 0x7FFF;

        // Preservar bits de janela temporal, clamping e enable do registo
        ulong newLow = (currentVal & 0xFFFFFFFF00000000UL) | (currentVal & 0xFFFF8000UL) | pl1Raw | (1UL << 15);
        ulong newHigh = ((currentVal >> 32) & 0xFFFF8000UL) | pl2Raw | (1UL << 15);
        ulong finalVal = (newHigh << 32) | (newLow & 0xFFFFFFFFUL);

        if (_msr.WriteMsr(MSR_PKG_POWER_LIMIT, finalVal))
        {
            errorMessage = null;
            return true;
        }

        errorMessage = "Falha ao executar ioctl_write_msr em MSR 0x610.";
        return false;
    }

    public void Dispose()
    {
        _msr?.Close();
    }
}
