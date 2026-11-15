// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Overlay;

namespace LibreHardwareMonitor.Windows.Forms.UI;

/// <summary>
/// In-game overlay wiring: one configuration and one telemetry source shared by the HUD window and the
/// editor preview, the global hotkeys, and the per-tick refresh.
/// </summary>
public sealed partial class MainForm
{
    private readonly OverlayConfig _overlayConfig;
    private readonly OverlayTelemetry _overlayTelemetry;
    private OverlayForm _overlayForm;
    private GlobalHotkeyManager _hotkeyManager;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_hotkeyManager != null)
            return;

        _hotkeyManager = new GlobalHotkeyManager(Handle);
        _hotkeyManager.HotkeyTriggered += OnGlobalHotkeyTriggered;

        _overlayForm = new OverlayForm(_overlayConfig, _overlayTelemetry);
        _overlayForm.UserMoved += delegate
        {
            _overlayConfig.Save(_settings);
            _overlayEditorPanel?.SyncState();
        };

        ApplyOverlayConfig();
    }

    private void OnGlobalHotkeyTriggered(int id)
    {
        if (id == GlobalHotkeyManager.HotkeyOverlayToggle)
            _overlayConfig.Enabled = !_overlayConfig.Enabled;
        else if (id == GlobalHotkeyManager.HotkeyClickThroughToggle)
            _overlayConfig.ClickThrough = !_overlayConfig.ClickThrough;
        else
            return;

        ApplyOverlayConfig();
        _overlayEditorPanel?.SyncState();
    }

    /// <summary>Persists the overlay configuration and brings the hotkeys and the HUD window in line with it.</summary>
    private void ApplyOverlayConfig()
    {
        _overlayConfig.Save(_settings);
        _hotkeyManager?.RegisterHotkey(GlobalHotkeyManager.HotkeyOverlayToggle, _overlayConfig.Hotkey);
        _hotkeyManager?.RegisterHotkey(GlobalHotkeyManager.HotkeyClickThroughToggle, _overlayConfig.LockHotkey);

        if (_overlayForm == null)
            return;

        if (!_overlayConfig.Enabled)
        {
            _overlayForm.Hide();
        }
        else if (_overlayForm.Visible)
        {
            _overlayForm.ApplyConfig();
        }
        else
        {
            // Resolve the sensors and take a first reading so the HUD never appears empty.
            _overlayTelemetry.Invalidate();
            _overlayTelemetry.Update(_computer);
            _overlayForm.Show();
        }

        ApplyAdaptivePolling();
    }

    /// <summary>Called after every sensor update: reads the HUD values once and repaints only what is on screen and changed.</summary>
    private void UpdateOverlay(bool mainWindowVisible)
    {
        bool hud = _overlayForm is { Visible: true };
        bool editor = mainWindowVisible && _overlayEditorPanel is { Visible: true };
        if ((!hud && !editor) || !_overlayTelemetry.Update(_computer))
            return;

        if (hud)
            _overlayForm.Render();
        if (editor)
            _overlayEditorPanel.RefreshPreview();
    }

    /// <summary>Opens the HUD studio with fresh readings in its preview.</summary>
    private void ShowOverlayEditor()
    {
        if (_overlayEditorPanel == null)
            return;

        _overlayEditorPanel.BringToFront();
        _overlayTelemetry.Update(_computer);
        _overlayEditorPanel.SyncState();
    }

    /// <summary>"Mostrar no Overlay" of the sensor tree context menu.</summary>
    private void ToggleOverlaySensor(ISensor sensor)
    {
        _overlayConfig.ToggleCustomSensor(sensor.Identifier.ToString(), sensor.Name);
        _overlayTelemetry.Invalidate();
        _overlayTelemetry.Update(_computer);
        ApplyOverlayConfig();
    }
}
