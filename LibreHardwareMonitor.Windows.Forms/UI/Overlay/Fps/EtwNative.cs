// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Runtime.InteropServices;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay.Fps;

/// <summary>
/// The few Win32 calls needed to listen to Event Tracing for Windows (advapi32.dll) and to know which window is in
/// front (user32.dll). x64 layouts only (the application is x64): the structures are written field by field at
/// documented offsets, so nothing depends on the marshaller. No injection, no hook in any game: ETW is the
/// operating system's own telemetry channel.
/// </summary>
internal static class EtwNative
{
    public const string SessionName = "LibreHardwareHub_FPS_ETW";

    public const uint ErrorSuccess = 0;
    public const uint ErrorAlreadyExists = 183;

    public const uint EventTraceControlStop = 1;
    public const uint EventControlCodeEnableProvider = 1;
    public const byte TraceLevelVerbose = 5;

    private const uint WnodeFlagTracedGuid = 0x00020000;
    private const uint EventTraceRealTimeMode = 0x00000100;
    private const uint EventTraceNoPerProcessorBuffering = 0x10000000;
    private const uint ProcessTraceModeRealTime = 0x00000100;
    private const uint ProcessTraceModeRawTimestamp = 0x00001000;
    private const uint ProcessTraceModeEventRecord = 0x10000000;
    private const uint EventFilterTypeEventId = 0x80000200;
    private const uint EnableTraceParametersVersion2 = 2;

    /// <summary>A failed OpenTrace returns INVALID_PROCESSTRACE_HANDLE (all bits set on x64).</summary>
    public const ulong InvalidTraceHandle = ulong.MaxValue;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void EventRecordCallback(IntPtr eventRecord);

    // ---- advapi32 ----

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern uint StartTraceW(out ulong sessionHandle, string sessionName, IntPtr properties);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern uint ControlTraceW(ulong sessionHandle, string sessionName, IntPtr properties, uint controlCode);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern uint EnableTraceEx2(ulong sessionHandle, ref Guid providerId, uint controlCode, byte level,
                                              ulong matchAnyKeyword, ulong matchAllKeyword, uint timeout, IntPtr enableParameters);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    public static extern ulong OpenTraceW(IntPtr logFile);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    public static extern uint ProcessTrace([In] ulong[] handleArray, uint handleCount, IntPtr startTime, IntPtr endTime);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    public static extern uint CloseTrace(ulong traceHandle);

    // ---- user32 ----

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    /// <summary>Process that owns the window in front, or 0 when there is none.</summary>
    public static int ForegroundProcessId()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero)
            return 0;

        GetWindowThreadProcessId(window, out uint pid);
        return (int)pid;
    }

    // ---- EVENT_TRACE_PROPERTIES ----
    // WNODE_HEADER (48): BufferSize@0, ProviderId@4, HistoricalContext@8, TimeStamp@16, Guid@24, ClientContext@40, Flags@44.
    // Then BufferSize@48, MinimumBuffers@52, MaximumBuffers@56, MaximumFileSize@60, LogFileMode@64, FlushTimer@68,
    // EnableFlags@72, AgeLimit@76, NumberOfBuffers@80 ... LoggerThreadId@104 (8), LogFileNameOffset@112, LoggerNameOffset@116.
    private const int PropertiesHeaderSize = 120;

    /// <summary>Unmanaged EVENT_TRACE_PROPERTIES for a real-time session, with room for the session name right after it.</summary>
    public static IntPtr AllocateProperties(string sessionName)
    {
        int size = PropertiesHeaderSize + ((sessionName.Length + 1) * 2);
        IntPtr block = Marshal.AllocHGlobal(size);
        Zero(block, size);

        Marshal.WriteInt32(block, 0, size);                                   // Wnode.BufferSize
        Marshal.WriteInt32(block, 40, 1);                                     // Wnode.ClientContext: 1 = QPC timestamps
        Marshal.WriteInt32(block, 44, unchecked((int)WnodeFlagTracedGuid));   // Wnode.Flags
        Marshal.WriteInt32(block, 48, 64);                                    // BufferSize (KB)
        Marshal.WriteInt32(block, 52, 2);                                     // MinimumBuffers
        Marshal.WriteInt32(block, 56, 16);                                    // MaximumBuffers
        Marshal.WriteInt32(block, 64, unchecked((int)(EventTraceRealTimeMode | EventTraceNoPerProcessorBuffering)));
        Marshal.WriteInt32(block, 68, 1);                                     // FlushTimer (s): the smallest delivery delay
        Marshal.WriteInt32(block, 116, PropertiesHeaderSize);                 // LoggerNameOffset
        Marshal.Copy(sessionName.ToCharArray(), 0, block + PropertiesHeaderSize, sessionName.Length);
        return block;
    }

    // ---- EVENT_TRACE_LOGFILEW (x64, 448 bytes) ----
    // LoggerName@8, ProcessTraceMode@28, EventRecordCallback@424, Context@440.
    private const int LogFileSize = 448;

    /// <summary>Unmanaged EVENT_TRACE_LOGFILEW that reads the real-time session <paramref name="sessionName" /> with raw (QPC) timestamps.</summary>
    public static IntPtr AllocateLogFile(string sessionName, EventRecordCallback callback, out IntPtr sessionNamePointer)
    {
        sessionNamePointer = Marshal.StringToHGlobalUni(sessionName);
        IntPtr block = Marshal.AllocHGlobal(LogFileSize);
        Zero(block, LogFileSize);

        Marshal.WriteIntPtr(block, 8, sessionNamePointer);
        Marshal.WriteInt32(block, 28, unchecked((int)(ProcessTraceModeRealTime | ProcessTraceModeEventRecord | ProcessTraceModeRawTimestamp)));
        Marshal.WriteIntPtr(block, 424, Marshal.GetFunctionPointerForDelegate(callback));
        return block;
    }

    // ---- EnableTraceEx2 with an event-id filter ----
    // ENABLE_TRACE_PARAMETERS (48): Version@0, EnableProperty@4, ControlFlags@8, SourceId@12 (16), EnableFilterDesc@32, FilterDescCount@40.
    // EVENT_FILTER_DESCRIPTOR (16): Ptr@0, Size@8, Type@12.  EVENT_FILTER_EVENT_ID: FilterIn@0, Reserved@1, Count@2, Events@4.
    private const int ParametersSize = 48;
    private const int DescriptorSize = 16;

    /// <summary>
    /// Enables <paramref name="provider" /> and, when <paramref name="eventIds" /> is given, asks the kernel to deliver only
    /// those events (a handful of presents per second instead of every event of the provider). Returns the Win32 error.
    /// </summary>
    public static uint EnableProvider(ulong session, Guid provider, ulong keywords, ushort[] eventIds)
    {
        IntPtr block = IntPtr.Zero;
        try
        {
            IntPtr parameters = IntPtr.Zero;
            if (eventIds is { Length: > 0 })
            {
                int filterSize = 4 + (eventIds.Length * 2);
                block = Marshal.AllocHGlobal(ParametersSize + DescriptorSize + filterSize);
                Zero(block, ParametersSize + DescriptorSize + filterSize);

                IntPtr descriptor = block + ParametersSize;
                IntPtr filter = descriptor + DescriptorSize;

                Marshal.WriteByte(filter, 0, 1);                                       // FilterIn
                Marshal.WriteInt16(filter, 2, (short)eventIds.Length);                 // Count
                for (int i = 0; i < eventIds.Length; i++)
                    Marshal.WriteInt16(filter, 4 + (i * 2), (short)eventIds[i]);

                Marshal.WriteInt64(descriptor, 0, filter.ToInt64());                   // Ptr
                Marshal.WriteInt32(descriptor, 8, filterSize);                         // Size
                Marshal.WriteInt32(descriptor, 12, unchecked((int)EventFilterTypeEventId));

                Marshal.WriteInt32(block, 0, (int)EnableTraceParametersVersion2);      // Version
                Marshal.WriteInt64(block, 32, descriptor.ToInt64());                   // EnableFilterDesc
                Marshal.WriteInt32(block, 40, 1);                                      // FilterDescCount
                parameters = block;
            }

            return EnableTraceEx2(session, ref provider, EventControlCodeEnableProvider, TraceLevelVerbose, keywords, 0, 0, parameters);
        }
        finally
        {
            if (block != IntPtr.Zero)
                Marshal.FreeHGlobal(block);
        }
    }

    // ---- EVENT_RECORD ----
    // EVENT_HEADER: ThreadId@8, ProcessId@12, TimeStamp@16, ProviderId@24 (GUID), EVENT_DESCRIPTOR@40: Id@40 (ushort),
    // Version@42, Channel@43, Level@44, Opcode@45, Task@46, Keyword@48.

    public static int ReadProcessId(IntPtr record) => Marshal.ReadInt32(record, 12);

    public static long ReadTimestamp(IntPtr record) => Marshal.ReadInt64(record, 16);

    public static ushort ReadEventId(IntPtr record) => (ushort)Marshal.ReadInt16(record, 40);

    public static byte ReadOpcode(IntPtr record) => Marshal.ReadByte(record, 45);

    /// <summary>Compares the provider GUID of the record with a GUID given as its two little-endian halves, without allocating.</summary>
    public static bool IsProvider(IntPtr record, long first, long second) =>
        Marshal.ReadInt64(record, 24) == first && Marshal.ReadInt64(record, 32) == second;

    /// <summary>The two 64-bit halves of a GUID, as stored in an event header.</summary>
    public static (long First, long Second) Halves(Guid guid)
    {
        byte[] bytes = guid.ToByteArray();
        return (BitConverter.ToInt64(bytes, 0), BitConverter.ToInt64(bytes, 8));
    }

    private static void Zero(IntPtr block, int size)
    {
        int offset = 0;
        for (; offset + 8 <= size; offset += 8)
            Marshal.WriteInt64(block, offset, 0);

        for (; offset < size; offset++)
            Marshal.WriteByte(block, offset, 0);
    }
}
