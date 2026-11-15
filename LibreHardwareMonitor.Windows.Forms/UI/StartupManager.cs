// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Action = Microsoft.Win32.TaskScheduler.Action;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public class StartupManager
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string TaskName = "LibreHardwareHub";
    private static readonly string[] LegacyTaskNames = { "LibreHardware", "LibreHardwareMonitor", "LibreHardwareMonitor-CrystalStudio" };
    private bool _startup;

    public StartupManager()
    {
        if (Environment.OSVersion.Platform >= PlatformID.Unix)
        {
            IsAvailable = false;
            return;
        }

        if (IsAdministrator() && TaskService.Instance.Connected)
        {
            IsAvailable = true;

            Task task = GetTask();
            if (task != null)
            {
                foreach (Action action in task.Definition.Actions)
                {
                    if (action.ActionType == TaskActionType.Execute && action is ExecAction execAction)
                    {
                        if (execAction.Path.Equals(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                            _startup = true;
                    }
                }
            }
        }
        else
        {
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    string value = (string)registryKey?.GetValue(TaskName)
                                ?? (string)registryKey?.GetValue(nameof(LibreHardwareMonitor));

                    if (value != null)
                        _startup = value == Application.ExecutablePath;
                }

                IsAvailable = true;
            }
            catch (SecurityException)
            {
                IsAvailable = false;
            }
        }
    }

    public bool IsAvailable { get; }

    /// <summary>
    /// Used by the installer ("--register-startup" / "--unregister-startup"): creates the logon task for this
    /// executable, or removes every startup entry whatever executable it points to.
    /// </summary>
    public static void Register(bool enable)
    {
        try
        {
            if (enable)
            {
                StartupManager manager = new();
                if (manager.IsAvailable)
                    manager.Startup = true;
            }
            else
            {
                DeleteTask();
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                if (key != null)
                {
                    key.DeleteValue(TaskName, false);
                    key.DeleteValue(nameof(LibreHardwareMonitor), false);
                }
            }
        }
        catch (Exception)
        {
            // Startup registration is a convenience: never fail an install or uninstall because of it.
        }
    }

    public bool Startup
    {
        get { return _startup; }
        set
        {
            if (_startup != value)
            {
                if (IsAvailable)
                {
                    if (TaskService.Instance.Connected)
                    {
                        if (value)
                            CreateTask();
                        else
                            DeleteTask();

                        _startup = value;
                    }
                    else
                    {
                        try
                        {
                            if (value)
                                CreateRegistryKey();
                            else
                                DeleteRegistryKey();

                            _startup = value;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            throw new InvalidOperationException();
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }

    private static bool IsAdministrator()
    {
        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static Task GetTask()
    {
        try
        {
            Task current = TaskService.Instance.AllTasks.FirstOrDefault(x => x.Name.Equals(TaskName, StringComparison.OrdinalIgnoreCase));
            if (current != null)
                return current;

            foreach (string legacyName in LegacyTaskNames)
            {
                Task legacy = TaskService.Instance.AllTasks.FirstOrDefault(x => x.Name.Equals(legacyName, StringComparison.OrdinalIgnoreCase));
                if (legacy != null)
                    return legacy;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void CreateTask()
    {
        DeleteTask();

        TaskDefinition taskDefinition = TaskService.Instance.NewTask();
        taskDefinition.RegistrationInfo.Description = "Starts LibreHardwareHub on Windows startup.";

        taskDefinition.Triggers.Add(new LogonTrigger());

        taskDefinition.Settings.StartWhenAvailable = true;
        taskDefinition.Settings.DisallowStartIfOnBatteries = false;
        taskDefinition.Settings.StopIfGoingOnBatteries = false;
        taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        taskDefinition.Settings.AllowHardTerminate = false;

        taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
        taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;

        taskDefinition.Actions.Add(new ExecAction(Application.ExecutablePath, "", Path.GetDirectoryName(Application.ExecutablePath)));

        TaskService.Instance.RootFolder.RegisterTaskDefinition(TaskName, taskDefinition);
    }

    private static void DeleteTask()
    {
        try
        {
            Task task = GetTask();
            task?.Folder.DeleteTask(task.Name, false);

            foreach (string legacyName in LegacyTaskNames)
            {
                Task legacy = TaskService.Instance.AllTasks.FirstOrDefault(x => x.Name.Equals(legacyName, StringComparison.OrdinalIgnoreCase));
                legacy?.Folder.DeleteTask(legacy.Name, false);
            }
        }
        catch
        { }
    }

    private static void CreateRegistryKey()
    {
        RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(RegistryPath);
        registryKey?.SetValue(TaskName, Application.ExecutablePath);
    }

    private static void DeleteRegistryKey()
    {
        RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(RegistryPath);
        registryKey?.DeleteValue(TaskName, false);
        registryKey?.DeleteValue(nameof(LibreHardwareMonitor), false);
    }
}