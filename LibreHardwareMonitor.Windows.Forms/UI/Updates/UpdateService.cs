// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Updates;

/// <summary>
/// Update flow shared by the startup check and the Definicoes page: one GitHub request per check, an optional silent
/// download, and the dialog that asks the user to restart. Nothing runs between checks.
/// </summary>
public sealed class UpdateService : IDisposable
{
    public const string CheckOnStartupKey = "updates.checkOnStartup";
    public const string AutoDownloadKey = "updates.autoDownload";

    private readonly PersistentSettings _settings;
    private readonly Action _requestExit;
    private readonly CancellationTokenSource _cancellation = new();
    private bool _pendingPrompt;

    /// <param name="settings">Where the two update options are persisted.</param>
    /// <param name="requestExit">Closes the application cleanly (fans handed back, settings saved) once an update is applied.</param>
    public UpdateService(PersistentSettings settings, Action requestExit)
    {
        _settings = settings;
        _requestExit = requestExit;
    }

    /// <summary>Raised on the UI thread whenever <see cref="StatusText" /> or <see cref="Latest" /> changed.</summary>
    public event EventHandler StatusChanged;

    /// <summary>The newest release when it is newer than this build; null otherwise.</summary>
    public UpdateReleaseInfo Latest { get; private set; }

    public string StatusText { get; private set; } = "Ainda não foi feita nenhuma verificação nesta sessão.";

    public bool IsChecking { get; private set; }

    public bool CheckOnStartup
    {
        get => _settings.GetValue(CheckOnStartupKey, true);
        set => _settings.SetValue(CheckOnStartupKey, value);
    }

    public bool AutoDownload
    {
        get => _settings.GetValue(AutoDownloadKey, false);
        set => _settings.SetValue(AutoDownloadKey, value);
    }

    private static bool IsExpected(Exception e) =>
        e is HttpRequestException or OperationCanceledException or JsonException or IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException;

    private void SetStatus(string text)
    {
        StatusText = text;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Asks GitHub for the latest release; true when a newer one exists (see <see cref="Latest" />).</summary>
    public async Task<bool> CheckAsync()
    {
        if (IsChecking)
            return Latest != null;

        IsChecking = true;
        SetStatus("A procurar atualizações...");
        try
        {
            UpdateReleaseInfo release = await AppUpdater.GetLatestAsync(_cancellation.Token);
            string time = DateTime.Now.ToString("HH:mm");
            if (AppUpdater.IsNewer(release))
            {
                Latest = release;
                SetStatus($"Nova versão disponível: v{release.VersionText} (verificado às {time}).");
            }
            else
            {
                Latest = null;
                SetStatus($"Sem atualizações disponíveis. Versão instalada: v{AppInfo.Version} (verificado às {time}).");
            }
        }
        catch (Exception e) when (IsExpected(e))
        {
            Latest = null;
            SetStatus("Não foi possível procurar atualizações (sem ligação à Internet ou GitHub indisponível).");
        }
        finally
        {
            IsChecking = false;
        }

        return Latest != null;
    }

    /// <summary>
    /// Startup check, called on the UI thread a few seconds after the window opened (the caller owns that delay, so
    /// the continuations of this method always resume on the UI thread): asks GitHub once, optionally downloads in
    /// silence and then offers the restart. Does nothing when the option is off or when there is no news.
    /// </summary>
    public async Task RunStartupCheckAsync(Form owner)
    {
        if (!CheckOnStartup)
            return;

        if (!await CheckAsync())
            return;

        if (AutoDownload)
        {
            try
            {
                await AppUpdater.DownloadUpdateAsync(Latest, null, _cancellation.Token);
                SetStatus($"A versão v{Latest.VersionText} já foi descarregada e está pronta a instalar.");
            }
            catch (Exception e) when (IsExpected(e))
            {
                SetStatus("Nova versão disponível, mas o descarregamento automático falhou.");
            }
        }

        _pendingPrompt = true;
        ShowPendingPrompt(owner);
    }

    /// <summary>Shows the update dialog once, as soon as the main window is on screen (it may start hidden in the tray).</summary>
    public void ShowPendingPrompt(Form owner)
    {
        if (!_pendingPrompt || Latest == null || owner == null || owner.IsDisposed || !owner.Visible || owner.WindowState == FormWindowState.Minimized)
            return;

        _pendingPrompt = false;
        ShowDialog(owner);
    }

    /// <summary>Opens the dialog for <see cref="Latest" /> and, if the user accepts, hands over to the new executable.</summary>
    public void ShowDialog(IWin32Window owner)
    {
        UpdateReleaseInfo release = Latest;
        if (release == null)
            return;

        string staged = AppUpdater.TryGetDownloaded(release, out string existing) ? existing : null;
        using UpdateDialog dialog = new(release, staged);
        if (dialog.ShowDialog(owner) != DialogResult.OK || dialog.StagedExecutable == null)
            return;

        if (UpdateApplier.LaunchApply(dialog.StagedExecutable))
        {
            _requestExit();
        }
        else
        {
            MessageBox.Show("Não foi possível iniciar a atualização. Tente novamente mais tarde.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
    }
}
