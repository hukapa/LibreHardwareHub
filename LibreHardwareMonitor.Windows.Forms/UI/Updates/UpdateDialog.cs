// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Updates;

/// <summary>
/// Themed dialog for a new release: versions, the patch notes (scrollable), what will happen, and either the
/// download with a progress bar or, when the package is already on disk, the restart question.
/// </summary>
public sealed class UpdateDialog : Form
{
    private readonly UpdateReleaseInfo _release;
    private readonly FluentButton _primary;
    private readonly FluentButton _secondary;
    private readonly ProgressStrip _progress = new() { Visible = false };
    private readonly Label _status;
    private CancellationTokenSource _cancellation;
    private bool _downloading;

    /// <param name="release">The release to offer.</param>
    /// <param name="stagedExecutable">Path of the already downloaded executable, or null when it must still be downloaded.</param>
    public UpdateDialog(UpdateReleaseInfo release, string stagedExecutable)
    {
        _release = release;
        StagedExecutable = stagedExecutable;
        bool ready = stagedExecutable != null;
        Theme theme = Theme.Current;

        Text = ready ? "Atualização pronta a instalar" : "Atualização disponível";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;
        Font = FluentStyle.UiFont;
        AutoScaleMode = AutoScaleMode.None; // sizes below are already in device units
        ClientSize = new Size(LogicalToDeviceUnits(540), LogicalToDeviceUnits(400));

        int pad = LogicalToDeviceUnits(20);
        int width = ClientSize.Width - (pad * 2);
        int y = pad;

        Label title = new()
        {
            Text = ready ? "A atualização está pronta" : "Há uma nova versão do LibreHardwareHub",
            AutoSize = false,
            Font = FluentStyle.UiFontSemibold,
            Location = new Point(pad, y),
            Size = new Size(width, FluentStyle.UiFontSemibold.Height + 4),
            BackColor = theme.BackgroundColor
        };
        y += title.Height + LogicalToDeviceUnits(4);

        string published = release.PublishedAt.HasValue ? $"   ·   publicada a {release.PublishedAt.Value:dd/MM/yyyy}" : string.Empty;
        Label versions = new()
        {
            Text = $"v{AppInfo.Version}  ->  v{release.VersionText}{published}",
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(pad, y),
            Size = new Size(width, FluentStyle.UiFont.Height + 4),
            ForeColor = theme.MutedForegroundColor,
            BackColor = theme.BackgroundColor
        };
        y += versions.Height + LogicalToDeviceUnits(14);

        Label notesCaption = new()
        {
            Text = "Notas de versão",
            AutoSize = false,
            Font = FluentStyle.UiFontSmallBold,
            Location = new Point(pad, y),
            Size = new Size(width, FluentStyle.UiFontSmallBold.Height + 2),
            ForeColor = theme.MutedForegroundColor,
            BackColor = theme.BackgroundColor
        };
        y += notesCaption.Height + LogicalToDeviceUnits(4);

        ReleaseNotesView notes = new() { OnSurface = false };
        ReleaseNoteEntry entry = ReleaseNotes.FromBody("Novidades desta versão", null, release.Body);
        notes.SetEntries(new[] { entry }, "A versão não traz notas publicadas.");
        ScrollHost host = new(notes)
        {
            Location = new Point(pad, y),
            Size = new Size(width, LogicalToDeviceUnits(170))
        };
        host.ApplyTheme(theme);
        y += host.Height + LogicalToDeviceUnits(14);

        string explanation = ready
            ? $"A versão v{release.VersionText} já foi descarregada e está pronta a instalar. Deseja reiniciar o LibreHardwareHub agora para aplicar as novidades?"
            : "A atualização será descarregada e a aplicação reiniciará automaticamente sem perder as suas configurações.";
        Label what = new()
        {
            Text = explanation,
            AutoSize = false,
            Location = new Point(pad, y),
            Size = new Size(width, TextRenderer.MeasureText(explanation, Font, new Size(width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4),
            BackColor = theme.BackgroundColor
        };
        y += what.Height + LogicalToDeviceUnits(10);

        _progress.Location = new Point(pad, y);
        _progress.Size = new Size(width, LogicalToDeviceUnits(6));
        _status = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(pad, y + LogicalToDeviceUnits(10)),
            Size = new Size(width, FluentStyle.UiFont.Height + 4),
            ForeColor = theme.MutedForegroundColor,
            BackColor = theme.BackgroundColor,
            Visible = false
        };
        y += LogicalToDeviceUnits(10) + _status.Height + LogicalToDeviceUnits(10);

        _primary = PageMetrics.FitButton(new FluentButton { Text = ready ? "Reiniciar Agora" : "Descarregar e Atualizar", Variant = FluentButtonVariant.Accent });
        _secondary = PageMetrics.FitButton(new FluentButton { Text = ready ? "Depois" : "Mais Tarde" });
        _primary.Location = new Point(ClientSize.Width - pad - _primary.Width, y);
        _secondary.Location = new Point(_primary.Left - LogicalToDeviceUnits(8) - _secondary.Width, y);
        _primary.Enabled = ready || !string.IsNullOrEmpty(release.AssetUrl);
        if (!_primary.Enabled)
            ShowStatus("Esta versão não inclui um pacote portátil para descarregar.", theme.StateWarmColor);

        ClientSize = new Size(ClientSize.Width, y + _primary.Height + pad);
        Controls.AddRange(new Control[] { title, versions, notesCaption, host, what, _progress, _status, _secondary, _primary });

        _primary.Click += OnPrimaryClick;
        _secondary.Click += (_, _) => CloseDialog(DialogResult.Cancel);
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                CloseDialog(DialogResult.Cancel);
                e.Handled = true;
            }
        };
    }

    /// <summary>Path of the executable to hand over to when the dialog returned OK.</summary>
    public string StagedExecutable { get; private set; }

    private void ShowStatus(string text, Color color)
    {
        _status.Text = text;
        _status.ForeColor = color;
        _status.Visible = true;
    }

    private void CloseDialog(DialogResult result)
    {
        _cancellation?.Cancel();
        DialogResult = result;
        Close();
    }

    private async void OnPrimaryClick(object sender, EventArgs e)
    {
        if (StagedExecutable != null)
        {
            CloseDialog(DialogResult.OK);
            return;
        }

        Theme theme = Theme.Current;
        _downloading = true;
        _primary.Enabled = false;
        _secondary.Text = "Cancelar";
        PageMetrics.FitButton(_secondary);
        _progress.Value = 0;
        _progress.Visible = true;
        ShowStatus("A descarregar... 0%", theme.MutedForegroundColor);

        _cancellation = new CancellationTokenSource();
        Progress<int> progress = new(percent =>
        {
            if (IsDisposed)
                return;

            _progress.Value = percent;
            _status.Text = $"A descarregar... {percent}%";
        });

        try
        {
            StagedExecutable = await AppUpdater.DownloadUpdateAsync(_release, progress, _cancellation.Token);
            _downloading = false;
            CloseDialog(DialogResult.OK);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // The user cancelled: the dialog is closing.
            _downloading = false;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or JsonException)
        {
            // OperationCanceledException here is the HTTP timeout, not the user: it is a failure like any other.
            _downloading = false;
            if (IsDisposed)
                return;

            _progress.Visible = false;
            ShowStatus($"O descarregamento falhou: {ex.Message}", theme.StateHotColor);
            _primary.Text = "Tentar novamente";
            PageMetrics.FitButton(_primary);
            _primary.Left = ClientSize.Width - LogicalToDeviceUnits(20) - _primary.Width;
            _primary.Enabled = true;
            _secondary.Text = "Mais Tarde";
            PageMetrics.FitButton(_secondary);
            _secondary.Left = _primary.Left - LogicalToDeviceUnits(8) - _secondary.Width;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_downloading)
            _cancellation?.Cancel();

        base.OnFormClosing(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Current.ApplyTitleBar(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Thin rounded progress bar in the card style (3 px bars elsewhere, a little taller here).</summary>
    private sealed class ProgressStrip : Control
    {
        private int _value;

        public ProgressStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                if (clamped == _value)
                    return;

                _value = clamped;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme theme = Theme.Current;
            Graphics g = e.Graphics;
            g.Clear(theme.BackgroundColor);
            if (Width <= 1 || Height <= 1)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new(0, 0, Width, Height);
            FluentStyle.FillRounded(g, Color.FromArgb(30, theme.MutedForegroundColor), track, Height / 2f);
            int fill = Math.Max(Height, Width * _value / 100);
            if (_value > 0)
                FluentStyle.FillRounded(g, theme.AccentColor, new Rectangle(0, 0, Math.Min(Width, fill), Height), Height / 2f);
        }
    }
}
