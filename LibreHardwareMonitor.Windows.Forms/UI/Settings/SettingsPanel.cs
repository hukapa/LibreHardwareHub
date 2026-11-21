// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;
using LibreHardwareMonitor.Windows.Forms.UI.Updates;
using LibreHardwareMonitor.Windows.Forms.Utilities;

namespace LibreHardwareMonitor.Windows.Forms.UI.Settings;

/// <summary>
/// "Definicoes" tool page: system and startup options, automatic updates and the patch notes. Built on first use
/// (nothing is allocated until the page is opened) and bound to the same options the menu uses.
/// </summary>
public sealed partial class SettingsPanel : UserControl, IThemedControl
{
    private static readonly (string Id, string Text)[] Themes =
    {
        ("dark", "Escuro"),
        ("black", "Preto OLED"),
        ("light", "Claro")
    };

    private readonly SettingsContext _context;

    // Re-reads every control from the options (page shown again, option changed from the menu).
    private readonly List<Action> _sync = new();
    private readonly StackPanel _page;
    private readonly ScrollHost _host;
    private readonly NoteLabel _statusNote;
    private readonly FluentButton _checkButton;
    private readonly FluentButton _viewButton;
    private bool _syncing;

    public SettingsPanel(SettingsContext context)
    {
        _context = context;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Dock = DockStyle.Fill;

        _statusNote = new NoteLabel(context.Updates.StatusText);
        _checkButton = PageMetrics.FitButton(new FluentButton { Text = "Procurar Atualizações Agora", Variant = FluentButtonVariant.Accent });
        _viewButton = PageMetrics.FitButton(new FluentButton { Text = "Ver atualização" });

        _page = new StackPanel { Dock = DockStyle.Top, Gap = PageMetrics.Gap };
        _page.Controls.Add(BuildSystemCard());
        _page.Controls.Add(BuildUpdatesCard());
        _page.Controls.Add(BuildNotesCard());

        _host = new ScrollHost(_page) { Dock = DockStyle.Fill };
        Controls.Add(_host);

        context.Updates.StatusChanged += OnUpdateStatusChanged;
        ApplyTheme(Theme.Current);
        SyncState();
        LoadRemoteNotes();
    }

    /// <summary>Re-reads the options into the controls.</summary>
    public void SyncState()
    {
        _syncing = true;
        foreach (Action sync in _sync)
            sync();

        _syncing = false;
        UpdateUpdateControls();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        _host.ApplyTheme(theme);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
            SyncState();
    }

    private void OnUpdateStatusChanged(object sender, EventArgs e)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
            BeginInvoke(new Action(UpdateUpdateControls));
        else
            UpdateUpdateControls();
    }

    private void UpdateUpdateControls()
    {
        UpdateService updates = _context.Updates;
        _statusNote.Text = updates.StatusText;
        _checkButton.Enabled = !updates.IsChecking;
        _viewButton.Visible = updates.Latest != null;
        if (updates.Latest != null)
        {
            _viewButton.Text = $"Ver v{updates.Latest.VersionText}";
            PageMetrics.FitButton(_viewButton);
        }

        _viewButton.Parent?.PerformLayout();
        _page.PerformLayout();
    }

    private Control BuildSystemCard()
    {
        FluentSectionCard card = new()
        {
            Title = "Sistema e arranque",
            Subtitle = "Como o LibreHardwareHub arranca, fecha e se apresenta.",
            AutoHeight = true
        };

        SettingRow startup = new("Iniciar com o Windows", _context.StartupAvailable ? "Arranque automático sem pedido de administrador (UAC)" : "Indisponível neste sistema",
            Toggle(() => _context.Startup.Value, v => _context.Startup.Value = v, _context.StartupAvailable), false) { ShowDivider = false };

        FluentComboBox theme = new();
        foreach ((string _, string text) in Themes)
            theme.Items.Add(text);
        theme.SelectedIndexChanged += (_, _) =>
        {
            if (!_syncing && theme.SelectedIndex >= 0)
                _context.SetTheme(Themes[theme.SelectedIndex].Id);
        };
        _sync.Add(() => theme.SelectedIndex = Math.Max(0, Array.FindIndex(Themes, t => t.Id == Theme.Current.Id)));

        FluentComboBox unit = new();
        unit.Items.Add("Celsius (°C)");
        unit.Items.Add("Fahrenheit (°F)");
        unit.SelectedIndexChanged += (_, _) =>
        {
            if (!_syncing && unit.SelectedIndex >= 0)
                _context.SetUnit((TemperatureUnit)unit.SelectedIndex);
        };
        _sync.Add(() => unit.SelectedIndex = (int)_context.GetUnit());

        return Stack(card,
            startup,
            new SettingRow("Iniciar minimizado na bandeja", "A janela não abre ao arrancar", Toggle(() => _context.StartMinimized.Value, v => _context.StartMinimized.Value = v), false),
            new SettingRow("Minimizar para a bandeja ao fechar", "O botão X esconde a janela em vez de terminar", Toggle(() => _context.MinimizeOnClose.Value, v => _context.MinimizeOnClose.Value = v), false),
            new SettingRow("Tema da interface", "Aplica-se de imediato", theme, false),
            new SettingRow("Unidade de temperatura", "Janela, bandeja e gráficos", unit, false));
    }

    private Control BuildUpdatesCard()
    {
        FluentSectionCard card = new()
        {
            Title = "Atualizações",
            Subtitle = "Uma única consulta às Releases do GitHub, sem serviços em segundo plano.",
            AutoHeight = true
        };

        SettingRow installed = new("Versão instalada", AppInfo.IsPortable ? "Modo portátil" : "Modo instalado", null);
        installed.SetValue($"v{AppInfo.Version}");

        _checkButton.Click += OnCheckNow;
        _viewButton.Click += (_, _) => _context.Updates.ShowDialog(FindForm());
        _viewButton.Visible = false;
        ToolbarRow buttons = new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };
        buttons.Controls.Add(_checkButton);
        buttons.Controls.Add(_viewButton);

        return Stack(card,
            installed,
            new SettingRow("Procurar atualizações no arranque", "Seis segundos depois de abrir, uma única vez",
                Toggle(() => _context.Updates.CheckOnStartup, v => _context.Updates.CheckOnStartup = v), false),
            new SettingRow("Descarregar atualizações automaticamente", "Descarrega em segundo plano e pergunta se quer reiniciar",
                Toggle(() => _context.Updates.AutoDownload, v => _context.Updates.AutoDownload = v), false),
            new SettingRow("Procurar agora", "Consulta o GitHub imediatamente", buttons, false),
            _statusNote);
    }

    private async void OnCheckNow(object sender, EventArgs e)
    {
        UpdateService updates = _context.Updates;
        _checkButton.Enabled = false;
        bool found = await updates.CheckAsync();
        if (IsDisposed)
            return;

        UpdateUpdateControls();
        if (found)
            updates.ShowDialog(FindForm());
    }

    // On/off switch: an accent button that reads "Ativado" or "Desativado", wrapped in a row so the setting row does not stretch it.
    private ToolbarRow Toggle(Func<bool> get, Action<bool> set, bool enabled = true)
    {
        FluentButton button = PageMetrics.FitButton(new FluentButton { Text = "Desativado", Enabled = enabled });
        void Refresh()
        {
            bool on = get();
            button.Text = on ? "Ativado" : "Desativado";
            button.IsActive = on;
        }

        button.Click += (_, _) =>
        {
            set(!get());
            Refresh();
        };
        _sync.Add(Refresh);

        ToolbarRow row = new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };
        row.Controls.Add(button);
        return row;
    }

    private static Control Stack(FluentSectionCard card, params Control[] rows)
    {
        StackPanel stack = new() { Dock = DockStyle.Top, Gap = 0, OnSurface = true };
        stack.Controls.AddRange(rows);
        card.Controls.Add(stack);
        return card;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context.Updates.StatusChanged -= OnUpdateStatusChanged;
            DisposeNotes();
        }

        base.Dispose(disposing);
    }
}
