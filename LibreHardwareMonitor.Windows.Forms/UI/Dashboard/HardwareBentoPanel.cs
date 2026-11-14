// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        _cpuCard = new CpuBentoCard();
        _gpuCard = new GpuBentoCard();
        _memoryCard = new MemoryBentoCard();
        _systemCard = new SystemBentoCard();

        _grid.Controls.Add(_cpuCard);
        _grid.Controls.Add(_gpuCard);
        _grid.Controls.Add(_memoryCard);
        _grid.Controls.Add(_systemCard);

        _host = new ScrollHost(_grid) { Dock = DockStyle.Fill };
        Controls.Add(_host);
    }

    public void SetScope(HardwareNode scope)
    {
        _currentScope = scope;
        ApplyFiltering();
    }

    public void ApplyFilter(string query)
    {
        _filterQuery = query ?? string.Empty;
        ApplyFiltering();
    }

    private void ApplyFiltering()
    {
        _grid.SuspendLayout();

        IHardware targetHw = _currentScope?.Hardware;
        HardwareType? targetType = targetHw?.HardwareType;

        bool showAll = targetHw == null;

        _cpuCard.Visible = (showAll || targetType == HardwareType.Cpu) && MatchesCard(_cpuCard, "cpu processador intel amd ryzen core");
        _gpuCard.Visible = (showAll || targetType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel) && MatchesCard(_gpuCard, "gpu placa grafica nvidia geforce rtx gtx radeon video vram");
        _memoryCard.Visible = (showAll || targetType == HardwareType.Memory) && MatchesCard(_memoryCard, "ram memoria memory dimm ddr4 ddr5 swap");
        _systemCard.Visible = (showAll || targetType is HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.Network) && MatchesCard(_systemCard, "motherboard placa mae fan ventilacao superio rede network ethernet wifi vcore");

        foreach (StorageBentoCard storageCard in _storageCards)
        {
            bool matchHardware = showAll || (targetType == HardwareType.Storage && (storageCard.Hardware == targetHw || (targetHw != null && storageCard.Title == targetHw.Name)));
            storageCard.Visible = matchHardware && MatchesCard(storageCard, "disco storage ssd nvme hdd smart");
        }

        _grid.ResumeLayout(true);
        _grid.PerformLayout();
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
        if (!Visible || computer == null)
            return;

        IHardware cpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu != null && _cpuCard.Visible)
            _cpuCard.UpdateSensors(cpu);

        IHardware gpu = computer.Hardware.FirstOrDefault(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                        ?? computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel);
        if (gpu != null && _gpuCard.Visible)
            _gpuCard.UpdateSensors(gpu);

        IHardware mem = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory && !h.Name.Contains("Virtual"))
                        ?? computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
        if (mem != null && _memoryCard.Visible)
            _memoryCard.UpdateSensors(mem);

        if (_systemCard.Visible)
            _systemCard.UpdateSensors(computer.Hardware);

        // Synchronize storage cards
        var storages = computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage).ToList();
        bool newStorageAdded = false;

        while (_storageCards.Count < storages.Count)
        {
            StorageBentoCard sc = new();
            sc.ApplyTheme(Theme.Current);
            _storageCards.Add(sc);
            _grid.Controls.Add(sc);
            newStorageAdded = true;
        }

        for (int i = 0; i < storages.Count; i++)
        {
            _storageCards[i].UpdateSensors(storages[i]);
        }

        if (newStorageAdded)
        {
            ApplyFiltering();
        }
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
