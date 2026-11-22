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

/// <summary>"Leituras" card of the HUD studio: which readings are shown, their names, colors and order.</summary>
public sealed partial class OverlayEditorPanel
{
    private static readonly string[] MemoryFormats = { "GB + %", "Só GB", "Usado / Total" };

    private StackPanel _metricsList;
    private FluentSectionCard _metricsCard;
    private ContextMenuStrip _sensorMenu;

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

        ToolbarRow actions = new() { OnSurface = true, Height = LogicalToDeviceUnits(40) };
        FluentButton add = PageMetrics.FitButton(new FluentButton { Text = "Adicionar sensor...", Variant = FluentButtonVariant.Accent });
        add.Click += (_, _) => ShowSensorMenu(add);
        actions.Controls.Add(add);

        _metricsList = new StackPanel { Gap = 4, OnSurface = true };
        return Stack(_metricsCard, actions, _metricsList);
    }

    private static bool HasGauge(OverlayMetricItem item)
    {
        return item.IsCustom || item.Id is "cpu_temp" or "cpu_load" or "gpu_temp" or "gpu_load" or "ram_used";
    }

    // Frame rates have no bar, but they do change color (inverted: a low value is the bad one).
    private static bool HasSeverity(OverlayMetricItem item) => HasGauge(item) || OverlayConfig.IsFpsMetric(item.Id);

    private void RebuildMetricsList()
    {
        _metricsList.SuspendLayout();
        while (_metricsList.Controls.Count > 0)
            _metricsList.Controls[0].Dispose();

        _metricSync.Clear();

        for (int i = 0; i < _config.MetricItems.Count; i++)
            _metricsList.Controls.Add(BuildMetricRow(_config.MetricItems[i], i));

        _metricsList.ApplyTheme(Theme.Current);
        _metricsList.ResumeLayout(true);
        _metricsCard.PerformLayout();
    }

    private Control BuildMetricRow(OverlayMetricItem item, int index)
    {
        const int RowHeight = 28;
        ToolbarRow row = new() { OnSurface = true, Height = LogicalToDeviceUnits(36) };
        FluentButton visible = Toggle("Visível", () => item.Enabled, v => { _config.SetMetricEnabled(item, v); MetricsChanged(); }, RowHeight, _metricSync);

        FluentTextBox name = new() { Text = item.DisplayName, Width = LogicalToDeviceUnits(150), Height = LogicalToDeviceUnits(RowHeight), MaxLength = 24 };
        _toolTip.SetToolTip(name, item.IsCustom ? $"Sensor: {item.DefaultTitle}" : OverlayConfig.IsFpsMetric(item.Id)
            ? $"Leitura: {item.DefaultTitle}. Mede o jogo em primeiro plano enquanto o HUD está visível; sem jogo mostra --"
            : $"Leitura: {item.DefaultTitle}");
        name.TextChanged += (_, _) =>
        {
            item.CustomTitle = name.Text;
            Changed();
        };

        row.Controls.Add(visible);
        row.Controls.Add(name);
        row.Controls.Add(MetricColor("Cor do rótulo", () => _config.LabelColor, () => item.CustomLabelColor, c => item.CustomLabelColor = c, RowHeight));
        row.Controls.Add(MetricColor("Cor do valor", () => _config.ValueColor, () => item.CustomValueColor, c => item.CustomValueColor = c, RowHeight));

        if (HasSeverity(item))
        {
            FluentButton severity = Toggle("Severidade", () => item.UseDynamicColor, v => item.UseDynamicColor = v, RowHeight, _metricSync);
            _toolTip.SetToolTip(severity, OverlayConfig.IsFpsMetric(item.Id)
                ? "O valor fica amarelo abaixo de 60 FPS (acima de 16,7 ms) e vermelho abaixo de 30 FPS (acima de 33,3 ms)"
                : "O valor muda de cor conforme a temperatura ou a carga");
            row.Controls.Add(severity);
        }

        if (HasGauge(item))
            row.Controls.Add(Toggle("Barra", () => item.ShowBar, v => item.ShowBar = v, RowHeight, _metricSync));

        if (item.Id == "ram_used")
        {
            FluentButton format = PageMetrics.FitButton(new FluentButton { Text = MemoryFormats[Math.Clamp(item.DisplayFormat, 0, 2)] }, RowHeight);
            _toolTip.SetToolTip(format, "Formato do valor");
            format.Click += (_, _) =>
            {
                item.DisplayFormat = (item.DisplayFormat + 1) % MemoryFormats.Length;
                format.Text = MemoryFormats[item.DisplayFormat];
                PageMetrics.FitButton(format, RowHeight);
                _telemetry.Update(_computer());
                Changed();
            };
            row.Controls.Add(format);
        }

        // Right side, added from the edge inwards: remove (custom only), down, up.
        if (item.IsCustom)
        {
            FluentButton remove = PageMetrics.FitButton(new FluentButton { Text = "Remover", Tag = PageMetrics.AlignRight }, RowHeight);
            remove.Click += (_, _) =>
            {
                _config.MetricItems.Remove(item);
                MetricsChanged();
            };
            row.Controls.Add(remove);
        }

        row.Controls.Add(MoveButton(item, +1, "Mover para baixo", index < _config.MetricItems.Count - 1));
        row.Controls.Add(MoveButton(item, -1, "Mover para cima", index > 0));

        return row;
    }

    private FluentColorButton MetricColor(string toolTip, Func<Color> fallback, Func<Color?> get, Action<Color?> set, int heightLogical)
    {
        FluentColorButton button = new(string.Empty, allowReset: true) { Fallback = fallback, Height = LogicalToDeviceUnits(heightLogical) };
        _toolTip.SetToolTip(button, toolTip + " (o ponto indica que segue a cor geral)");
        button.ValueChanged += (_, _) =>
        {
            set(button.Value);
            Changed();
        };
        _metricSync.Add(() => button.Value = get());
        return button;
    }

    private FluentButton MoveButton(OverlayMetricItem item, int delta, string toolTip, bool enabled)
    {
        FluentButton button = new()
        {
            Text = delta < 0 ? "↑" : "↓",
            Width = LogicalToDeviceUnits(32),
            Height = LogicalToDeviceUnits(28),
            Enabled = enabled,
            Tag = PageMetrics.AlignRight
        };
        _toolTip.SetToolTip(button, toolTip);
        button.Click += (_, _) =>
        {
            if (_config.Move(item, delta))
                MetricsChanged();
        };
        return button;
    }

    /// <summary>The set or order of readings changed: rebuild the rows and resolve the sensors again.</summary>
    private void MetricsChanged()
    {
        _telemetry.Invalidate();
        _telemetry.Update(_computer());
        // Rebuilding disposes the button being clicked; let its click handler return first.
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
