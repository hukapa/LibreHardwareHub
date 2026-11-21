// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Settings;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;
using LibreHardwareMonitor.Windows.Forms.UI.Updates;

namespace LibreHardwareMonitor.Windows.Forms.UI;

/// <summary>Wiring of the "Definicoes" page and of the update check that runs a few seconds after startup.</summary>
public sealed partial class MainForm
{
    private const int UpdateCheckDelayMilliseconds = 6000;

    private UpdateService _updateService;
    private SettingsPanel _settingsPanel;
    private UserOption _startMinimized;
    private System.Windows.Forms.Timer _updateStartTimer;

    /// <summary>Creates the update service and schedules the single startup check (a one-shot UI timer, gone after it fires).</summary>
    private void InitializeUpdates()
    {
        _updateService = new UpdateService(_settings, RequestExit);
        if (!_updateService.CheckOnStartup)
            return;

        _updateStartTimer = new System.Windows.Forms.Timer { Interval = UpdateCheckDelayMilliseconds };
        _updateStartTimer.Tick += async (_, _) =>
        {
            _updateStartTimer.Stop();
            _updateStartTimer.Dispose();
            _updateStartTimer = null;
            await _updateService.RunStartupCheckAsync(this);
        };
        _updateStartTimer.Start();
    }

    /// <summary>The settings page is built on first use: nothing is created until it is opened.</summary>
    private void ShowSettings()
    {
        if (_settingsPanel == null)
        {
            _settingsPanel = new SettingsPanel(new SettingsContext
            {
                Startup = _autoStart,
                StartupAvailable = _startupManager.IsAvailable,
                StartMinimized = _startMinimized,
                MinimizeOnClose = _minimizeOnClose,
                GetUnit = () => _unitManager.TemperatureUnit,
                SetUnit = unit =>
                {
                    if (unit == TemperatureUnit.Fahrenheit)
                        FahrenheitMenuItem_Click(this, EventArgs.Empty);
                    else
                        CelsiusMenuItem_Click(this, EventArgs.Empty);
                },
                SetTheme = SetThemeById,
                Updates = _updateService
            }) { Dock = DockStyle.Fill };
            _content.Controls.Add(_settingsPanel);
        }

        _settingsPanel.Visible = true;
        _settingsPanel.BringToFront();
        _settingsPanel.SyncState();
    }

    // Same path as the Tema menu, so the menu check marks, the plot colors and the saved choice all follow.
    private void SetThemeById(string id)
    {
        Theme target = Theme.All.Find(theme => theme.Id == id);
        if (target == null)
            return;

        foreach (ToolStripItem item in themeMenuItem.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && menuItem.Text == target.DisplayName)
            {
                menuItem.PerformClick();
                return;
            }
        }
    }

    // The update prompt waits for the window to be on screen (the app may start hidden in the tray).
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible && _updateService != null && IsHandleCreated)
            BeginInvoke(new Action(() => _updateService?.ShowPendingPrompt(this)));
    }

    private void DisposeUpdates()
    {
        _updateStartTimer?.Stop();
        _updateStartTimer?.Dispose();
        _updateStartTimer = null;
        _updateService?.Dispose();
    }
}
