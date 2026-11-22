// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay.Fps;

/// <summary>
/// Measures the frame rate of the game in the foreground from the "present" events Windows itself publishes through
/// ETW (DXGI and the DirectX kernel): no DLL is injected and no DirectX/OpenGL/Vulkan call is hooked, so it is invisible
/// to anti-cheats. The ETW session and its reader thread exist only between <see cref="Start" /> and <see cref="Stop" />:
/// the overlay starts it while the HUD is visible and a frame-rate reading is enabled, and stops it right after.
/// </summary>
public sealed class FpsMonitorService : IDisposable
{
    private static readonly Guid DxgiProvider = new("ca11c036-0102-4a2d-a6ad-f03cfed5d3c9");
    private static readonly Guid DxgKrnlProvider = new("802ec45a-1e99-4b83-9920-87c98277ba9d");
    private static readonly (long First, long Second) DxgiHalves = EtwNative.Halves(DxgiProvider);
    private static readonly (long First, long Second) DxgKrnlHalves = EtwNative.Halves(DxgKrnlProvider);

    // Microsoft-Windows-DXGI Present_Start, and the DxgKrnl present event used for OpenGL, Vulkan and DirectX 9.
    private const ushort DxgiPresentStart = 42;
    private const ushort DxgKrnlPresent = 184;

    // A DirectX 11/12 present is also seen by the kernel provider; two queues of the same present can fire within this.
    private const double KernelMinDeltaMs = 0.25;

    private const long ForegroundCheckMs = 250;
    private const int StopJoinMs = 2000;

    private static readonly string[] IgnoredProcesses =
    {
        "dwm", "explorer", "LibreHardwareHub", "ShellExperienceHost", "SearchHost", "StartMenuExperienceHost", "LockApp", "TextInputHost"
    };

    private readonly object _lifecycle = new();
    private readonly object _data = new();
    private readonly FrameTimeBuffer _buffer = new();
    private readonly EtwNative.EventRecordCallback _callback;
    private readonly EventHandler _exitHandler;

    private Thread _thread;
    private ulong _session;
    private ulong _trace = EtwNative.InvalidTraceHandle;
    private IntPtr _properties;
    private IntPtr _logFile;
    private IntPtr _logFileName;
    private bool _running;
    private bool _disposed;

    private int _foregroundPid;
    private bool _foregroundIgnored = true;
    private int _dxgiPid;
    private long _nextForegroundCheck;

    public FpsMonitorService()
    {
        _callback = OnEvent;
        _exitHandler = (_, _) => Stop();
        AppDomain.CurrentDomain.ProcessExit += _exitHandler;
    }

    /// <summary>True while the ETW session is running.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_lifecycle)
                return _running;
        }
    }

    /// <summary>Win32 error of the last failed <see cref="Start" /> (5 = access denied, 1450 = no resources); 0 when it worked.</summary>
    public uint LastError { get; private set; }

    /// <summary>Starts or stops the session according to <paramref name="active" />; safe to call repeatedly.</summary>
    public void SetActive(bool active)
    {
        if (active)
            Start();
        else
            Stop();
    }

    /// <summary>Starts the session. Returns false, silently, when Windows refuses (no privileges, too many sessions).</summary>
    public bool Start()
    {
        lock (_lifecycle)
        {
            if (_running)
                return true;

            if (_disposed)
                return false;

            try
            {
                return StartCore();
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or OutOfMemoryException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or ThreadStartException)
            {
                // No ETW on this system: the HUD simply shows "--" for the frame readings.
                ReleaseNative();
                return false;
            }
        }
    }

    private bool StartCore()
    {
        // A session survives a process that died badly: stop any left over with our name before starting.
        IntPtr orphan = EtwNative.AllocateProperties(EtwNative.SessionName);
        try
        {
            EtwNative.ControlTraceW(0, EtwNative.SessionName, orphan, EtwNative.EventTraceControlStop);
        }
        finally
        {
            Marshal.FreeHGlobal(orphan);
        }

        _properties = EtwNative.AllocateProperties(EtwNative.SessionName);
        LastError = EtwNative.StartTraceW(out _session, EtwNative.SessionName, _properties);
        if (LastError != EtwNative.ErrorSuccess)
        {
            _session = 0;
            ReleaseNative();
            return false;
        }

        // Only the present events are asked for (event-id filter, all keywords), so the reader wakes a few hundred times a
        // second at most. DXGI has few events and may fall back to unfiltered; DxgKrnl has thousands per second, so without
        // the filter it stays off and only DXGI (DirectX 10/11/12) is measured.
        bool dxgi = EnableProvider(DxgiProvider, DxgiPresentStart, allowUnfiltered: true);
        bool kernel = EnableProvider(DxgKrnlProvider, DxgKrnlPresent, allowUnfiltered: false);
        if (!dxgi && !kernel)
        {
            LastError = 1;
            StopSession();
            ReleaseNative();
            return false;
        }

        _logFile = EtwNative.AllocateLogFile(EtwNative.SessionName, _callback, out _logFileName);
        _trace = EtwNative.OpenTraceW(_logFile);
        if (_trace == EtwNative.InvalidTraceHandle)
        {
            LastError = 2;
            StopSession();
            ReleaseNative();
            return false;
        }

        lock (_data)
        {
            _buffer.Reset();
            _foregroundPid = 0;
            _foregroundIgnored = true;
            _dxgiPid = 0;
            _nextForegroundCheck = 0;
        }

        ulong[] handles = { _trace };
        _thread = new Thread(() => EtwNative.ProcessTrace(handles, 1, IntPtr.Zero, IntPtr.Zero)) { IsBackground = true, Name = "LHH-ETW-FPS" };
        _thread.Start();
        _running = true;
        LastError = 0;
        return true;
    }

    private bool EnableProvider(Guid provider, ushort eventId, bool allowUnfiltered)
    {
        // Keyword 0 means "every keyword": the event id alone decides what is delivered.
        if (EtwNative.EnableProvider(_session, provider, 0, new[] { eventId }) == EtwNative.ErrorSuccess)
            return true;

        return allowUnfiltered && EtwNative.EnableProvider(_session, provider, 0, null) == EtwNative.ErrorSuccess;
    }

    /// <summary>Stops the session and its thread; afterwards nothing of the monitor is running. Safe to call repeatedly.</summary>
    public void Stop()
    {
        lock (_lifecycle)
        {
            if (!_running)
                return;

            _running = false;
            StopSession();
            if (_trace != EtwNative.InvalidTraceHandle)
                EtwNative.CloseTrace(_trace); // unblocks ProcessTrace right away

            bool exited = _thread == null || _thread.Join(StopJoinMs);
            _thread = null;

            // Memory the reader may still touch is only freed once it has really left.
            if (exited)
                ReleaseNative();

            lock (_data)
                _buffer.Reset();
        }
    }

    private void StopSession()
    {
        if (_properties != IntPtr.Zero)
            EtwNative.ControlTraceW(_session, null, _properties, EtwNative.EventTraceControlStop);
    }

    private void ReleaseNative()
    {
        if (_logFile != IntPtr.Zero)
            Marshal.FreeHGlobal(_logFile);
        if (_logFileName != IntPtr.Zero)
            Marshal.FreeHGlobal(_logFileName);
        if (_properties != IntPtr.Zero)
            Marshal.FreeHGlobal(_properties);

        _logFile = _logFileName = _properties = IntPtr.Zero;
        _trace = EtwNative.InvalidTraceHandle;
        _session = 0;
    }

    /// <summary>The current reading for the game in front; <see cref="FpsSnapshot.HasData" /> is false when there is none.</summary>
    public FpsSnapshot GetSnapshot()
    {
        RefreshForeground();
        lock (_data)
            return _foregroundIgnored ? default : _buffer.ComputeSnapshot(Stopwatch.GetTimestamp());
    }

    // Called by the ETW thread for every delivered present (a few hundred per second at most): no allocations, no locks
    // unless the event belongs to the game in front.
    private void OnEvent(IntPtr record)
    {
        try
        {
            RefreshForeground();

            int pid = EtwNative.ReadProcessId(record);
            if (pid != Volatile.Read(ref _foregroundPid) || Volatile.Read(ref _foregroundIgnored))
                return;

            ushort id = EtwNative.ReadEventId(record);
            long timestamp = EtwNative.ReadTimestamp(record);

            if (EtwNative.IsProvider(record, DxgiHalves.First, DxgiHalves.Second))
            {
                if (id != DxgiPresentStart)
                    return;

                // DirectX 10/11/12: DXGI is the only source for this process, the kernel provider is then ignored.
                lock (_data)
                {
                    _dxgiPid = pid;
                    _buffer.RecordPresent(timestamp);
                }
            }
            else if (EtwNative.IsProvider(record, DxgKrnlHalves.First, DxgKrnlHalves.Second))
            {
                if (id != DxgKrnlPresent)
                    return;

                // OpenGL, Vulkan and DirectX 9 only show up here.
                lock (_data)
                {
                    if (_dxgiPid != pid)
                        _buffer.RecordPresent(timestamp, KernelMinDeltaMs);
                }
            }
        }
        catch (Exception)
        {
            // An exception must never cross back into the ETW runtime.
        }
    }

    // The window in front is looked at a few times per second, never per event; a new one starts a fresh measurement.
    private void RefreshForeground()
    {
        long now = Environment.TickCount64;
        if (now < Volatile.Read(ref _nextForegroundCheck))
            return;

        Volatile.Write(ref _nextForegroundCheck, now + ForegroundCheckMs);
        int pid = EtwNative.ForegroundProcessId();
        if (pid == Volatile.Read(ref _foregroundPid) && pid != 0)
            return;

        bool ignored = IsIgnored(pid);
        lock (_data)
        {
            if (pid == _foregroundPid)
                return;

            _foregroundPid = pid;
            _foregroundIgnored = ignored;
            _dxgiPid = 0;
            _buffer.Reset();
        }
    }

    // The shell, the compositor and this application are not games, even when they present frames all the time.
    private static bool IsIgnored(int pid)
    {
        if (pid <= 0 || pid == Environment.ProcessId)
            return true;

        try
        {
            using Process process = Process.GetProcessById(pid);
            string name = process.ProcessName;
            foreach (string ignored in IgnoredProcesses)
            {
                if (string.Equals(name, ignored, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        Stop();
        AppDomain.CurrentDomain.ProcessExit -= _exitHandler;
    }
}
