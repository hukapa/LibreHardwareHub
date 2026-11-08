// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

public enum FanChannelMode
{
    DefaultBios,
    Fixed,
    Curve
}

public enum GlobalFanPreset
{
    DefaultBios,
    Silent,
    Balanced,
    Performance,
    Custom
}

/// <summary>What is connected to a channel. Drives the safety rules applied to it.</summary>
public enum FanChannelRole
{
    /// <summary>Not identified yet by the user.</summary>
    Unassigned,
    CpuCooler,
    Pump,
    Radiator,
    CaseFan,
    Gpu,
    /// <summary>Nothing connected / not wanted: the app never writes to it.</summary>
    NotConnected
}

/// <summary>Safety rules per role. Generic: they do not assume any particular PC.</summary>
public static class FanRolePolicy
{
    /// <summary>Lowest duty any managed channel may receive; below this many fans stall.</summary>
    public const float GlobalMinimumPwm = 20f;

    /// <summary>Pumps are kept on the BIOS by default; a manual value can never go below this.</summary>
    public const float PumpMinimumPwm = 70f;

    public static float MinimumPwm(FanChannelRole role) => role == FanChannelRole.Pump ? PumpMinimumPwm : GlobalMinimumPwm;

    /// <summary>
    /// A manual fixed speed is only allowed once the user has said what is connected: an unidentified
    /// channel could be a pump, and "nothing connected" channels are never written.
    /// </summary>
    public static bool AllowsFixed(FanChannelRole role) =>
        role is FanChannelRole.CpuCooler or FanChannelRole.Pump or FanChannelRole.Radiator or FanChannelRole.CaseFan or FanChannelRole.Gpu;

    /// <summary>Temperature curves are never offered for pumps (they run at a constant speed).</summary>
    public static bool AllowsCurve(FanChannelRole role) =>
        role is FanChannelRole.CpuCooler or FanChannelRole.Radiator or FanChannelRole.CaseFan or FanChannelRole.Gpu;

    /// <summary>Global presets (page, tray) only touch roles that allow a curve.</summary>
    public static bool FollowsGlobalPresets(FanChannelRole role) => AllowsCurve(role);

    public static bool AllowsMode(FanChannelRole role, FanChannelMode mode) => mode switch
    {
        FanChannelMode.Fixed => AllowsFixed(role),
        FanChannelMode.Curve => AllowsCurve(role),
        _ => true
    };

    /// <summary>Roles whose stop is critical even while the BIOS controls them.</summary>
    public static bool IsCritical(FanChannelRole role) => role is FanChannelRole.Pump or FanChannelRole.CpuCooler;

    public static string DisplayName(FanChannelRole role) => role switch
    {
        FanChannelRole.CpuCooler => "Cooler do CPU",
        FanChannelRole.Pump => "Bomba (watercooler)",
        FanChannelRole.Radiator => "Radiador",
        FanChannelRole.CaseFan => "Ventoinha da caixa",
        FanChannelRole.Gpu => "Placa gráfica",
        FanChannelRole.NotConnected => "Nada ligado / não usar",
        _ => "Por identificar"
    };
}
