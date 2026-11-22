// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Diagnostics;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay.Fps;

/// <summary>What the HUD shows for the game in the foreground. <see cref="HasData" /> is false when no game is presenting frames.</summary>
public readonly struct FpsSnapshot
{
    public FpsSnapshot(float fps, float onePercentLow, float frametimeMs)
    {
        HasData = true;
        Fps = fps;
        OnePercentLow = onePercentLow;
        FrametimeMs = frametimeMs;
    }

    public bool HasData { get; }

    /// <summary>Frames per second over the last second.</summary>
    public float Fps { get; }

    /// <summary>Benchmark-style "1% low": the average of the slowest 1% of the frames of the last 5 seconds, as FPS.</summary>
    public float OnePercentLow { get; }

    /// <summary>Average frame time over the last second, in milliseconds.</summary>
    public float FrametimeMs { get; }
}

/// <summary>
/// Frame times of the foreground game in a pre-allocated ring buffer. Recording a frame and computing a snapshot
/// allocate nothing: the 1% low is sorted in a scratch array that is reused. Not thread-safe: the owner locks.
/// </summary>
public sealed class FrameTimeBuffer
{
    /// <summary>Intervals at or below this are the same present reported twice (several queues, several providers).</summary>
    public const double MinFrameMs = 0.1;

    /// <summary>Longer intervals are pauses (Alt-Tab, loading screens), not frames.</summary>
    public const double MaxFrameMs = 2000.0;

    /// <summary>No frame for this long means nothing is being rendered any more (the desktop at rest).</summary>
    public const double StaleMs = 1500.0;

    public const double CurrentWindowMs = 1000.0;
    public const double LowWindowMs = 5000.0;

    private readonly long[] _timestamps;
    private readonly double[] _frameMs;
    private readonly double[] _sortScratch;
    private readonly double _msPerTick;
    private int _next;
    private int _count;
    private long _last;

    /// <param name="capacity">Frames kept; 4096 covers 5 s up to about 800 FPS.</param>
    /// <param name="frequency">Timestamp ticks per second; the QPC frequency by default.</param>
    public FrameTimeBuffer(int capacity = 4096, long frequency = 0)
    {
        _timestamps = new long[capacity];
        _frameMs = new double[capacity];
        _sortScratch = new double[capacity];
        _msPerTick = 1000.0 / (frequency > 0 ? frequency : Stopwatch.Frequency);
    }

    /// <summary>Number of frames currently stored.</summary>
    public int Count => _count;

    /// <summary>Forgets every frame; call when the foreground process changes.</summary>
    public void Reset()
    {
        _next = 0;
        _count = 0;
        _last = 0;
    }

    /// <summary>Registers a present at <paramref name="timestamp" /> (ticks) and stores the interval since the previous one.</summary>
    /// <param name="minDeltaMs">Intervals not above this are ignored as duplicates of the same present.</param>
    public void RecordPresent(long timestamp, double minDeltaMs = MinFrameMs)
    {
        if (_last == 0)
        {
            _last = timestamp;
            return;
        }

        double dtMs = (timestamp - _last) * _msPerTick;
        if (dtMs <= minDeltaMs)
            return;

        // Even a discarded long interval moves the baseline: the next frame is measured from this present.
        _last = timestamp;
        if (dtMs > MaxFrameMs)
            return;

        _timestamps[_next] = timestamp;
        _frameMs[_next] = dtMs;
        _next = _next + 1 == _timestamps.Length ? 0 : _next + 1;
        if (_count < _timestamps.Length)
            _count++;
    }

    /// <summary>FPS and frame time of the last second and the 1% low of the last five seconds, as of <paramref name="now" />.</summary>
    public FpsSnapshot ComputeSnapshot(long now)
    {
        if (_count == 0 || (now - _last) * _msPerTick > StaleMs)
            return default;

        long currentCut = now - (long)(CurrentWindowMs / _msPerTick);
        long lowCut = now - (long)(LowWindowMs / _msPerTick);

        // Newest to oldest: timestamps only grow, so the walk stops at the first frame outside the 5 s window.
        double sum = 0;
        int inCurrent = 0;
        int inLow = 0;
        int index = _next;
        for (int i = 0; i < _count; i++)
        {
            index = index == 0 ? _timestamps.Length - 1 : index - 1;
            if (_timestamps[index] < lowCut)
                break;

            double dt = _frameMs[index];
            _sortScratch[inLow++] = dt;
            if (_timestamps[index] >= currentCut)
            {
                sum += dt;
                inCurrent++;
            }
        }

        if (inLow == 0)
            return default;

        // The latest frame can be a bit older than a second while the game is still presenting (a slow game): use it alone.
        if (inCurrent == 0)
        {
            sum = _sortScratch[0];
            inCurrent = 1;
        }

        double frametime = sum / inCurrent;
        float fps = (float)(1000.0 / frametime);

        Array.Sort(_sortScratch, 0, inLow);
        int slowest = Math.Max(1, (inLow + 99) / 100); // ceil(count / 100), exact in integers
        double slowestSum = 0;
        for (int i = inLow - slowest; i < inLow; i++)
            slowestSum += _sortScratch[i];

        float low = (float)(1000.0 / (slowestSum / slowest));
        return new FpsSnapshot(fps, Math.Min(low, fps), (float)frametime);
    }
}
