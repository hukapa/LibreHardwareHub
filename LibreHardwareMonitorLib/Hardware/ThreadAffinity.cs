// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.SystemInformation;
using LibreHardwareMonitor.Interop;

namespace LibreHardwareMonitor.Hardware;

internal static class ThreadAffinity
{
    [ThreadStatic]
    private static GroupAffinity _currentThreadAffinity;

    [ThreadStatic]
    private static bool _isAffinityCached;

    /// <summary>
    /// Initializes static members of the <see cref="ThreadAffinity" /> class.
    /// </summary>
    static ThreadAffinity()
    {
        ProcessorGroupCount = Software.OperatingSystem.IsUnix ? 1 : PInvoke.GetActiveProcessorGroupCount();

        if (ProcessorGroupCount < 1)
            ProcessorGroupCount = 1;
    }

    /// <summary>
    /// Gets the processor group count.
    /// </summary>
    public static int ProcessorGroupCount { get; }

    /// <summary>
    /// Sets the processor group affinity for the current thread.
    /// </summary>
    /// <param name="affinity">The processor group affinity.</param>
    /// <returns>The previous processor group affinity.</returns>
    public static unsafe GroupAffinity Set(GroupAffinity affinity)
    {
        if (affinity == GroupAffinity.Undefined)
            return GroupAffinity.Undefined;

        // Skip redundant context switch syscall if thread is already pinned to this group and mask
        if (_isAffinityCached && _currentThreadAffinity == affinity)
            return _currentThreadAffinity;

        if (Software.OperatingSystem.IsUnix)
        {
            if (affinity.Group > 0)
                throw new ArgumentOutOfRangeException(nameof(affinity));

            ulong result = 0;
            if (LibC.sched_getaffinity(0, (IntPtr)8, ref result) != 0)
                return GroupAffinity.Undefined;

            ulong mask = affinity.Mask;
            if (LibC.sched_setaffinity(0, (IntPtr)8, ref mask) != 0)
            {
                _isAffinityCached = false;
                return GroupAffinity.Undefined;
            }

            _currentThreadAffinity = affinity;
            _isAffinityCached = true;
            return new GroupAffinity(0, result);
        }

        ulong maxValue = IntPtr.Size == 8 ? ulong.MaxValue : uint.MaxValue;
        if (affinity.Mask > maxValue)
            throw new ArgumentOutOfRangeException(nameof(affinity));

        var groupAffinity = new GROUP_AFFINITY { Group = affinity.Group, Mask = (UIntPtr)affinity.Mask };
        GROUP_AFFINITY previousGroupAffinity = new();

        IntPtr currentThread = PInvoke.GetCurrentThread();

        if (PInvoke.SetThreadGroupAffinity(new HANDLE(currentThread),
                                              &groupAffinity,
                                              &previousGroupAffinity))
        {
            _currentThreadAffinity = affinity;
            _isAffinityCached = true;
            return new GroupAffinity(previousGroupAffinity.Group, previousGroupAffinity.Mask);
        }

        _isAffinityCached = false;
        return GroupAffinity.Undefined;
    }
}
