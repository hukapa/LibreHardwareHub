// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.IO;

namespace LibreHardwareMonitor.Windows.Forms.Utilities;

/// <summary>
/// Identity of the application and the places it keeps its files. Installed copies live in a read-only
/// folder (Program Files), so settings and logs go to the user's AppData; a copy with a "portable.txt"
/// file next to the executable keeps everything in its own folder instead.
/// </summary>
public static class AppInfo
{
    /// <summary>Name shown to the user (window title, tray, installer).</summary>
    public const string Name = "LibreHardwareHub";

    /// <summary>Executable and process name, also used for file names.</summary>
    public const string Id = "LibreHardwareHub";

    /// <summary>Signalled to ask the running instance to close cleanly (fans are handed back to the BIOS).</summary>
    public const string ExitEventName = "Global\\LibreHardwareHub_Exit_Event";

    private const string PortableMarker = "portable.txt";

    /// <summary>Process names of this and of earlier builds, for single-instance handling.</summary>
    public static readonly string[] ProcessNames = { Id, "LibreHardware", "LibreHardwareMonitor.Windows.Forms", "LibreHardwareMonitor" };

    private static string _dataDirectory;

    public static string Version => typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static string ExeDirectory => AppContext.BaseDirectory;

    public static bool IsPortable => File.Exists(Path.Combine(ExeDirectory, PortableMarker));

    /// <summary>Folder for settings, logs and crash reports; created on first use.</summary>
    public static string DataDirectory
    {
        get
        {
            if (_dataDirectory != null)
                return _dataDirectory;

            string directory = ExeDirectory;
            if (!IsPortable)
            {
                directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    directory = ExeDirectory;
                }
            }

            return _dataDirectory = directory;
        }
    }

    public static string ConfigFile => Path.Combine(DataDirectory, Id + ".config");

    public static string CrashLogFile => Path.Combine(DataDirectory, "crash.log");

    /// <summary>
    /// First run after an update from a build that kept its settings next to the executable: copies that
    /// file to <see cref="ConfigFile" /> so fan curves, overlay and layout are not lost.
    /// </summary>
    public static void MigrateLegacyConfig()
    {
        if (File.Exists(ConfigFile))
            return;

        // 1. Check legacy directories in %AppData% (e.g. "%AppData%\Libre Hardware\LibreHardware.config")
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] legacyAppDataDirs = { "Libre Hardware", "LibreHardware", "LibreHardwareMonitor" };
            foreach (string legacyDir in legacyAppDataDirs)
            {
                foreach (string name in ProcessNames)
                {
                    string legacyPath = Path.Combine(appData, legacyDir, name + ".config");
                    if (File.Exists(legacyPath))
                    {
                        File.Copy(legacyPath, ConfigFile, true);
                        return;
                    }
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { }

        // 2. Check beside the executable (earlier builds or portable mode)
        foreach (string name in ProcessNames)
        {
            string legacy = Path.Combine(ExeDirectory, name + ".config");
            if (!File.Exists(legacy))
                continue;

            try
            {
                File.Copy(legacy, ConfigFile, true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { }

            return;
        }
    }
}
