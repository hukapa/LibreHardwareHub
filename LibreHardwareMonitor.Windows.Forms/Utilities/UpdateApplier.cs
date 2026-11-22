// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LibreHardwareMonitor.Windows.Forms.Utilities;

/// <summary>
/// Swaps the executable. A running exe cannot overwrite itself, so the new executable (downloaded to %TEMP%) is
/// started with "--apply-update PID TARGET": it waits for the old instance to close cleanly, copies itself over the
/// old executable (settings, portable.txt and logs are never touched), starts the updated copy with
/// "--cleanup-update" and exits.
/// </summary>
public static class UpdateApplier
{
    public const string ApplyArgument = "--apply-update";
    public const string CleanupArgument = "--cleanup-update";

    private static readonly string[] PackageFiles = { "PawnIO_setup.exe" };

    /// <summary>
    /// Starts the downloaded executable in apply mode. The caller must then close this instance cleanly. Only the executable
    /// that this session extracted from the update folder is started, and only if it is still byte for byte what was
    /// extracted: it is held open without write access while it is hashed and started.
    /// </summary>
    public static bool LaunchApply(string stagedExecutable)
    {
        if (!AppUpdater.IsUnderUpdateRoot(stagedExecutable) || !AppUpdater.TryGetStagedHash(stagedExecutable, out byte[] hash))
            return false;

        try
        {
            using FileStream guard = VerifiedFile.OpenVerified(stagedExecutable, hash);
            ProcessStartInfo start = new(stagedExecutable)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(stagedExecutable) ?? AppInfo.ExeDirectory
            };
            start.ArgumentList.Add(ApplyArgument);
            start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.ArgumentList.Add(AppInfo.ExeDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return Process.Start(start) != null;
        }
        catch (InvalidDataException)
        {
            // The extracted file is not what was extracted: it is never started, and the next attempt fetches it again.
            AppUpdater.ForgetStaged(stagedExecutable);
            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Handles "--apply-update PID TARGET" when present; true means this process only had to apply an update.</summary>
    public static bool TryRunApply(string[] args)
    {
        int index = Array.FindIndex(args, a => string.Equals(a, ApplyArgument, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        if (index + 2 < args.Length && int.TryParse(args[index + 1], out int pid) && IsValidApplyRequest(pid, args[index + 2], out string target))
            Apply(pid, target);

        return true;
    }

    // Apply mode is for the new executable only, running from the update folder: a copy of the program started from anywhere
    // else (or by anyone who just types the arguments) never writes into another folder. The destination must exist and hold
    // the installed executable.
    private static bool IsValidApplyRequest(int pid, string targetDirectory, out string fullTarget)
    {
        fullTarget = null;
        string source = Environment.ProcessPath;
        if (pid <= 0 || pid == Environment.ProcessId || source == null || !AppUpdater.IsUnderUpdateRoot(source))
            return false;

        string full = Path.GetFullPath(targetDirectory);
        if (!Directory.Exists(full) || AppUpdater.IsUnderUpdateRoot(Path.Combine(full, AppInfo.Id + ".exe")) || !File.Exists(Path.Combine(full, AppInfo.Id + ".exe")))
            return false;

        fullTarget = full;
        return true;
    }

    /// <summary>
    /// Folder named by "--cleanup-update DIR", only when it is strictly one level inside the temp folder, named for the updater
    /// and a real folder (a link planted there would make the delete reach somewhere else): null otherwise.
    /// </summary>
    public static string GetCleanupDirectory(string[] args)
    {
        int index = Array.FindIndex(args, a => string.Equals(a, CleanupArgument, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
            return null;

        string full = Path.GetFullPath(args[index + 1]).TrimEnd(Path.DirectorySeparatorChar);
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(full).StartsWith("LibreHardwareHub_Update", StringComparison.OrdinalIgnoreCase))
            return null;

        return Directory.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0 ? null : full;
    }

    /// <summary>Deletes the update folder once the apply process, which ran from it, has exited.</summary>
    public static void ScheduleCleanup(string directory)
    {
        if (directory == null)
            return;

        Task.Run(() =>
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                Thread.Sleep(2000);
                try
                {
                    if (!Directory.Exists(directory))
                        return;

                    AppUpdater.DeleteDirectory(directory);
                    return;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Still in use: try again shortly.
                }
            }
        });
    }

    private static void Apply(int pid, string targetDirectory)
    {
        string source = Environment.ProcessPath;
        string destination = Path.Combine(targetDirectory, AppInfo.Id + ".exe");
        if (source == null || !Directory.Exists(targetDirectory))
            return;

        if (!WaitForOldInstance(pid))
        {
            Fail("A instância anterior não fechou a tempo. A atualização será tentada novamente na próxima vez.", destination);
            return;
        }

        try
        {
            ReplaceFile(source, destination);
            string sourceDirectory = Path.GetDirectoryName(source);
            foreach (string name in PackageFiles)
            {
                string packaged = Path.Combine(sourceDirectory, name);
                if (File.Exists(packaged))
                    ReplaceFile(packaged, Path.Combine(targetDirectory, name));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Fail($"Não foi possível copiar a nova versão: {e.Message}", destination);
            return;
        }

        try
        {
            ProcessStartInfo start = new(destination) { UseShellExecute = false, WorkingDirectory = targetDirectory };
            start.ArgumentList.Add(CleanupArgument);
            start.ArgumentList.Add(AppUpdater.UpdateRoot);
            Process.Start(start);
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
        {
            Fail($"A nova versão foi instalada, mas não arrancou: {e.Message}", null);
        }
    }

    // Waits for the old instance to leave (it closes by itself, which hands the fans back to the BIOS); asks again
    // through the exit event if it is slow. Never kills it.
    private static bool WaitForOldInstance(int pid)
    {
        try
        {
            using Process old = Process.GetProcessById(pid);
            if (old.WaitForExit(15000))
                return true;

            if (EventWaitHandle.TryOpenExisting(AppInfo.ExitEventName, out EventWaitHandle exitEvent))
            {
                using (exitEvent)
                    exitEvent.Set();
            }

            return old.WaitForExit(10000);
        }
        catch (ArgumentException)
        {
            // No such process: it already closed.
            return true;
        }
    }

    // Copies next to the destination first and then moves it over, so an interrupted copy never leaves a half-written
    // executable behind. A leftover ".new" is deleted first (a link is removed, not followed) and the copy never overwrites,
    // so nothing planted there can redirect the write. Antivirus scanners may hold the file for a moment: a few retries.
    private static void ReplaceFile(string source, string destination)
    {
        string temporary = destination + ".new";
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Delete(temporary);
                File.Copy(source, temporary, false);
                File.Move(temporary, destination, true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 9)
            {
                Thread.Sleep(500);
            }
        }
    }

    // The apply process has no window of its own: report the problem and, when possible, bring the old version back.
    private static void Fail(string message, string restart)
    {
        try
        {
            MessageBox.Show(message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (InvalidOperationException)
        {
        }

        if (restart != null && File.Exists(restart))
        {
            try
            {
                Process.Start(new ProcessStartInfo(restart) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(restart) });
            }
            catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }
}
