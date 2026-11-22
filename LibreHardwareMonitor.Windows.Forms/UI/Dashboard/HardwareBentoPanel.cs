// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Dashboard;

/// <summary>
/// Responsive Bento card container presenting hardware telemetry in intelligent,
/// high-density cards using a responsive WrapGrid and thin Fluent scrollbar.
/// </summary>
public sealed class HardwareBentoPanel : Panel, IThemedControl
{
    private readonly CpuBentoCard _cpuCard;
    private readonly GpuBentoCard _gpuCard;
    private readonly MemoryBentoCard _memoryCard;
    private readonly SystemBentoCard _systemCard;
    private readonly List<StorageBentoCard> _storageCards = new();

    private readonly WrapGrid _grid;
    private readonly ScrollHost _host;

    private HardwareNode _currentScope;
    private Computer _computer;
    private bool _scoping;
    private string _filterQuery = string.Empty;

    public HardwareBentoPanel()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;

        _grid = new WrapGrid
        {
            Dock = DockStyle.Top,
            MinColumnWidth = 380,
            Gap = PageMetrics.Gap,
            Padding = new Padding(0, 0, 0, 16)
        };

        _cpuCard = AddCard(new CpuBentoCard());
        _gpuCard = AddCard(new GpuBentoCard());
        _memoryCard = AddCard(new MemoryBentoCard());
        _systemCard = AddCard(new SystemBentoCard());

        _host = new ScrollHost(_grid) { Dock = DockStyle.Fill };
        Controls.Add(_host);
    }

    // A card that was just expanded collects its detailed sensors right away instead of waiting for the next tick.
    private T AddCard<T>(T card) where T : BentoCardBase
    {
        card.ExpansionChanged += delegate
        {
            if (card.IsExpanded && !_scoping)
                UpdateTelemetry(_computer);
        };
        _grid.Controls.Add(card);
        return card;
    }

    /// <summary>
    /// Shows every card collapsed ("All devices") or only the card of one device, expanded with all its sensors.
    /// </summary>
    public void SetScope(HardwareNode scope)
    {
        _currentScope = scope;
        ApplyFiltering(true);
    }

    public void ApplyFilter(string query)
    {
        _filterQuery = query ?? string.Empty;
        ApplyFiltering(false);
    }

    private void ApplyFiltering(bool scopeChanged)
    {
        IHardware target = _currentScope?.Hardware;
        HardwareType? type = target?.HardwareType;
        bool all = target == null;

        _scoping = true;
        _grid.SuspendLayout();

        Show(_cpuCard, all || type == HardwareType.Cpu, "cpu processador intel amd ryzen core");
        Show(_gpuCard, all || (type.HasValue && SensorLookup.IsGpu(type.Value)), "gpu placa grafica nvidia geforce rtx gtx radeon video vram");
        Show(_memoryCard, all || type == HardwareType.Memory, "ram memoria memory dimm ddr4 ddr5 swap");
        Show(_systemCard, all || type is HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.Network, "motherboard placa mae fan ventilacao superio rede network ethernet wifi vcore");
        foreach (StorageBentoCard card in _storageCards)
            Show(card, all || (type == HardwareType.Storage && (card.Hardware == target || card.Title == target.Name)), "disco storage ssd nvme hdd smart");

        _scoping = false;
        _grid.ResumeLayout(false);
        UpdateTelemetry(_computer);
        _grid.PerformLayout();

        void Show(BentoCardBase card, bool inScope, string keywords)
        {
            bool visible = inScope && MatchesCard(card, keywords);
            card.Visible = visible;
            // A single device is always shown with its details; going back to the overview collapses everything.
            if (!all)
                card.IsExpanded = visible;
            else if (scopeChanged)
                card.IsExpanded = false;
        }
    }

    private bool MatchesCard(BentoCardBase card, string extraKeywords)
    {
        if (string.IsNullOrWhiteSpace(_filterQuery))
            return true;

        string query = _filterQuery.Trim();
        if (card.Title?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (card.Subtitle?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (!string.IsNullOrEmpty(extraKeywords) && extraKeywords.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    public void UpdateTelemetry(Computer computer)
    {
        _computer = computer ?? _computer;
        if (!Visible || _computer == null)
            return;

        IList<IHardware> hardware = _computer.Hardware;
        if (_cpuCard.Visible)
            _cpuCard.UpdateSensors(SensorLookup.First(hardware, HardwareType.Cpu));
        if (_gpuCard.Visible)
            _gpuCard.UpdateSensors(SensorLookup.PrimaryGpu(hardware));
        if (_memoryCard.Visible)
            _memoryCard.UpdateSensors(hardware);
        if (_systemCard.Visible)
            _systemCard.UpdateSensors(hardware);

        // One card per drive, created the first time the drive shows up.
        int drives = 0;
        bool added = false;
        foreach (IHardware hw in hardware)
        {
            if (hw.HardwareType != HardwareType.Storage)
                continue;

            if (drives == _storageCards.Count)
            {
                StorageBentoCard card = AddCard(new StorageBentoCard());
                card.ApplyTheme(Theme.Current);
                _storageCards.Add(card);
                added = true;
            }

            _storageCards[drives++].UpdateSensors(hw);
        }

        if (added)
            ApplyFiltering(false);
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        _grid.ApplyTheme(theme);
        _host.ApplyTheme(theme);

        foreach (Control c in _grid.Controls)
        {
            if (c is IThemedControl tc)
                tc.ApplyTheme(theme);
        }
    }
}
