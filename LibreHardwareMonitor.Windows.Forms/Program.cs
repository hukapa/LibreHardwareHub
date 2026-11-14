// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI;

namespace LibreHardwareMonitor.Windows.Forms;

public static class Program
{
    private const string SingleInstanceMutexName = "Global\\LibreHardwareMonitor_SingleInstance_Mutex";

    private static void LogCrash(string category, Exception ex)
    {
        string logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {category}: {ex}\n";
        try
        {
            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), logText);
        }
        catch { }

        try
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibreHardwareHub");
            Directory.CreateDirectory(appData);
            File.AppendAllText(Path.Combine(appData, "crash.log"), logText);
        }
        catch { }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            UI.FanControl.FanControlEngine.RestoreAllControlsToDefault();
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash("Unhandled Exception", ex);
                try
                {
                    MessageBox.Show($"Ocorreu um erro ao iniciar o LibreHardwareMonitor:\n\n{ex.Message}\n\nConsulte crash.log para mais detalhes.", "Erro LibreHardwareMonitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        };

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            UI.FanControl.FanControlEngine.RestoreAllControlsToDefault();
            LogCrash("Thread Exception", e.Exception);
            try
            {
                MessageBox.Show($"Ocorreu um erro no LibreHardwareMonitor:\n\n{e.Exception.Message}\n\nConsulte crash.log para mais detalhes.", "Erro LibreHardwareMonitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        };

        // Processamento de argumentos de linha de comando
        if (args != null && args.Length > 0)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--exit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "/exit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-exit", StringComparison.OrdinalIgnoreCase))
                {
                    CloseRunningInstances();
                    return;
                }

                if (string.Equals(arg, "--elevate", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "/elevate", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-elevate", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsElevated())
                    {
                        RestartElevated();
                        return;
                    }
                }
            }
        }

        // Exigir privilegios administrativos para acesso a sensores de hardware de baixo nivel (MSR, PawnIO, Super I/O)
        if (!IsElevated())
        {
            RestartElevated();
            return;
        }

        // Blindagem contra execucao concorrente para prevenir colisoes de barramento LPC/SMBus
        using Mutex mutex = new(true, SingleInstanceMutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            BringExistingInstanceToFront();
            return;
        }

        if (!AllRequiredFilesAvailable())
            Environment.Exit(0);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (MainForm form = new MainForm())
        {
            form.FormClosed += delegate
            {
                Application.Exit();
            };
            Application.Run();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void BringExistingInstanceToFront()
    {
        try
        {
            int currentPid = Process.GetCurrentProcess().Id;
            foreach (Process proc in Process.GetProcessesByName("LibreHardwareMonitor.Windows.Forms"))
            {
                if (proc.Id != currentPid && proc.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(proc.MainWindowHandle, 9); // SW_RESTORE
                    SetForegroundWindow(proc.MainWindowHandle);
                    return;
                }
            }
        }
        catch
        {
        }
    }

    private static void CloseRunningInstances()
    {
        try
        {
            int currentPid = Process.GetCurrentProcess().Id;
            string[] processNames = ["LibreHardwareMonitor.Windows.Forms", "LibreHardwareMonitor"];
            foreach (string name in processNames)
            {
                foreach (Process proc in Process.GetProcessesByName(name))
                {
                    if (proc.Id != currentPid)
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                }
            }
        }
        catch
        {
        }
    }

    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void RestartElevated()
    {
        try
        {
            ProcessStartInfo psi = new(Application.ExecutablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };
            Process.Start(psi);
        }
        catch
        {
        }
    }

    private static bool IsAssemblyAvailable(string assemblyName)
    {
        try
        {
            Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (string.Equals(loaded[i].GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            Assembly asm = Assembly.Load(new AssemblyName(assemblyName));
            if (asm != null)
                return true;
        }
        catch
        {
        }

        string path = Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar;
        if (File.Exists(path + assemblyName + ".dll"))
            return true;

        MessageBox.Show("The following component could not be found: " + assemblyName +
                        "\nPlease extract all files from the archive.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }

    private static bool AllRequiredFilesAvailable()
    {
        if (!IsAssemblyAvailable("Aga.Controls"))
            return false;

        if (!IsAssemblyAvailable("LibreHardwareMonitorLib"))
            return false;

        if (!IsAssemblyAvailable("OxyPlot"))
            return false;

        if (!IsAssemblyAvailable("OxyPlot.WindowsForms"))
            return false;

        return true;
    }
}
