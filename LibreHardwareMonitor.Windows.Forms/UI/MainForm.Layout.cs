// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Aga.Controls.Tree;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Dashboard;
using LibreHardwareMonitor.Windows.Forms.UI.FanControl;
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Overlay;
using LibreHardwareMonitor.Windows.Forms.UI.Tuning;

namespace LibreHardwareMonitor.Windows.Forms.UI;

/// <summary>
/// Fluent shell layout: hardware navigation sidebar, KPI summary strip and the sensor tree
/// hosted in a rounded card. Selecting a device scopes the tree to that hardware.
/// </summary>
public sealed partial class MainForm
{
    private const string ScopeSettingKey = "navigation.scope";
    private const string SidebarModeSettingKey = "navigation.sidebar";
    private const string SidebarCollapsedSettingKey = "navigation.collapsed";
    private const int SidebarLogicalWidth = 232;
    private const int SidebarCompactLogicalWidth = 56;
    private const int CompactBreakpointLogical = 860;
    private const int SearchLogicalWidth = 300;

    private SidebarPanel _sidebar;
    private SummaryPanel _summary;
    private Panel _content;
    private CardPanel _treeCard;
    private SearchBox _search;
    private ToolbarRow _toolbar;
    private FanControlPanel _fanControlPanel;
    private GpuTuningPanel _gpuTuningPanel;
    private CpuTuningPanel _cpuTuningPanel;
    private HardwareBentoPanel _hardwareBentoPanel;
    private OverlayEditorPanel _overlayEditorPanel;
    private FluentButton _btnVisualCards;
    private FluentButton _btnRawTable;
    private bool _isVisualMode = true;
    private int _summaryTickCounter = 0;

    private void InitializeShell()
    {
        SuspendLayout();

        Controls.Remove(splitContainer);
        _treeCard = new CardPanel { Dock = DockStyle.Fill };
        _treeCard.Controls.Add(splitContainer);

        _hardwareBentoPanel = new HardwareBentoPanel { Dock = DockStyle.Fill, Visible = true, Padding = new Padding(0, LogicalToDeviceUnits(8), 0, 0) };
        _overlayEditorPanel = new OverlayEditorPanel(_overlayConfig, _overlayTelemetry, () => _computer, ApplyOverlayConfig) { Dock = DockStyle.Fill, Visible = false };
        _fanControlPanel = new FanControlPanel { Dock = DockStyle.Fill, Visible = false };
        _gpuTuningPanel = new GpuTuningPanel { Dock = DockStyle.Fill, Visible = false };
        _cpuTuningPanel = new CpuTuningPanel { Dock = DockStyle.Fill, Visible = false };

        _summary = new SummaryPanel { Dock = DockStyle.Top, Visible = false };
        _summary.UseFahrenheit = () => _unitManager.TemperatureUnit == TemperatureUnit.Fahrenheit;
        _summary.CardClicked += Summary_CardClicked;

        int gutter = LogicalToDeviceUnits(12);
        _search = new SearchBox { Width = LogicalToDeviceUnits(SearchLogicalWidth) };
        _search.SearchChanged += Search_SearchChanged;

        _btnRawTable = PageMetrics.FitButton(new FluentButton
        {
            Text = "Tabela Completa",
            Tag = PageMetrics.AlignRight
        });
        _btnRawTable.Click += (s, e) => SetViewMode(false);

        _btnVisualCards = PageMetrics.FitButton(new FluentButton
        {
            Text = "Vista Visual",
            Variant = FluentButtonVariant.Accent,
            Tag = PageMetrics.AlignRight
        });
        _btnVisualCards.Click += (s, e) => SetViewMode(true);

        _toolbar = new ToolbarRow
        {
            Dock = DockStyle.Top,
            Height = LogicalToDeviceUnits(40),
            Padding = new Padding(0, LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(4))
        };
        _toolbar.Controls.Add(_search);
        _toolbar.Controls.Add(_btnRawTable);
        _toolbar.Controls.Add(_btnVisualCards);

        // Docking runs from the back of the z-order: summary, then toolbar, then panels.
        _content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, LogicalToDeviceUnits(8), gutter, gutter) };
        _content.Controls.Add(_overlayEditorPanel);
        _content.Controls.Add(_cpuTuningPanel);
        _content.Controls.Add(_gpuTuningPanel);
        _content.Controls.Add(_fanControlPanel);
        _content.Controls.Add(_hardwareBentoPanel);
        _content.Controls.Add(_treeCard);
        _content.Controls.Add(_toolbar);
        _content.Controls.Add(_summary);

        _sidebar = new SidebarPanel { Dock = DockStyle.Left, Width = LogicalToDeviceUnits(SidebarLogicalWidth) };
        _sidebar.CollapsedSections = _settings.GetValue(SidebarCollapsedSettingKey, "Rede");
        _sidebar.SelectionChanged += Sidebar_SelectionChanged;
        _sidebar.CollapsedSectionsChanged += delegate { _settings.SetValue(SidebarCollapsedSettingKey, _sidebar.CollapsedSections); };
        _sidebar.CompactToggleRequested += delegate
        {
            _settings.SetValue(SidebarModeSettingKey, _sidebar.Compact ? "wide" : "compact");
            UpdateShellDensity();
        };

        // Docking runs from the back of the z-order: menu first, then sidebar, then content.
        Controls.Add(_content);
        Controls.Add(_sidebar);
        _content.BringToFront();
        mainMenu.SendToBack();

        string savedView = _settings.GetValue("ui.view_mode", "visual");
        SetViewMode(savedView != "table");

        Resize += delegate { UpdateShellDensity(); };
        UpdateShellDensity();
        ResumeLayout(true);
    }

    private void SetViewMode(bool visual)
    {
        _isVisualMode = visual;
        _btnVisualCards.Variant = visual ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;
        _btnRawTable.Variant = !visual ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;

        bool isToolPage = _sidebar?.SelectedItem?.Tag is string;
        if (!isToolPage)
        {
            _hardwareBentoPanel.Visible = visual;
            _treeCard.Visible = !visual;

            if (visual)
            {
                if (_summary != null)
                    _summary.Visible = false;
                _hardwareBentoPanel.BringToFront();
                _hardwareBentoPanel.UpdateTelemetry(_computer);
                _hardwareBentoPanel.SetScope(_sidebar?.SelectedItem?.Tag as HardwareNode);
            }
            else
            {
                _treeCard.BringToFront();
                HardwareNode scopeNode = _sidebar?.SelectedItem?.Tag as HardwareNode;
                bool isAllDevices = scopeNode == null;
                if (_summary != null)
                {
                    _summary.Visible = isAllDevices && _summary.HasMetrics;
                    if (_summary.Visible)
                        _summary.Invalidate();
                }
                ApplyScope(scopeNode, false);
            }
        }

        _settings.SetValue("ui.view_mode", visual ? "visual" : "table");
    }

    private void UpdateShellDensity()
    {
        if (_sidebar == null)
            return;

        // Automatic by window width unless the user chose a mode with the navigation button.
        string mode = _settings.GetValue(SidebarModeSettingKey, "auto");
        bool compact = mode == "auto" ? ClientSize.Width < LogicalToDeviceUnits(CompactBreakpointLogical) : mode == "compact";
        _sidebar.Compact = compact;
        _sidebar.Width = LogicalToDeviceUnits(compact ? SidebarCompactLogicalWidth : SidebarLogicalWidth);
    }

    private static string SidebarSection(HardwareType type)
    {
        return type switch
        {
            HardwareType.Memory => "Memória",
            HardwareType.Storage => "Armazenamento",
            HardwareType.Network => "Rede",
            _ => "Hardware"
        };
    }

    /// <summary>Rebuilds the device list and summary cards after hardware was added or removed.</summary>
    private void RefreshShell()
    {
        if (_sidebar == null)
            return;

        _overlayTelemetry.Invalidate();

        HardwareNode scope = TreeScope;
        if (scope != null && !_root.Nodes.Contains(scope))
            scope = null;

        // The first entries keep a fixed position: Ctrl+1..5 jump to them.
        const string tools = "Ferramentas";
        string savedScope = _settings.GetValue(ScopeSettingKey, string.Empty);
        List<SidebarItem> items = new()
        {
            new SidebarItem("Visão geral", FluentIcons.Computer, null),
            new SidebarItem("Ventoinhas", FluentIcons.ForSensorType(SensorType.Fan), "fan_control", tools),
            new SidebarItem("Ajuste de CPU", FluentIcons.ForHardware(HardwareType.Cpu), "cpu_tuning", tools),
            new SidebarItem("Ajuste de GPU", FluentIcons.ForSensorType(SensorType.Clock), "gpu_tuning", tools),
            new SidebarItem("Overlay HUD", FluentIcons.ForSensorType(SensorType.Level), "overlay_hud", tools)
        };

        // Devices grouped by kind, so the many memory modules and network adapters do not bury the main components.
        foreach (string section in new[] { "Hardware", "Memória", "Armazenamento", "Rede" })
        {
            foreach (HardwareNode node in _root.Nodes.OfType<HardwareNode>())
            {
                if (SidebarSection(node.Hardware.HardwareType) != section)
                    continue;

                items.Add(new SidebarItem(node.Text, node.Image, node, section));
                if (scope == null && savedScope.Length > 0 && node.Hardware.Identifier.ToString() == savedScope)
                    scope = node;
            }
        }

        _sidebar.SetItems(items, scope);
        ResolveSummary();
        NavigateView(scope);
    }

    private void ResolveSummary()
    {
        if (_summary == null || _computer == null)
            return;

        _summary.SetMetrics(SummaryMetrics.Resolve(_computer.Hardware));
        bool toolPage = _sidebar?.SelectedItem?.Tag is string;
        bool isAllDevices = _sidebar?.SelectedItem?.Tag == null;
        _summary.Visible = !_isVisualMode && isAllDevices && _summary.HasMetrics && !toolPage;
    }

    /// <summary>Sensors may appear a few update cycles after the hardware; dynamically keeps KPIs resolved.</summary>
    private void UpdateShell()
    {
        if (_summary == null)
            return;

        _summaryTickCounter++;
        if (!_summary.HasMetrics || _summaryTickCounter % 5 == 0)
            ResolveSummary();

        if (_summary.Visible)
            _summary.Invalidate();
    }

    private HardwareNode TreeScope => (treeView.Model as TreeModel)?.Scope as HardwareNode;

    private void Sidebar_SelectionChanged(object sender, EventArgs e)
    {
        NavigateView(_sidebar.SelectedItem?.Tag);
    }

    private void NavigateView(object tag)
    {
        // 1. Tool Pages
        if (tag is string toolTag)
        {
            if (_summary != null) _summary.Visible = false;
            if (_toolbar != null) _toolbar.Visible = false;
            if (_hardwareBentoPanel != null) _hardwareBentoPanel.Visible = false;
            if (_treeCard != null) _treeCard.Visible = false;

            if (_fanControlPanel != null) _fanControlPanel.Visible = toolTag == "fan_control";
            if (_cpuTuningPanel != null) _cpuTuningPanel.Visible = toolTag == "cpu_tuning";
            if (_gpuTuningPanel != null) _gpuTuningPanel.Visible = toolTag == "gpu_tuning";
            if (_overlayEditorPanel != null) _overlayEditorPanel.Visible = toolTag == "overlay_hud";

            switch (toolTag)
            {
                case "fan_control":
                    if (_fanControlPanel != null)
                    {
                        _fanControlPanel.BringToFront();
                        if (_fanControlPanel.CardCount == 0)
                            _fanControlPanel.RefreshChannels();
                        _fanControlPanel.UpdateTelemetry();
                    }
                    break;
                case "cpu_tuning":
                    if (_cpuTuningPanel != null)
                    {
                        _cpuTuningPanel.BringToFront();
                        _cpuTuningPanel.UpdateTelemetry();
                    }
                    break;
                case "gpu_tuning":
                    if (_gpuTuningPanel != null)
                    {
                        _gpuTuningPanel.BringToFront();
                        _gpuTuningPanel.UpdateTelemetry();
                    }
                    break;
                case "overlay_hud":
                    ShowOverlayEditor();
                    break;
            }
            return;
        }

        // 2. Hardware Navigation ("All devices" or specific HardwareNode)
        if (_fanControlPanel != null) _fanControlPanel.Visible = false;
        if (_cpuTuningPanel != null) _cpuTuningPanel.Visible = false;
        if (_gpuTuningPanel != null) _gpuTuningPanel.Visible = false;
        if (_overlayEditorPanel != null) _overlayEditorPanel.Visible = false;

        HardwareNode scopeNode = tag as HardwareNode;

        if (_toolbar != null) _toolbar.Visible = true;
        if (_summary != null)
        {
            bool isAllDevices = scopeNode == null;
            if (!_isVisualMode && isAllDevices)
            {
                if (!_summary.HasMetrics)
                    ResolveSummary();
                _summary.Visible = _summary.HasMetrics;
                if (_summary.Visible)
                    _summary.Invalidate();
            }
            else
            {
                _summary.Visible = false;
            }
        }

        if (_hardwareBentoPanel != null)
        {
            _hardwareBentoPanel.Visible = _isVisualMode;
            _hardwareBentoPanel.SetScope(scopeNode);
            if (_isVisualMode)
            {
                _hardwareBentoPanel.BringToFront();
                _hardwareBentoPanel.UpdateTelemetry(_computer);
            }
        }

        if (_treeCard != null)
        {
            _treeCard.Visible = !_isVisualMode;
            if (!_isVisualMode)
            {
                _treeCard.BringToFront();
                ApplyScope(scopeNode, true);
            }
            else
            {
                // In visual mode, persist selection without heavy tree traversal
                _settings.SetValue(ScopeSettingKey, scopeNode?.Hardware.Identifier.ToString() ?? string.Empty);
            }
        }
    }

    private void Summary_CardClicked(object sender, IHardware hardware)
    {
        HardwareNode node = _root.Nodes.OfType<HardwareNode>().FirstOrDefault(n => n.Hardware == hardware);
        if (node != null)
            _sidebar.SelectTag(node);
    }

    private void ApplyScope(HardwareNode scope, bool persist)
    {
        if (treeView.Model is not TreeModel model)
            return;

        if (model.Scope != scope)
        {
            model.Scope = scope;
            RestoreExpandedState();
        }

        if (persist)
            _settings.SetValue(ScopeSettingKey, scope?.Hardware.Identifier.ToString() ?? string.Empty);
    }

    private void Search_SearchChanged(object sender, EventArgs e)
    {
        if (treeView.Model is TreeModel model)
        {
            model.Filter = _search.Query;
            RestoreExpandedState();
        }

        _hardwareBentoPanel?.ApplyFilter(_search.Query);
    }

    /// <summary>
    /// Re-applies the expand state to nodes reloaded by a scope or filter change: everything is
    /// expanded while searching (without persisting), otherwise the persisted state is used.
    /// </summary>
    private void RestoreExpandedState()
    {
        bool searching = (treeView.Model as TreeModel)?.Filter.Length > 0;
        treeView.BeginUpdate();
        foreach (TreeNodeAdv node in treeView.AllNodes.ToList())
            node.IsExpanded = searching || node.Tag is not IExpandPersistNode persist || persist.Expanded;

        treeView.EndUpdate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F) && _search != null)
        {
            _search.FocusSearch();
            return true;
        }

        // Ctrl+1..5: overview and the four tool pages.
        int shortcut = (int)(keyData & Keys.KeyCode) - (int)Keys.D1;
        if ((keyData & Keys.Modifiers) == Keys.Control && shortcut is >= 0 and < 5 && _sidebar != null)
        {
            _sidebar.SelectIndex(shortcut);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
