// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using LibreHardwareMonitor.Windows.Forms.UI.Updates;

namespace LibreHardwareMonitor.Windows.Forms.UI.Settings;

/// <summary>
/// What the Definicoes page needs from the main window: the existing options (the same objects the menu items use,
/// so the menu and the page never disagree) and the actions that apply a theme or a unit.
/// </summary>
public sealed class SettingsContext
{
    public UserOption Startup { get; init; }

    public bool StartupAvailable { get; init; }

    public UserOption StartMinimized { get; init; }

    public UserOption MinimizeOnClose { get; init; }

    public Func<TemperatureUnit> GetUnit { get; init; }

    public Action<TemperatureUnit> SetUnit { get; init; }

    /// <summary>Applies a theme by its id ("dark", "black" or "light") and remembers the choice.</summary>
    public Action<string> SetTheme { get; init; }

    public UpdateService Updates { get; init; }
}
