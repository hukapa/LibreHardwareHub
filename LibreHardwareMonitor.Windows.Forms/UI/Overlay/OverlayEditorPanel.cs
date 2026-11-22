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
/// HUD studio page: live preview on top, then what to show (readings), how it looks (appearance and
/// severity colors) and where it sits (position). Every control is created by a small helper that binds
/// it to one <see cref="OverlayConfig" /> value, applies the change live and knows how to re-read it.
/// </summary>
public sealed partial class OverlayEditorPanel : UserControl, IThemedControl
{
    private static readonly (OverlayLayout Layout, string Text)[] Layouts =
    {
        (OverlayLayout.VerticalList, "Lista"),
        (OverlayLayout.HorizontalBar, "Barra"),
        (OverlayLayout.CompactGrid, "Grelha"),
        (OverlayLayout.FreeCanvas, "Livre")
    };

    private static readonly (OverlayThemePreset Preset, string Text)[] Presets =
    {
        (OverlayThemePreset.FluentDark, "Fluent Dark"),
        (OverlayThemePreset.MinimalistPure, "Minimalista"),
        (OverlayThemePreset.CyberMatrix, "Cyber Matrix"),
        (OverlayThemePreset.ClassicRtss, "Clássico RTSS")
    };

    private static readonly (OverlayAnchor Anchor, string Text)[] Anchors =
    {
        (OverlayAnchor.TopLeft, "Canto superior esquerdo"),
        (OverlayAnchor.TopCenter, "Centro superior"),
        (OverlayAnchor.TopRight, "Canto superior direito"),
        (OverlayAnchor.BottomLeft, "Canto inferior esquerdo"),
        (OverlayAnchor.BottomCenter, "Centro inferior"),
        (OverlayAnchor.BottomRight, "Canto inferior direito"),
        (OverlayAnchor.Custom, "Posição personalizada")
    };

    private readonly OverlayConfig _config;
    private readonly OverlayTelemetry _telemetry;
    private readonly Func<IComputer> _computer;
    private readonly Action _onConfigChanged;

    // Re-reads every bound control from the configuration (after a preset, a reset, a hotkey or a drag).
    private readonly List<Action> _sync = new();
    private readonly ToolTip _toolTip = new();
    private readonly StackPanel _page;
    private readonly ScrollHost _host;
    private readonly OverlayPreviewCanvas _preview;
    private readonly FluentBadge _statusBadge;
    private bool _syncing;

    public OverlayEditorPanel(OverlayConfig config, OverlayTelemetry telemetry, Func<IComputer> computer, Action onConfigChanged)
    {
        _config = config;
        _telemetry = telemetry;
        _computer = computer;
        _onConfigChanged = onConfigChanged;

        DoubleBuffered = true;
        Dock = DockStyle.Fill;

        _statusBadge = new FluentBadge { Height = LogicalToDeviceUnits(24), Tag = PageMetrics.AlignRight };
        _preview = new OverlayPreviewCanvas(config, telemetry);
        _preview.LayoutEdited += () => Changed(true);

        _page = new StackPanel { Dock = DockStyle.Top, Gap = PageMetrics.Gap };
        _page.Controls.Add(BuildToolbar());
        _page.Controls.Add(BuildPreviewCard());
        _page.Controls.Add(BuildMetricsCard());
        _page.Controls.Add(BuildAppearanceCard());
        _page.Controls.Add(BuildSeverityCard());
        _page.Controls.Add(BuildPositionCard());

        _host = new ScrollHost(_page) { Dock = DockStyle.Fill };
        _metricsList.ScrollOwner = _host; // dragging a reading near the top or bottom edge scrolls the page
        Controls.Add(_host);

        RebuildMetricsList();
        SyncState();
    }

    /// <summary>Re-reads the configuration into the controls; call when it was changed from outside the page.</summary>
    public void SyncState()
    {
        _syncing = true;
        foreach (Action sync in _sync)
            sync();
        foreach (Action sync in _metricSync)
            sync();
        _syncing = false;

        _preview.ConfigChanged();
    }

    /// <summary>Repaints the preview with the latest readings.</summary>
    public void RefreshPreview() => _preview.RefreshPreview();

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        _host.ApplyTheme(theme);
    }

    /// <summary>Applies a change made on this page: preview, real HUD and persistence.</summary>
    private void Changed(bool resync = false)
    {
        if (_syncing)
            return;

        if (resync)
            SyncState();
        else
            _preview.ConfigChanged();

        _onConfigChanged?.Invoke();
    }

    private Control BuildToolbar()
    {
        ToolbarRow toolbar = new();

        FluentButton toggle = new();
        toggle.Click += (_, _) =>
        {
            _config.Enabled = !_config.Enabled;
            Changed(true);
        };
        _sync.Add(() =>
        {
            toggle.Text = _config.Enabled ? "Ocultar HUD" : "Mostrar HUD";
            toggle.Variant = _config.Enabled ? FluentButtonVariant.Standard : FluentButtonVariant.Accent;
            PageMetrics.FitButton(toggle);
            _statusBadge.SetBadge(_config.Enabled ? "HUD visível" : "HUD oculto", _config.Enabled ? BadgeStyle.Success : BadgeStyle.Neutral);
        });

        toolbar.Controls.Add(toggle);
        toolbar.Controls.Add(HotkeyButton("Mostrar/ocultar", () => _config.Hotkey, k => _config.Hotkey = k));
        toolbar.Controls.Add(HotkeyButton("Bloquear/mover", () => _config.LockHotkey, k => _config.LockHotkey = k));
        toolbar.Controls.Add(_statusBadge);
        return toolbar;
    }

    private Control BuildPreviewCard()
    {
        FluentSectionCard card = new()
        {
            Title = "Pré-visualização ao vivo",
            Subtitle = "Mostra exatamente o que aparece sobre o jogo, com as leituras reais deste PC.",
            AutoHeight = true
        };

        ToolbarRow layoutRow = new() { OnSurface = true, Height = LogicalToDeviceUnits(44) };
        foreach ((OverlayLayout layout, string text) in Layouts)
        {
            FluentButton button = PageMetrics.FitButton(new FluentButton { Text = text });
            button.Click += (_, _) =>
            {
                _config.Layout = layout;
                Changed(true);
            };
            _sync.Add(() => button.IsActive = _config.Layout == layout);
            layoutRow.Controls.Add(button);
        }

        FluentButton reset = PageMetrics.FitButton(new FluentButton { Text = "Repor aspeto", Tag = PageMetrics.AlignRight });
        _toolTip.SetToolTip(reset, "Repõe disposição, cores e tamanhos. Não altera as leituras escolhidas nem os atalhos.");
        reset.Click += (_, _) =>
        {
            _config.ResetAppearance();
            Changed(true);
        };
        layoutRow.Controls.Add(reset);

        return Stack(card, _preview, layoutRow);
    }

    private Control BuildAppearanceCard()
    {
        FluentSectionCard card = new()
        {
            Title = "Aspeto",
            Subtitle = "Comece por um estilo pronto e ajuste o que quiser.",
            AutoHeight = true
        };

        ToolbarRow presets = Row();
        foreach ((OverlayThemePreset preset, string text) in Presets)
        {
            FluentButton button = PageMetrics.FitButton(new FluentButton { Text = text });
            button.Click += (_, _) =>
            {
                _config.ApplyPreset(preset);
                Changed(true);
            };
            _sync.Add(() => button.IsActive = _config.ThemePreset == preset);
            presets.Controls.Add(button);
        }

        ToolbarRow colors = Row();
        colors.Controls.Add(ColorButton("Fundo", () => _config.BackgroundColor, c => _config.BackgroundColor = c, true));
        colors.Controls.Add(ColorButton("Borda", () => _config.BorderColor, c => _config.BorderColor = c, true));
        colors.Controls.Add(ColorButton("Rótulos", () => _config.LabelColor, c => _config.LabelColor = c, true));
        colors.Controls.Add(ColorButton("Valores", () => _config.ValueColor, c => _config.ValueColor = c, true));

        ToolbarRow options = Row();
        options.Controls.Add(Toggle("Rótulos", () => _config.ShowLabels, v => _config.ShowLabels = v));
        options.Controls.Add(Toggle("Barras", () => _config.ShowProgressBars, v => _config.ShowProgressBars = v));
        options.Controls.Add(Toggle("Sombra", () => _config.ShowTextShadow, v => _config.ShowTextShadow = v));
        options.Controls.Add(Toggle("Números monoespaçados", () => _config.UseMonospaceNumbers, v => _config.UseMonospaceNumbers = v));

        return Stack(card,
            new SettingRow("Estilo", "Temas prontos a usar", presets, false) { ShowDivider = false },
            new SettingRow("Cores", "Fundo, borda, rótulos e valores", colors, false),
            new SettingRow("Elementos", "O que cada leitura mostra", options, false),
            Slider("Tamanho do texto", "De 1080p a 4K", 8, 28, "pt", () => (int)_config.FontSize, v => _config.FontSize = v),
            Slider("Opacidade do fundo", "0% deixa só o texto sobre o jogo", 0, 100, "%", () => _config.BackgroundOpacity, v => _config.BackgroundOpacity = v, true),
            Slider("Espaçamento", "Margem interior e distância entre leituras", 0, 16, "px", () => _config.Spacing, v => _config.Spacing = v),
            Slider("Cantos arredondados", "Raio dos cantos do painel", 0, 16, "px", () => _config.CornerRadius, v => _config.CornerRadius = v, true),
            Slider("Espessura da borda", "0 remove a borda", 0, 4, "px", () => _config.BorderThickness, v => _config.BorderThickness = v, true),
            Slider("Colunas da grelha", "Usado pela disposição Grelha", 2, 4, string.Empty, () => _config.GridColumns, v => _config.GridColumns = v));
    }

    private Control BuildSeverityCard()
    {
        FluentSectionCard card = new()
        {
            Title = "Cores por severidade",
            Subtitle = "O valor muda de cor conforme a temperatura ou a carga.",
            AutoHeight = true
        };

        ToolbarRow colors = Row();
        colors.Controls.Add(Toggle("Ativo", () => _config.ColorBySeverity, v => _config.ColorBySeverity = v));
        colors.Controls.Add(ColorButton("Normal", () => _config.CoolColor, c => _config.CoolColor = c, false));
        colors.Controls.Add(ColorButton("Aviso", () => _config.WarmColor, c => _config.WarmColor = c, false));
        colors.Controls.Add(ColorButton("Crítico", () => _config.HotColor, c => _config.HotColor = c, false));

        return Stack(card,
            new SettingRow("Cores", $"Cargas: aviso aos {OverlayConfig.LoadWarnPercent}%, crítico aos {OverlayConfig.LoadCritPercent}%", colors, false) { ShowDivider = false },
            Slider("Temperatura de aviso", "A partir daqui usa a cor de aviso", 40, 100, "°C", () => _config.TempWarnC, v =>
            {
                _config.TempWarnC = v;
                _config.TempCritC = Math.Max(_config.TempCritC, v);
            }, linked: true),
            Slider("Temperatura crítica", "A partir daqui usa a cor crítica", 40, 110, "°C", () => _config.TempCritC, v =>
            {
                _config.TempCritC = v;
                _config.TempWarnC = Math.Min(_config.TempWarnC, v);
            }, linked: true));
    }

    private Control BuildPositionCard()
    {
        FluentSectionCard card = new()
        {
            Title = "Posição e interação",
            Subtitle = "Onde o HUD fica no ecrã e se reage ao rato.",
            AutoHeight = true
        };

        FluentButton clickThrough = new();
        clickThrough.Click += (_, _) =>
        {
            _config.ClickThrough = !_config.ClickThrough;
            Changed(true);
        };
        _sync.Add(() =>
        {
            clickThrough.Text = _config.ClickThrough ? "Bloqueado: os cliques passam para o jogo" : "Desbloqueado: arraste o HUD no ecrã";
            clickThrough.IsActive = !_config.ClickThrough;
        });

        FluentComboBox anchor = new();
        foreach ((OverlayAnchor _, string text) in Anchors)
            anchor.Items.Add(text);
        anchor.SelectedIndexChanged += (_, _) =>
        {
            if (_syncing || anchor.SelectedIndex < 0)
                return;

            _config.Anchor = Anchors[anchor.SelectedIndex].Anchor;
            Changed();
        };
        _sync.Add(() => anchor.SelectedIndex = Array.FindIndex(Anchors, a => a.Anchor == _config.Anchor));

        FluentComboBox screen = new();
        screen.Items.Add("Ecrã principal");
        Screen[] screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
            screen.Items.Add($"Ecrã {i + 1} ({screens[i].Bounds.Width} x {screens[i].Bounds.Height})");
        screen.SelectedIndexChanged += (_, _) =>
        {
            if (_syncing || screen.SelectedIndex < 0)
                return;

            _config.ScreenIndex = screen.SelectedIndex - 1;
            Changed();
        };
        _sync.Add(() => screen.SelectedIndex = Math.Clamp(_config.ScreenIndex + 1, 0, screen.Items.Count - 1));

        // Drag mode is the exception: while the HUD is unlocked a quiet warning reminds the player to lock it.
        ToolbarRow dragWarning = Row();
        dragWarning.Controls.Add(new FluentBadge { Height = LogicalToDeviceUnits(24) });
        _sync.Add(() =>
        {
            ((FluentBadge)dragWarning.Controls[0]).SetBadge("Modo Arrasto Ativo - Bloqueie antes de jogar", BadgeStyle.Warning);
            dragWarning.Visible = !_config.ClickThrough;
        });

        NoteLabel note = new("Nota de jogos: para o HUD aparecer sobre jogos como League of Legends, selecione o modo Sem Bordas (Borderless) nas definições de vídeo do jogo. Zero injeção de DLLs (seguro com anti-cheats).");

        return Stack(card,
            new SettingRow("Interação com o rato", "Alterna também com o atalho Bloquear/mover", clickThrough, false) { ShowDivider = false },
            dragWarning,
            new SettingRow("Âncora", "Canto do ecrã onde o HUD fica preso", anchor, false),
            new SettingRow("Ecrã", "Monitor usado pelas âncoras fixas", screen, false),
            Slider("Margem ao ecrã", "Distância às margens nas âncoras fixas", 0, 200, "px", () => _config.Margin, v => _config.Margin = v),
            note);
    }

    private ToolbarRow Row() => new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };

    private static Control Stack(FluentSectionCard card, params Control[] rows)
    {
        StackPanel stack = new() { Dock = DockStyle.Top, Gap = 0, OnSurface = true };
        stack.Controls.AddRange(rows);
        card.Controls.Add(stack);
        return card;
    }

    private FluentButton Toggle(string text, Func<bool> get, Action<bool> set, int heightLogical = 32, List<Action> sync = null)
    {
        FluentButton button = PageMetrics.FitButton(new FluentButton { Text = text }, heightLogical);
        button.Click += (_, _) =>
        {
            set(!get());
            button.IsActive = get();
            Changed();
        };
        (sync ?? _sync).Add(() => button.IsActive = get());
        return button;
    }

    private FluentColorButton ColorButton(string text, Func<Color> get, Action<Color> set, bool partOfPreset)
    {
        FluentColorButton button = new(text);
        button.ValueChanged += (_, _) =>
        {
            set(button.EffectiveColor);
            Changed(partOfPreset && MarkCustomPreset());
        };
        _sync.Add(() => button.Value = get());
        return button;
    }

    private SettingRow Slider(string title, string description, int min, int max, string unit, Func<int> get, Action<int> set, bool partOfPreset = false, bool linked = false)
    {
        FluentSlider slider = new() { Minimum = min, Maximum = max, SmallChange = 1, LargeChange = Math.Max(1, (max - min) / 10) };
        SettingRow row = new(title, description, slider);
        slider.Scroll += (_, _) =>
        {
            row.SetValue($"{slider.Value} {unit}".TrimEnd());
            if (_syncing)
                return;

            set(slider.Value);
            // Linked sliders (warning/critical temperature) may have moved each other.
            bool presetLeft = partOfPreset && MarkCustomPreset();
            Changed(presetLeft || linked || get() != slider.Value);
        };
        _sync.Add(() =>
        {
            slider.Value = get();
            row.SetValue($"{slider.Value} {unit}".TrimEnd());
        });
        return row;
    }

    /// <summary>A manual tweak of a preset-controlled value means no preset is selected any more; true when that just happened.</summary>
    private bool MarkCustomPreset()
    {
        if (_config.ThemePreset == OverlayThemePreset.Custom)
            return false;

        _config.ThemePreset = OverlayThemePreset.Custom;
        return true;
    }

    private FluentButton HotkeyButton(string caption, Func<Keys> get, Action<Keys> set)
    {
        FluentButton button = new();
        bool recording = false;

        void Refresh()
        {
            button.Text = recording ? "Prima a combinação (Esc cancela)" : $"{caption}: {GlobalHotkeyManager.FormatKeyCombo(get())}";
            button.IsActive = recording;
            PageMetrics.FitButton(button);
        }

        button.Click += (_, _) =>
        {
            recording = true;
            button.Focus();
            Refresh();
        };
        button.LostFocus += (_, _) =>
        {
            recording = false;
            Refresh();
        };
        button.PreviewKeyDown += (_, e) => e.IsInputKey = recording;
        button.KeyDown += (_, e) =>
        {
            if (!recording)
                return;

            e.SuppressKeyPress = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.None)
                return;

            recording = false;
            if (e.KeyCode != Keys.Escape)
            {
                set((e.Control ? Keys.Control : Keys.None) | (e.Alt ? Keys.Alt : Keys.None) | (e.Shift ? Keys.Shift : Keys.None) | e.KeyCode);
                Changed(true);
            }

            Refresh();
        };

        _sync.Add(Refresh);
        return button;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _sensorMenu?.Dispose();
        }

        base.Dispose(disposing);
    }
}
