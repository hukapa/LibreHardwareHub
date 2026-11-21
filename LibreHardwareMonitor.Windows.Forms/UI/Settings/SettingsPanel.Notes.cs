// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Updates;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Settings;

/// <summary>Patch notes card: the embedded changelog at once, replaced by the GitHub Releases when they can be read.</summary>
public sealed partial class SettingsPanel
{
    private const int HistoryCount = 8;

    private readonly CancellationTokenSource _notesCancellation = new();
    private FluentSectionCard _notesCard;
    private ReleaseNotesView _notes;

    private Control BuildNotesCard()
    {
        _notesCard = new FluentSectionCard
        {
            Title = "Notas de versão",
            Subtitle = "Fonte: registo embutido na aplicação",
            AutoHeight = true
        };

        _notes = new ReleaseNotesView { Dock = DockStyle.Top };
        _notes.SetEntries(ReleaseNotes.LoadEmbedded());
        _notesCard.Controls.Add(_notes);
        return _notesCard;
    }

    private async void LoadRemoteNotes()
    {
        try
        {
            List<UpdateReleaseInfo> releases = await AppUpdater.GetHistoryAsync(HistoryCount, _notesCancellation.Token);
            if (IsDisposed || releases.Count == 0)
                return;

            List<ReleaseNoteEntry> embedded = ReleaseNotes.LoadEmbedded();
            List<ReleaseNoteEntry> entries = new(releases.Count);
            foreach (UpdateReleaseInfo release in releases)
            {
                ReleaseNoteEntry entry = ReleaseNotes.FromBody(release.Tag, release.PublishedAt, release.Body);

                // A release published without notes borrows the ones of the embedded changelog, when it has them.
                if (entry.Lines.Count == 0)
                {
                    foreach (ReleaseNoteEntry local in embedded)
                    {
                        if (AppUpdater.ParseVersion(local.Version) == release.Version)
                        {
                            entry.Lines.AddRange(local.Lines);
                            break;
                        }
                    }
                }

                entries.Add(entry);
            }

            _notes.SetEntries(entries);
            _notesCard.Subtitle = "Fonte: GitHub Releases";
            RelayoutNotes();
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException or IOException or InvalidOperationException)
        {
            if (!IsDisposed)
                _notesCard.Subtitle = "Fonte: registo embutido (sem ligação ao GitHub)";
        }
    }

    private void RelayoutNotes()
    {
        _notesCard.PerformLayout();
        _page.PerformLayout();
        _host.PerformLayout();
    }

    private void DisposeNotes()
    {
        _notesCancellation.Cancel();
        _notesCancellation.Dispose();
    }
}
