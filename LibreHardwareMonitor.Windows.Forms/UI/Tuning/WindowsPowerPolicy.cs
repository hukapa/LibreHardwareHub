// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Runtime.InteropServices;

namespace LibreHardwareMonitor.Windows.Forms.UI.Tuning;

public static class WindowsPowerPolicy
{
    private static readonly Guid SubGroupProcessor = new("54533251-82be-4824-96c1-47b60b740d00");
    private static readonly Guid SettingThrottleMax = new("bc5038f7-23e0-4960-96da-33abaf5935ec");
    private static readonly Guid SettingThrottleMin = new("893dee8e-2bef-41e0-89c6-b55d0929964c");
    private static readonly Guid SettingPerfBoostMode = new("be337238-0d82-4146-a960-4f3749d470c7");

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, out uint acValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, out uint dcValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, uint acValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid, ref Guid subGroupOfPowerSettingsGuid, ref Guid powerSettingGuid, uint dcValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static bool GetActiveSchemeGuid(out Guid activeScheme)
    {
        activeScheme = Guid.Empty;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr pGuid) == 0 && pGuid != IntPtr.Zero)
            {
                activeScheme = Marshal.PtrToStructure<Guid>(pGuid);
                LocalFree(pGuid);
                return true;
            }
        }
        catch
        {
        }
        return false;
    }

    public static bool GetMaxProcessorState(out int percent)
    {
        percent = 100;
        if (!GetActiveSchemeGuid(out Guid scheme))
            return false;

        Guid sub = SubGroupProcessor;
        Guid setting = SettingThrottleMax;

        if (PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint val) == 0)
        {
            percent = (int)val;
            return true;
        }

        return false;
    }

    public static bool SetMaxProcessorState(int percent)
    {
        int clamped = percent < 50 ? 50 : (percent > 100 ? 100 : percent);
        if (!GetActiveSchemeGuid(out Guid scheme))
            return false;

        Guid sub = SubGroupProcessor;
        Guid setting = SettingThrottleMax;

        uint resAc = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, (uint)clamped);
        uint resDc = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, (uint)clamped);
        PowerSetActiveScheme(IntPtr.Zero, ref scheme);

        return resAc == 0 && resDc == 0;
    }

    public static bool GetTurboBoostMode(out int mode)
    {
        mode = 1; // 1 = Padrao / Ativo
        if (!GetActiveSchemeGuid(out Guid scheme))
            return false;

        Guid sub = SubGroupProcessor;
        Guid setting = SettingPerfBoostMode;

        if (PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint val) == 0)
        {
            mode = (int)val;
            return true;
        }

        return false;
    }

    public static bool SetTurboBoostMode(int mode)
    {
        // 0 = Desativado, 1 = Ativo / Padrao, 2 = Agressivo, 3 = Eficiente
        int clamped = mode < 0 ? 0 : (mode > 3 ? 1 : mode);
        if (!GetActiveSchemeGuid(out Guid scheme))
            return false;

        Guid sub = SubGroupProcessor;
        Guid setting = SettingPerfBoostMode;

        uint resAc = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, (uint)clamped);
        uint resDc = PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, (uint)clamped);
        PowerSetActiveScheme(IntPtr.Zero, ref scheme);

        return resAc == 0 && resDc == 0;
    }
}
