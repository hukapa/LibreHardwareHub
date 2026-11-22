// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// "Leituras" card of the HUD studio. Two blocks: the readings that are on the HUD, as compact rows in display order,
/// and the other ones as one-click chips. The rows and chips are rebuilt only when the structure changes (show, hide,
/// move, add, remove), never while a name is being typed.
/// </summary>
public sealed partial class OverlayEditorPanel
{
    private OverlayMetricList _metricsList;
    private FluentSectionCard _metricsCard;
    private ContextMenuStrip _sensorMenu;
    private OverlaySectionCaption _activeCaption;
    private OverlaySectionCaption _availableCaption;
    private OverlayChipFlow _availableChips;
    private NoteLabel _emptyNote;

    // Refresh actions of the reading rows; they are rebuilt with the list, unlike the page-level ones in _sync.
    private readonly List<Action> _metricSync = new();

    private Control BuildMetricsCard()
    {
        _metricsCard = new FluentSectionCard
        {
            Title = "Leituras",
            Subtitle = "Escolha o que aparece no HUD, mude nomes e cores, e ordene. Qualquer sensor do PC pode ser adicionado.",
            AutoHeight = true
        };

        ToolbarRow header = new() { OnSurface = true, Height = LogicalToDeviceUnits(40) };
        _activeCaption = new OverlaySectionCaption { Tag = PageMetrics.Fill };
        FluentButton add = PageMetrics.FitButton(new FluentButton { Text = "+ Adicionar sensor...", Variant = FluentButtonVariant.Accent, Tag = PageMetrics.AlignRight });
        add.Click += (_, _) => ShowSensorMenu(add);
        header.Controls.Add(_activeCaption);
        header.Controls.Add(add);

        // A little padding above and below: the drop guide of a drag also has to fit before the first and after the last row.
        _metricsList = new OverlayMetricList { Gap = 4, OnSurface = true, Padding = new Padding(0, LogicalToDeviceUnits(3), 0, LogicalToDeviceUnits(3)) };
        _metricsList.OrderChanged += (item, index) =>
        {
            if (_config.MoveActiveMetric(item, index))
                MetricsChanged();
        };
        _emptyNote = new NoteLabel("Nenhuma leitura ativa. Clique num sensor abaixo para adicionar ao HUD.");
        _availableCaption = new OverlaySectionCaption { Divider = true };
        _availableChips = new OverlayChipFlow();
        return Stack(_metricsCard, header, _metricsList, _emptyNote, _availableCaption, _availableChips);
    }

    private void RebuildMetricsList()
    {
        _metricsList.SuspendLayout();
        while (_metricsList.Controls.Count > 0)
            _metricsList.Controls[0].Dispose();

        _availableChips.Clear();
        _metricSync.Clear();

        int active = _config.ActiveMetricCount;
        for (int i = 0; i < _config.MetricItems.Count; i++)
        {
            if (i < active)
                _metricsList.Controls.Add(BuildMetricRow(_config.MetricItems[i], i, active));
            else
                _availableChips.Controls.Add(BuildMetricChip(_config.MetricItems[i]));
        }

        int hidden = _config.MetricItems.Count - active;
        _activeCaption.Text = $"ATIVOS NO HUD ({active})";
        _availableCaption.Text = $"DISPONÍVEIS ({hidden}) - clique para ativar";
        _emptyNote.Visible = active == 0;
        _availableCaption.Visible = hidden > 0;
        _availableChips.Visible = hidden > 0;

        Theme theme = Theme.Current;
        _metricsList.ApplyTheme(theme);
        _activeCaption.ApplyTheme(theme);
        _availableCaption.ApplyTheme(theme);
        _availableChips.ApplyTheme(theme);
        _emptyNote.ApplyTheme(theme);

        _metricsList.ResumeLayout(true);
        _availableChips.PerformLayout();
        _metricsCard.PerformLayout();
        _page.PerformLayout();
        _host.PerformLayout();
    }

    private OverlayMetricRow BuildMetricRow(OverlayMetricItem item, int index, int activeCount)
    {
        OverlayMetricRow row = new(item, _config, _toolTip, index > 0, index < activeCount - 1);
        row.Edited += (_, _) => Changed();
        row.FormatChanged += (_, _) =>
        {
            _telemetry.Update(_computer());
            Changed();
        };
        row.MoveRequested += (_, delta) =>
        {
            if (_config.Move(item, delta))
                MetricsChanged();
        };
        row.HideRequested += (_, _) =>
        {
            _config.SetMetricEnabled(item, false);
            MetricsChanged();
        };

        _metricSync.Add(row.Sync);
        return row;
    }

    private OverlayMetricChip BuildMetricChip(OverlayMetricItem item)
    {
        OverlayMetricChip chip = new(item);
        _toolTip.SetToolTip(chip, item.IsCustom ? "Clique para mostrar no HUD. O x apaga o sensor da configuração" : "Mostrar no HUD");
        chip.Activated += (_, _) =>
        {
            _config.SetMetricEnabled(item, true);
            MetricsChanged();
        };
        chip.RemoveRequested += (_, _) =>
        {
            if (_config.RemoveMetric(item))
                MetricsChanged();
        };

        return chip;
    }

    /// <summary>The set or order of readings changed: rebuild the rows and resolve the sensors again.</summary>
    private void MetricsChanged()
    {
        _telemetry.Invalidate();
        _telemetry.Update(_computer());
        // Rebuilding disposes the control being clicked; let its click handler return first.
        BeginInvoke(new Action(() =>
        {
            RebuildMetricsList();
            Changed(true);
        }));
    }

    /// <summary>Hardware > sensor type > sensor menu; a sub-menu is only filled when it is opened.</summary>
    private void ShowSensorMenu(Control anchor)
    {
        IComputer computer = _computer();
        if (computer == null)
            return;

        _sensorMenu?.Dispose();
        _sensorMenu = new ContextMenuStrip { Renderer = new ThemedToolStripRenderer(), ShowImageMargin = false, ShowCheckMargin = true };

        foreach (IHardware hardware in computer.Hardware)
        {
            List<ISensor> sensors = new();
            SensorLookup.Collect(hardware, sensors);
            if (sensors.Count == 0)
                continue;

            ToolStripMenuItem hardwareItem = new(hardware.Name);
            hardwareItem.DropDownItems.Add(new ToolStripMenuItem("...") { Enabled = false });
            hardwareItem.DropDownOpening += (_, _) => FillSensorMenu(hardwareItem, sensors);
            _sensorMenu.Items.Add(hardwareItem);
        }

        _sensorMenu.Show(anchor, new Point(0, anchor.Height + LogicalToDeviceUnits(4)));
    }

    private void FillSensorMenu(ToolStripMenuItem hardwareItem, List<ISensor> sensors)
    {
        hardwareItem.DropDownItems.Clear();
        Dictionary<string, ToolStripMenuItem> groups = new();

        foreach (ISensor sensor in sensors)
        {
            string groupName = SensorLookup.GroupName(sensor.SensorType);
            if (!groups.TryGetValue(groupName, out ToolStripMenuItem group))
            {
                group = new ToolStripMenuItem(groupName);
                groups[groupName] = group;
                hardwareItem.DropDownItems.Add(group);
            }

            string id = sensor.Identifier.ToString();
            ToolStripMenuItem item = new($"{sensor.Name}    {SensorLookup.Format(sensor)}") { Checked = _config.HasCustomSensor(id) };
            item.Click += (_, _) =>
            {
                _config.ToggleCustomSensor(id, sensor.Name);
                MetricsChanged();
            };
            group.DropDownItems.Add(item);
        }
    }
}
