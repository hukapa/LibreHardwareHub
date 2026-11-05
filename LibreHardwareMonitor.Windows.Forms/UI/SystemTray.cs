// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.FanControl;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI;

public class SystemTray : IDisposable
{
    private IComputer _computer;
    private readonly PersistentSettings _settings;
    private readonly UnitManager _unitManager;
    private readonly List<SensorNotifyIcon> _sensorList = new();
    private bool _mainIconEnabled;
    private readonly NotifyIconAdv _mainIcon;
    private readonly ContextMenuStrip _contextMenuStrip;

    public bool HasSensors => _sensorList.Count > 0;

    public SystemTray(IComputer computer, PersistentSettings settings, UnitManager unitManager)
    {
        _computer = computer;
        _settings = settings;
        _unitManager = unitManager;
        computer.HardwareAdded += HardwareAdded;
        computer.HardwareRemoved += HardwareRemoved;

        _mainIcon = new NotifyIconAdv();

        _contextMenuStrip = new ContextMenuStrip();
        ToolStripItem hideShowItem = new ToolStripMenuItem("Hide/Show");
        hideShowItem.Click += delegate
        {
            SendHideShowCommand();
        };
        _contextMenuStrip.Items.Add(hideShowItem);
        _contextMenuStrip.Items.Add(new ToolStripSeparator());
        ToolStripItem exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += delegate
        {
            SendExitCommand();
        };
        _contextMenuStrip.Items.Add(exitItem);
        _mainIcon.ContextMenuStrip = _contextMenuStrip;
        _mainIcon.DoubleClick += delegate
        {
            SendHideShowCommand();
        };
        _mainIcon.Icon = EmbeddedResources.GetIcon("smallicon.ico");
        _mainIcon.Text = "Libre Hardware Monitor";
    }

    public void SetFanControlEngine(FanControlEngine engine)
    {
        if (engine == null)
            return;

        ToolStripMenuItem fanMenu = new("Perfil de Ventoinhas");
        ToolStripMenuItem biosItem = new("BIOS Padrao");
        ToolStripMenuItem silentItem = new("Silencioso");
        ToolStripMenuItem balancedItem = new("Equilibrado");
        ToolStripMenuItem perfItem = new("Desempenho");
        ToolStripMenuItem customItem = new("Personalizado");

        void UpdateChecks()
        {
            biosItem.Checked = engine.ActivePreset == GlobalFanPreset.DefaultBios;
            silentItem.Checked = engine.ActivePreset == GlobalFanPreset.Silent;
            balancedItem.Checked = engine.ActivePreset == GlobalFanPreset.Balanced;
            perfItem.Checked = engine.ActivePreset == GlobalFanPreset.Performance;
            customItem.Checked = engine.ActivePreset == GlobalFanPreset.Custom;
        }

        biosItem.Click += (_, _) => { engine.ApplyPreset(GlobalFanPreset.DefaultBios); UpdateChecks(); };
        silentItem.Click += (_, _) => { engine.ApplyPreset(GlobalFanPreset.Silent); UpdateChecks(); };
        balancedItem.Click += (_, _) => { engine.ApplyPreset(GlobalFanPreset.Balanced); UpdateChecks(); };
        perfItem.Click += (_, _) => { engine.ApplyPreset(GlobalFanPreset.Performance); UpdateChecks(); };
        customItem.Click += (_, _) => { engine.ApplyPreset(GlobalFanPreset.Custom); UpdateChecks(); };

        fanMenu.DropDownItems.Add(biosItem);
        fanMenu.DropDownItems.Add(silentItem);
        fanMenu.DropDownItems.Add(balancedItem);
        fanMenu.DropDownItems.Add(perfItem);
        fanMenu.DropDownItems.Add(customItem);

        fanMenu.DropDownOpening += (_, _) => UpdateChecks();
        engine.PresetChanged += (_, _) => UpdateChecks();

        _contextMenuStrip.Items.Insert(1, fanMenu);
    }

    private void HardwareRemoved(IHardware hardware)
    {
        hardware.SensorAdded -= SensorAdded;
        hardware.SensorRemoved -= SensorRemoved;

        foreach (ISensor sensor in hardware.Sensors)
            SensorRemoved(sensor);

        foreach (IHardware subHardware in hardware.SubHardware)
            HardwareRemoved(subHardware);
    }

    private void HardwareAdded(IHardware hardware)
    {
        foreach (ISensor sensor in hardware.Sensors)
            SensorAdded(sensor);

        hardware.SensorAdded += SensorAdded;
        hardware.SensorRemoved += SensorRemoved;

        foreach (IHardware subHardware in hardware.SubHardware)
            HardwareAdded(subHardware);
    }

    private void SensorAdded(ISensor sensor)
    {
        if (_settings.GetValue(new Identifier(sensor.Identifier, "tray").ToString(), false))
            Add(sensor, false);
    }

    private void SensorRemoved(ISensor sensor)
    {
        if (Contains(sensor))
            Remove(sensor, false);
    }

    public void Dispose()
    {
        foreach (SensorNotifyIcon icon in _sensorList)
            icon.Dispose();
        _mainIcon.Dispose();
    }

    public void Redraw()
    {
        foreach (SensorNotifyIcon icon in _sensorList)
            icon.Update();
    }

    public bool Contains(ISensor sensor)
    {
        foreach (SensorNotifyIcon icon in _sensorList)
            if (icon.Sensor == sensor)
                return true;
        return false;
    }

    public void Add(ISensor sensor, bool balloonTip)
    {
        if (Contains(sensor))
            return;

        _sensorList.Add(new SensorNotifyIcon(this, sensor, _settings, _unitManager));
        UpdateMainIconVisibility();
        _settings.SetValue(new Identifier(sensor.Identifier, "tray").ToString(), true);
    }

    public void Remove(ISensor sensor)
    {
        Remove(sensor, true);
    }

    private void Remove(ISensor sensor, bool deleteConfig)
    {
        SensorNotifyIcon instance = null;
        foreach (SensorNotifyIcon icon in _sensorList)
            if (icon.Sensor == sensor)
                instance = icon;

        if (instance != null)
        {
            _sensorList.Remove(instance);
            instance.Dispose();
            UpdateMainIconVisibility();
            if (deleteConfig)
                _settings.Remove(new Identifier(sensor.Identifier, "tray").ToString());
        }
    }

    public event EventHandler HideShowCommand;

    public void SendHideShowCommand()
    {
        HideShowCommand?.Invoke(this, null);
    }

    public event EventHandler ExitCommand;

    public void SendExitCommand()
    {
        ExitCommand?.Invoke(this, null);
    }

    private void UpdateMainIconVisibility()
    {
        if (_mainIconEnabled)
            _mainIcon.Visible = _sensorList.Count == 0;
        else
            _mainIcon.Visible = false;
    }

    public bool IsMainIconEnabled
    {
        get { return _mainIconEnabled; }
        set
        {
            if (_mainIconEnabled != value)
            {
                _mainIconEnabled = value;
                UpdateMainIconVisibility();
            }
        }
    }
}
