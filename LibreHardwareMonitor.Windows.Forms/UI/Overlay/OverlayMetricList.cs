// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// The list of readings that are on the HUD. It follows a row that is being dragged: it works out where the row would
/// drop, draws a 2 px guide in the accent color between the two rows, scrolls the page when the cursor nears its top or
/// bottom edge, and reports the new position when the button is released. Esc cancels. Everything runs on the native
/// mouse capture of the controls; the timer and the keyboard filter exist only while a row is being dragged.
/// </summary>
public sealed class OverlayMetricList : StackPanel
{
    private const int EdgeLogical = 48;
    private const int MaxScrollStepLogical = 18;
    private const int ScrollIntervalMs = 30;

    private OverlayMetricRow _dragRow;
    private int _insertion = -1;
    private System.Windows.Forms.Timer _scrollTimer;
    private EscapeFilter _escape;

    /// <summary>The page that scrolls this list; it is scrolled while a row is dragged near its edges.</summary>
    public ScrollHost ScrollOwner { get; set; }

    /// <summary>A row was dropped at a new place: the reading and its new index among the active readings.</summary>
    public event Action<OverlayMetricItem, int> OrderChanged;

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is OverlayMetricRow row)
        {
            row.DragStarted += Row_DragStarted;
            row.DragMoved += Row_DragMoved;
            row.DragEnded += Row_DragEnded;
        }
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        if (e.Control is not OverlayMetricRow row)
            return;

        row.DragStarted -= Row_DragStarted;
        row.DragMoved -= Row_DragMoved;
        row.DragEnded -= Row_DragEnded;
        if (row == _dragRow)
            EndDrag(false);
    }

    private void Row_DragStarted(object sender, EventArgs e)
    {
        _dragRow = (OverlayMetricRow)sender;
        _insertion = Controls.GetChildIndex(_dragRow);

        _scrollTimer = new System.Windows.Forms.Timer { Interval = ScrollIntervalMs };
        _scrollTimer.Tick += ScrollTick;
        _scrollTimer.Start();

        _escape = new EscapeFilter(CancelDrag);
        Application.AddMessageFilter(_escape);
        Invalidate();
    }

    private void Row_DragMoved(object sender, Point screen) => UpdateInsertion(screen);

    private void Row_DragEnded(object sender, bool commit) => EndDrag(commit);

    private void CancelDrag()
    {
        _dragRow?.CancelGesture();
        EndDrag(false);
    }

    private void EndDrag(bool commit)
    {
        if (_dragRow == null)
            return;

        OverlayMetricRow row = _dragRow;
        int insertion = _insertion;
        // A row that was just removed from the list (a rebuild) has no position any more: nothing to commit.
        int from = Controls.Contains(row) ? Controls.GetChildIndex(row) : -1;

        _dragRow = null;
        _insertion = -1;
        StopHelpers();
        Invalidate();

        // The slot the row leaves does not count: dropping below its own position is one place less than the guide shows.
        int to = insertion > from ? insertion - 1 : insertion;
        if (commit && from >= 0 && insertion >= 0 && to != from)
            OrderChanged?.Invoke(row.Item, to);
    }

    private void StopHelpers()
    {
        if (_scrollTimer != null)
        {
            _scrollTimer.Stop();
            _scrollTimer.Tick -= ScrollTick;
            _scrollTimer.Dispose();
            _scrollTimer = null;
        }

        if (_escape != null)
        {
            Application.RemoveMessageFilter(_escape);
            _escape = null;
        }
    }

    // Insertion index 0..count: before the first row whose middle is below the cursor, or after the last one.
    private void UpdateInsertion(Point screen)
    {
        if (_dragRow == null)
            return;

        int y = PointToClient(screen).Y;
        int index = 0;
        foreach (Control row in Controls)
        {
            if (y < row.Top + (row.Height / 2))
                break;

            index++;
        }

        if (index == _insertion)
            return;

        _insertion = index;
        Invalidate();
    }

    private void ScrollTick(object sender, EventArgs e)
    {
        if (_dragRow == null || ScrollOwner == null)
            return;

        Point cursor = Cursor.Position;
        int y = ScrollOwner.PointToClient(cursor).Y;
        int edge = LogicalToDeviceUnits(EdgeLogical);
        int step = 0;
        if (y < edge)
            step = -ScrollStep(edge - y, edge);
        else if (y > ScrollOwner.Height - edge)
            step = ScrollStep(y - (ScrollOwner.Height - edge), edge);

        if (step == 0)
            return;

        ScrollOwner.ScrollBy(step);
        UpdateInsertion(cursor); // the rows moved under the cursor
    }

    // Faster the closer to (or beyond) the edge, never less than 2 px per tick.
    private int ScrollStep(int depth, int edge) => Math.Max(2, LogicalToDeviceUnits(MaxScrollStepLogical) * Math.Min(depth, edge) / edge);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_dragRow == null || _insertion < 0 || Controls.Count == 0 || Width <= 2)
            return;

        // The guide sits in the gap between two rows (or in the padding above the first and below the last one).
        float y;
        if (_insertion <= 0)
            y = Controls[0].Top / 2f;
        else if (_insertion >= Controls.Count)
            y = Controls[Controls.Count - 1].Bottom + (Padding.Bottom / 2f);
        else
            y = (Controls[_insertion - 1].Bottom + Controls[_insertion].Top) / 2f;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color accent = Theme.Current.AccentColor;
        float scale = DeviceDpi / 96f;
        float thickness = Math.Max(2f, 2f * scale);
        float inset = 8f * scale;
        float dot = thickness * 1.8f;
        using Pen pen = new(accent, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using SolidBrush brush = new(accent);
        g.DrawLine(pen, inset, y, Width - inset, y);
        g.FillEllipse(brush, inset - (dot / 2), y - (dot / 2), dot, dot);
        g.FillEllipse(brush, Width - inset - (dot / 2), y - (dot / 2), dot, dot);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            StopHelpers();

        base.Dispose(disposing);
    }

    /// <summary>Esc during a drag cancels it; the key press is swallowed so nothing else reacts to it.</summary>
    private sealed class EscapeFilter : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int VkEscape = 0x1B;
        private readonly Action _cancel;

        public EscapeFilter(Action cancel) => _cancel = cancel;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WmKeyDown || (int)m.WParam != VkEscape)
                return false;

            _cancel();
            return true;
        }
    }
}
