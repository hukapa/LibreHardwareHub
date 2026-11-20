// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;
using StorageDeviceLhm = LibreHardwareMonitor.Hardware.Storage.StorageDevice;

namespace LibreHardwareMonitor.Windows.Forms.UI.DiskHealth;

/// <summary>
/// "Saúde dos discos" tool page. Read-only: it interprets the S.M.A.R.T. data the background poller already
/// collects, so it costs nothing while hidden and never issues commands to the drives itself.
/// </summary>
public sealed class DiskHealthPanel : UserControl, IThemedControl
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly Func<IEnumerable<IHardware>> _hardware;
    private readonly StackPanel _page;
    private readonly ScrollHost _host;
    private readonly ToolbarRow _toolbar;
    private readonly Label _summaryLabel;
    private readonly FluentBadge _overallBadge;
    private readonly StackPanel _cards;
    private readonly FluentSectionCard _emptyCard;
    private readonly Label _emptyLabel;
    private readonly Label _footnote;
    private readonly Dictionary<string, DiskHealthCard> _cardsById = new();
    private DateTime _lastRefresh = DateTime.MinValue;

    public DiskHealthPanel(Func<IEnumerable<IHardware>> hardware)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Dock = DockStyle.Fill;
        _hardware = hardware;

        _toolbar = new ToolbarRow();
        _summaryLabel = new Label { AutoSize = false, Height = FluentStyle.UiFont.Height + 4, Font = FluentStyle.UiFontSemibold, AutoEllipsis = true, Tag = PageMetrics.Fill, Text = "A ler os discos…" };
        _overallBadge = new FluentBadge { Height = LogicalToDeviceUnits(24), Tag = PageMetrics.AlignRight, Visible = false };
        _toolbar.Controls.Add(_summaryLabel);
        _toolbar.Controls.Add(_overallBadge);

        _cards = new StackPanel { Dock = DockStyle.Top };

        _emptyCard = new FluentSectionCard { Title = "Sem discos para mostrar", AutoHeight = true, Visible = false };
        _emptyLabel = new Label { Dock = DockStyle.Top, AutoSize = false, Height = (FluentStyle.UiFont.Height * 2) + 8, Font = FluentStyle.UiFont };
        _emptyCard.Controls.Add(_emptyLabel);

        _footnote = new Label
        {
            AutoSize = false,
            Height = FluentStyle.UiFontSmall.Height + 4,
            Font = FluentStyle.UiFontSmall,
            AutoEllipsis = true,
            Text = "Leitura dos dados SMART que o próprio disco guarda. Não faz testes nem escreve no disco, e não acorda discos em repouso."
        };

        _page = new StackPanel { Dock = DockStyle.Top };
        _page.Controls.Add(_toolbar);
        _page.Controls.Add(_emptyCard);
        _page.Controls.Add(_cards);
        _page.Controls.Add(_footnote);
        _host = new ScrollHost(_page) { Dock = DockStyle.Fill };
        Controls.Add(_host);

        ApplyTheme(Theme.Current);
    }

    /// <summary>Re-evaluates the drives; throttled unless <paramref name="force" /> is set (page just opened).</summary>
    public void UpdateTelemetry(bool force = false)
    {
        if (!Visible && !force)
            return;

        DateTime now = DateTime.UtcNow;
        if (!force && now - _lastRefresh < RefreshInterval)
            return;

        _lastRefresh = now;

        List<(string Id, DiskHealthReport Report)> reports = new();
        TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        foreach (StorageDeviceLhm device in (_hardware?.Invoke() ?? Enumerable.Empty<IHardware>()).OfType<StorageDeviceLhm>())
        {
            try
            {
                reports.Add((device.Identifier.ToString(), DiskHealthEvaluator.Evaluate(device.Storage, device.BytesWrittenSinceBoot, uptime)));
            }
            catch (Exception)
            {
                // A drive that disappears or changes mid-read is simply shown again on the next refresh.
            }
        }

        // Same order as Disk Management: Disco 0, 1, 2…
        reports.Sort((a, b) => (a.Report.DiskNumber ?? uint.MaxValue).CompareTo(b.Report.DiskNumber ?? uint.MaxValue));
        ApplyReports(reports);
    }

    private void ApplyReports(List<(string Id, DiskHealthReport Report)> reports)
    {
        SuspendLayout();
        _cards.SuspendLayout();

        HashSet<string> seen = new();
        for (int i = 0; i < reports.Count; i++)
        {
            (string id, DiskHealthReport report) = reports[i];
            seen.Add(id);
            if (!_cardsById.TryGetValue(id, out DiskHealthCard card))
            {
                card = new DiskHealthCard();
                card.ApplyTheme(Theme.Current);
                _cardsById[id] = card;
                _cards.Controls.Add(card);
            }

            _cards.Controls.SetChildIndex(card, i);
            card.SetReport(report);
        }

        foreach (string stale in _cardsById.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            _cards.Controls.Remove(_cardsById[stale]);
            _cardsById[stale].Dispose();
            _cardsById.Remove(stale);
        }

        UpdateSummary(reports.Select(r => r.Report).ToList());

        _cards.ResumeLayout(true);
        ResumeLayout(true);
        _page.PerformLayout();
        _host.PerformLayout();
    }

    private void UpdateSummary(List<DiskHealthReport> reports)
    {
        bool empty = reports.Count == 0;
        _emptyCard.Visible = empty;
        _cards.Visible = !empty;
        _overallBadge.Visible = !empty;

        if (empty)
        {
            _summaryLabel.Text = "Nenhum disco encontrado";
            _emptyLabel.Text = "Confirme que a leitura de discos está ativa (File > Hardware > Storage Devices) e que a aplicação corre como administrador.";
            return;
        }

        int bad = reports.Count(r => r.Level == DiskHealthLevel.Bad);
        int caution = reports.Count(r => r.Level == DiskHealthLevel.Caution);
        int unknown = reports.Count(r => r.Level == DiskHealthLevel.Unknown);
        string count = reports.Count == 1 ? "1 disco" : $"{reports.Count} discos";

        if (bad > 0)
        {
            _summaryLabel.Text = $"{count} · {Plural(bad, "precisa", "precisam")} de atenção imediata";
            _overallBadge.SetBadge("Faça cópia de segurança", BadgeStyle.Danger);
        }
        else if (caution > 0)
        {
            _summaryLabel.Text = $"{count} · {Plural(caution, "a vigiar", "a vigiar")}";
            _overallBadge.SetBadge("A vigiar", BadgeStyle.Warning);
        }
        else if (unknown == reports.Count)
        {
            _summaryLabel.Text = $"{count} · sem dados SMART disponíveis";
            _overallBadge.SetBadge("Sem dados", BadgeStyle.Neutral);
        }
        else
        {
            _summaryLabel.Text = unknown > 0
                ? $"{count} · os restantes estão em bom estado ({unknown} sem dados SMART)"
                : $"{count} · todos em bom estado";
            _overallBadge.SetBadge("Tudo bem", BadgeStyle.Success);
        }
    }

    private static string Plural(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;
        theme.Apply(_host);
        _summaryLabel.ForeColor = theme.ForegroundColor;
        _emptyLabel.ForeColor = theme.MutedForegroundColor;
        _emptyLabel.BackColor = theme.SurfaceColor;
        _footnote.ForeColor = theme.MutedForegroundColor;
        _footnote.BackColor = theme.BackgroundColor;
        foreach (DiskHealthCard card in _cardsById.Values)
            card.ApplyTheme(theme);
    }
}
