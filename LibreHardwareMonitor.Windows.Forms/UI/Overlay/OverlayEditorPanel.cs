// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// Fluent design studio panel for configuring the in-game HUD overlay,
/// keybinds, metrics selection, presets and live preview.
/// Built strictly with the design system components (ScrollHost, StackPanel, FluentSectionCard, SettingRow).
/// </summary>
public sealed class OverlayEditorPanel : UserControl, IThemedControl
{
    private readonly OverlayConfig _config;
    private OverlayForm _overlayForm;
    private readonly Action _onConfigChanged;

    private readonly StackPanel _page;
    private readonly ScrollHost _host;

    // Top Toolbar
    private readonly ToolbarRow _toolbar;
    private readonly FluentButton _btnToggleOverlay;
    private readonly FluentButton _btnRecordHotkey;
    private readonly FluentBadge _statusBadge;

    // Card 1: Preview
    private readonly FluentSectionCard _previewCard;
    private readonly PreviewCanvas _previewCanvas;

    // Card 2: Appearance & Geometry
    private readonly FluentSectionCard _appearanceCard;
    private readonly FluentButton _btnDark;
    private readonly FluentButton _btnMinimal;
    private readonly FluentButton _btnMatrix;
    private readonly FluentButton _btnRtss;
    private readonly FluentButton _btnClickThrough;
    private readonly FluentComboBox _cmbAnchor;
    private readonly FluentComboBox _cmbLayout;
    private readonly FluentSlider _sliderOpacity;
    private readonly FluentSlider _sliderFontSize;
    private readonly FluentSlider _sliderCornerRadius;
    private readonly SettingRow _rowOpacity;
    private readonly SettingRow _rowFontSize;
    private readonly SettingRow _rowCornerRadius;
    private readonly FluentButton _btnSeverity;
    private readonly FluentButton _btnBars;
    private readonly FluentButton _btnShadow;
    private readonly FluentButton _btnMono;

    // Card 3: Metrics
    private readonly FluentSectionCard _metricsCard;
    private readonly StackPanel _metricsList;

    private bool _recordingHotkey;

    public OverlayEditorPanel(OverlayConfig config, OverlayForm overlayForm, Action onConfigChanged)
    {
        _config = config;
        _overlayForm = overlayForm;
        _onConfigChanged = onConfigChanged;

        DoubleBuffered = true;
        Dock = DockStyle.Fill;

        // Top Toolbar
        _toolbar = new ToolbarRow();
        _btnToggleOverlay = PageMetrics.FitButton(new FluentButton
        {
            Text = _config.Enabled ? "Ocultar HUD Agora" : "Mostrar HUD Agora",
            Variant = _config.Enabled ? FluentButtonVariant.Accent : FluentButtonVariant.Standard
        });
        _btnToggleOverlay.Click += (_, _) =>
        {
            _config.Enabled = !_config.Enabled;
            UpdateUiState();
            NotifyChange();
        };

        _btnRecordHotkey = PageMetrics.FitButton(new FluentButton
        {
            Text = $"Atalho: {GlobalHotkeyManager.FormatKeyCombo(_config.Hotkey)}"
        });
        _btnRecordHotkey.Click += (_, _) =>
        {
            _recordingHotkey = true;
            _btnRecordHotkey.Text = "Prima uma tecla (ex: Alt + R)...";
            _btnRecordHotkey.Focus();
        };
        _btnRecordHotkey.KeyDown += OnRecordHotkeyKeyDown;

        _statusBadge = new FluentBadge { Height = LogicalToDeviceUnits(24), Tag = PageMetrics.AlignRight };

        _toolbar.Controls.Add(_btnToggleOverlay);
        _toolbar.Controls.Add(_btnRecordHotkey);
        _toolbar.Controls.Add(_statusBadge);

        // Card 1: Preview Canvas with Alignment Controls
        _previewCard = new FluentSectionCard
        {
            Title = "Estúdio HUD e Pré-visualização Interativa",
            Subtitle = "Arraste os sensores diretamente no painel com o rato (grelha magnética de 8px) ou use os alinhamentos rápidos.",
            AutoHeight = true
        };

        ToolbarRow alignRow = new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };
        FluentButton btnAlignVert = PageMetrics.FitButton(new FluentButton { Text = "Empilhar na Vertical" });
        btnAlignVert.Click += (_, _) =>
        {
            _config.ApplyAutoLayout(OverlayLayout.VerticalList);
            _cmbLayout.SelectedIndex = (int)OverlayLayout.VerticalList;
            NotifyChange();
        };

        FluentButton btnAlignHoriz = PageMetrics.FitButton(new FluentButton { Text = "Fila Horizontal" });
        btnAlignHoriz.Click += (_, _) =>
        {
            _config.ApplyAutoLayout(OverlayLayout.HorizontalBar);
            _cmbLayout.SelectedIndex = (int)OverlayLayout.HorizontalBar;
            NotifyChange();
        };

        FluentButton btnAlignGrid = PageMetrics.FitButton(new FluentButton { Text = "Grelha 2 Colunas" });
        btnAlignGrid.Click += (_, _) =>
        {
            _config.ApplyAutoLayout(OverlayLayout.CompactGrid);
            _cmbLayout.SelectedIndex = (int)OverlayLayout.CompactGrid;
            NotifyChange();
        };

        alignRow.Controls.Add(btnAlignVert);
        alignRow.Controls.Add(btnAlignHoriz);
        alignRow.Controls.Add(btnAlignGrid);
        _previewCard.Controls.Add(alignRow);

        _previewCanvas = new PreviewCanvas(_config, () =>
        {
            if (_cmbLayout.SelectedIndex != (int)OverlayLayout.FreeCanvas)
                _cmbLayout.SelectedIndex = (int)OverlayLayout.FreeCanvas;
            NotifyChange();
        })
        {
            Dock = DockStyle.Top,
            Height = LogicalToDeviceUnits(220)
        };
        _previewCard.Controls.Add(_previewCanvas);

        // Card 2: Appearance & Geometry
        _appearanceCard = new FluentSectionCard
        {
            Title = "Aspeto Visual e Geometria",
            Subtitle = "Ajuste o posicionamento, transparência e tipografia do HUD.",
            AutoHeight = true
        };

        ToolbarRow presetsRow = new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };
        _btnDark = PageMetrics.FitButton(new FluentButton { Text = "Fluent Dark" });
        _btnDark.Click += (_, _) => ApplyThemePreset(OverlayThemePreset.FluentDark);
        _btnMinimal = PageMetrics.FitButton(new FluentButton { Text = "Minimalista" });
        _btnMinimal.Click += (_, _) => ApplyThemePreset(OverlayThemePreset.MinimalistPure);
        _btnMatrix = PageMetrics.FitButton(new FluentButton { Text = "Cyber Matrix" });
        _btnMatrix.Click += (_, _) => ApplyThemePreset(OverlayThemePreset.CyberMatrix);
        _btnRtss = PageMetrics.FitButton(new FluentButton { Text = "Clássico RTSS" });
        _btnRtss.Click += (_, _) => ApplyThemePreset(OverlayThemePreset.ClassicRtss);
        presetsRow.Controls.Add(_btnDark);
        presetsRow.Controls.Add(_btnMinimal);
        presetsRow.Controls.Add(_btnMatrix);
        presetsRow.Controls.Add(_btnRtss);

        _btnClickThrough = PageMetrics.FitButton(new FluentButton
        {
            Text = _config.ClickThrough ? "Ativo (Cliques passam para o jogo)" : "Inativo (Arrastável com o rato)",
            Variant = _config.ClickThrough ? FluentButtonVariant.Accent : FluentButtonVariant.Standard
        });
        _btnClickThrough.Click += (_, _) =>
        {
            _config.ClickThrough = !_config.ClickThrough;
            _btnClickThrough.Text = _config.ClickThrough ? "Ativo (Cliques passam para o jogo)" : "Inativo (Arrastável com o rato)";
            _btnClickThrough.Variant = _config.ClickThrough ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;
            PageMetrics.FitButton(_btnClickThrough);
            _overlayForm?.ApplyClickThrough();
            NotifyChange();
        };

        _cmbAnchor = new FluentComboBox();
        _cmbAnchor.Items.AddRange(new object[]
        {
            "Canto Sup. Esquerdo",
            "Centro Superior",
            "Canto Sup. Direito",
            "Canto Inf. Esquerdo",
            "Canto Inf. Direito",
            "Posição Personalizada"
        });
        _cmbAnchor.SelectedIndex = (int)_config.Anchor;
        _cmbAnchor.SelectedIndexChanged += (_, _) =>
        {
            _config.Anchor = (OverlayAnchor)_cmbAnchor.SelectedIndex;
            _overlayForm?.UpdatePosition();
            NotifyChange();
        };

        _cmbLayout = new FluentComboBox();
        _cmbLayout.Items.AddRange(new object[]
        {
            "Lista Vertical",
            "Barra Horizontal",
            "Grelha Compacta (2 Colunas)",
            "Livre / Drag-and-Drop (Free Canvas)"
        });
        _cmbLayout.SelectedIndex = Math.Min((int)_config.Layout, 3);
        _cmbLayout.SelectedIndexChanged += (_, _) =>
        {
            _config.Layout = (OverlayLayout)_cmbLayout.SelectedIndex;
            _config.ApplyAutoLayout(_config.Layout);
            NotifyChange();
        };

        _sliderOpacity = new FluentSlider { Minimum = 0, Maximum = 100, Value = _config.BackgroundOpacity, SmallChange = 1, LargeChange = 5 };
        _sliderOpacity.Scroll += (_, _) =>
        {
            _config.BackgroundOpacity = _sliderOpacity.Value;
            UpdateSlidersText();
            NotifyChange();
        };

        _sliderFontSize = new FluentSlider { Minimum = 8, Maximum = 20, Value = (int)_config.FontSize, SmallChange = 1, LargeChange = 2 };
        _sliderFontSize.Scroll += (_, _) =>
        {
            _config.FontSize = _sliderFontSize.Value;
            _overlayForm?.UpdateFonts();
            UpdateSlidersText();
            NotifyChange();
        };

        _sliderCornerRadius = new FluentSlider { Minimum = 0, Maximum = 16, Value = _config.CornerRadius, SmallChange = 1, LargeChange = 2 };
        _sliderCornerRadius.Scroll += (_, _) =>
        {
            _config.CornerRadius = _sliderCornerRadius.Value;
            UpdateSlidersText();
            NotifyChange();
        };

        _rowOpacity = new SettingRow("Opacidade do Fundo", "Nível de transparência sobre o jogo", _sliderOpacity);
        _rowFontSize = new SettingRow("Tamanho do Texto", "Escala da tipografia para resolução 1080p a 4K", _sliderFontSize);
        _rowCornerRadius = new SettingRow("Raio dos Cantos", "Arredondamento das bordas do painel", _sliderCornerRadius);

        ToolbarRow colorsRow = new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };
        FluentButton btnColorBg = PageMetrics.FitButton(new FluentButton { Text = "Cor de Fundo" });
        btnColorBg.Click += (_, _) => PickColor("Cor de Fundo", _config.BackgroundColor, c => _config.BackgroundColor = c);
        FluentButton btnColorBorder = PageMetrics.FitButton(new FluentButton { Text = "Cor da Borda" });
        btnColorBorder.Click += (_, _) => PickColor("Cor da Borda", _config.BorderColor, c => _config.BorderColor = c);
        FluentButton btnColorLabel = PageMetrics.FitButton(new FluentButton { Text = "Cor dos Rótulos" });
        btnColorLabel.Click += (_, _) => PickColor("Cor dos Rótulos", _config.LabelColor, c => _config.LabelColor = c);
        FluentButton btnColorVal = PageMetrics.FitButton(new FluentButton { Text = "Cor dos Valores" });
        btnColorVal.Click += (_, _) => PickColor("Cor dos Valores", _config.ValueColor, c => _config.ValueColor = c);
        colorsRow.Controls.Add(btnColorBg);
        colorsRow.Controls.Add(btnColorBorder);
        colorsRow.Controls.Add(btnColorLabel);
        colorsRow.Controls.Add(btnColorVal);

        ToolbarRow thermalColorsRow = new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };
        FluentButton btnColorCool = PageMetrics.FitButton(new FluentButton { Text = "Cor Fria" });
        btnColorCool.Click += (_, _) => PickColor("Cor Normal / Fria", _config.CoolColor, c => _config.CoolColor = c);
        FluentButton btnColorWarm = PageMetrics.FitButton(new FluentButton { Text = "Cor Quente" });
        btnColorWarm.Click += (_, _) => PickColor("Cor Alerta / Quente", _config.WarmColor, c => _config.WarmColor = c);
        FluentButton btnColorHot = PageMetrics.FitButton(new FluentButton { Text = "Cor Crítica" });
        btnColorHot.Click += (_, _) => PickColor("Cor Crítica", _config.HotColor, c => _config.HotColor = c);
        thermalColorsRow.Controls.Add(btnColorCool);
        thermalColorsRow.Controls.Add(btnColorWarm);
        thermalColorsRow.Controls.Add(btnColorHot);

        ToolbarRow optionsRow = new() { OnSurface = true, Height = LogicalToDeviceUnits(32) };
        _btnSeverity = PageMetrics.FitButton(new FluentButton { Text = "Cores por Severidade", IsActive = _config.ColorBySeverity });
        _btnSeverity.Click += (_, _) =>
        {
            _config.ColorBySeverity = !_config.ColorBySeverity;
            _btnSeverity.IsActive = _config.ColorBySeverity;
            NotifyChange();
        };

        _btnBars = PageMetrics.FitButton(new FluentButton { Text = "Barras de Carga", IsActive = _config.ShowProgressBars });
        _btnBars.Click += (_, _) =>
        {
            _config.ShowProgressBars = !_config.ShowProgressBars;
            _btnBars.IsActive = _config.ShowProgressBars;
            NotifyChange();
        };

        _btnShadow = PageMetrics.FitButton(new FluentButton { Text = "Sombra de Texto", IsActive = _config.ShowTextShadow });
        _btnShadow.Click += (_, _) =>
        {
            _config.ShowTextShadow = !_config.ShowTextShadow;
            _btnShadow.IsActive = _config.ShowTextShadow;
            NotifyChange();
        };

        _btnMono = PageMetrics.FitButton(new FluentButton { Text = "Números Monoespaçados", IsActive = _config.UseMonospaceNumbers });
        _btnMono.Click += (_, _) =>
        {
            _config.UseMonospaceNumbers = !_config.UseMonospaceNumbers;
            _btnMono.IsActive = _config.UseMonospaceNumbers;
            _overlayForm?.UpdateFonts();
            NotifyChange();
        };

        optionsRow.Controls.Add(_btnSeverity);
        optionsRow.Controls.Add(_btnBars);
        optionsRow.Controls.Add(_btnShadow);
        optionsRow.Controls.Add(_btnMono);

        StackPanel appearanceRows = new() { Dock = DockStyle.Top, Gap = 0, OnSurface = true };
        appearanceRows.Controls.Add(new SettingRow("Predefinições de Estilo", "Temas pré-configurados", presetsRow, false) { ShowDivider = false });
        appearanceRows.Controls.Add(new SettingRow("Modo Click-Through", "Cliques do rato passam diretamente para o jogo (Zero Input Lag)", _btnClickThrough, false));
        appearanceRows.Controls.Add(new SettingRow("Âncora de Ecrã", "Posição fixa relativa ao monitor ativo", _cmbAnchor, false));
        appearanceRows.Controls.Add(new SettingRow("Orientação / Layout", "Disposição das leituras no HUD", _cmbLayout, false));
        appearanceRows.Controls.Add(_rowOpacity);
        appearanceRows.Controls.Add(_rowFontSize);
        appearanceRows.Controls.Add(_rowCornerRadius);
        appearanceRows.Controls.Add(new SettingRow("Cores Gerais", "Fundo, bordas, rótulos e valores base", colorsRow, false));
        appearanceRows.Controls.Add(new SettingRow("Cores Térmicas", "Paleta dinâmica de temperaturas e cargas", thermalColorsRow, false));
        appearanceRows.Controls.Add(new SettingRow("Opções de Renderização", "Aprimoramentos visuais de leitura rápida", optionsRow, false));

        _appearanceCard.Controls.Add(appearanceRows);

        // Card 3: Metrics List
        _metricsCard = new FluentSectionCard
        {
            Title = "Métricas e Sensores Ativos",
            Subtitle = "Ative, renomeie e ordene as leituras exibidas na sobreposição.",
            AutoHeight = true
        };

        _metricsList = new StackPanel { Dock = DockStyle.Top, Gap = 6, OnSurface = true };
        _metricsCard.Controls.Add(_metricsList);

        // Assemble Page
        _page = new StackPanel { Dock = DockStyle.Top, Gap = PageMetrics.Gap };
        _page.Controls.Add(_toolbar);
        _page.Controls.Add(_previewCard);
        _page.Controls.Add(_appearanceCard);
        _page.Controls.Add(_metricsCard);

        _host = new ScrollHost(_page) { Dock = DockStyle.Fill };
        Controls.Add(_host);

        RebuildMetricsList();
        UpdateUiState();
        UpdateSlidersText();
    }

    public void SetOverlayForm(OverlayForm form)
    {
        _overlayForm = form;
    }

    private void OnRecordHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recordingHotkey)
            return;

        e.SuppressKeyPress = true;

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.None)
            return;

        Keys combo = Keys.None;
        if (e.Control) combo |= Keys.Control;
        if (e.Alt) combo |= Keys.Alt;
        if (e.Shift) combo |= Keys.Shift;
        combo |= e.KeyCode;

        _config.Hotkey = combo;
        _recordingHotkey = false;
        _btnRecordHotkey.Text = $"Atalho: {GlobalHotkeyManager.FormatKeyCombo(_config.Hotkey)}";
        PageMetrics.FitButton(_btnRecordHotkey);

        NotifyChange();
    }

    private void ApplyThemePreset(OverlayThemePreset preset)
    {
        _config.ApplyPreset(preset);

        _sliderOpacity.Value = _config.BackgroundOpacity;
        _sliderCornerRadius.Value = _config.CornerRadius;
        _btnShadow.IsActive = _config.ShowTextShadow;

        UpdateSlidersText();
        NotifyChange();
    }

    private void PickColor(string title, Color current, Action<Color> onSelected)
    {
        using ColorDialog dlg = new() { Color = current, FullOpen = true };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            onSelected(dlg.Color);
            NotifyChange();
        }
    }

    private void UpdateSlidersText()
    {
        _rowOpacity.SetValue($"{_sliderOpacity.Value}%");
        _rowFontSize.SetValue($"{_sliderFontSize.Value} pt");
        _rowCornerRadius.SetValue($"{_sliderCornerRadius.Value} px");
    }

    private void UpdateUiState()
    {
        _btnToggleOverlay.Text = _config.Enabled ? "Ocultar HUD Agora" : "Mostrar HUD Agora";
        _btnToggleOverlay.Variant = _config.Enabled ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;
        PageMetrics.FitButton(_btnToggleOverlay);

        if (_config.Enabled)
            _statusBadge.SetBadge("HUD Ativo · Visível", BadgeStyle.Success);
        else
            _statusBadge.SetBadge("HUD em Pausa", BadgeStyle.Neutral);

        if (_overlayForm != null)
            _overlayForm.Visible = _config.Enabled;
    }

    private void RebuildMetricsList()
    {
        _metricsList.SuspendLayout();
        _metricsList.Controls.Clear();

        var sorted = _config.MetricItems.OrderBy(m => m.Order).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            OverlayMetricItem item = sorted[i];
            int index = i;

            ToolbarRow row = new()
            {
                OnSurface = true,
                Height = LogicalToDeviceUnits(36)
            };

            FluentButton btnActive = PageMetrics.FitButton(new FluentButton
            {
                Text = item.Enabled ? "Visível" : "Oculto",
                Variant = item.Enabled ? FluentButtonVariant.Accent : FluentButtonVariant.Standard
            }, 28);
            btnActive.Click += (_, _) =>
            {
                item.Enabled = !item.Enabled;
                btnActive.Text = item.Enabled ? "Visível" : "Oculto";
                btnActive.Variant = item.Enabled ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;
                NotifyChange();
            };

            Label lblName = new()
            {
                Text = item.DefaultTitle,
                Font = FluentStyle.UiFontSemibold,
                Width = LogicalToDeviceUnits(110),
                Height = LogicalToDeviceUnits(28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            FluentTextBox txtCustom = new()
            {
                Text = item.DisplayName,
                Width = LogicalToDeviceUnits(140),
                Height = LogicalToDeviceUnits(28)
            };
            txtCustom.TextChanged += (_, _) =>
            {
                item.CustomTitle = txtCustom.Text;
                NotifyChange();
            };

            FluentButton btnColorLbl = PageMetrics.FitButton(new FluentButton { Text = "Rótulo" }, 28);
            btnColorLbl.Click += (_, _) =>
            {
                PickColor($"Cor do Rótulo ({item.DisplayName})", item.CustomLabelColor ?? _config.LabelColor, c => item.CustomLabelColor = c);
            };

            FluentButton btnColorVal = PageMetrics.FitButton(new FluentButton { Text = "Valor" }, 28);
            btnColorVal.Click += (_, _) =>
            {
                PickColor($"Cor do Valor ({item.DisplayName})", item.CustomValueColor ?? _config.ValueColor, c => item.CustomValueColor = c);
            };

            FluentButton btnDynamic = PageMetrics.FitButton(new FluentButton
            {
                Text = "Térmico",
                Variant = item.UseDynamicColor ? FluentButtonVariant.Accent : FluentButtonVariant.Standard
            }, 28);
            btnDynamic.Click += (_, _) =>
            {
                item.UseDynamicColor = !item.UseDynamicColor;
                btnDynamic.Variant = item.UseDynamicColor ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;
                NotifyChange();
            };

            FluentButton btnBar = PageMetrics.FitButton(new FluentButton
            {
                Text = "Barra",
                Variant = item.ShowBar ? FluentButtonVariant.Accent : FluentButtonVariant.Standard
            }, 28);
            btnBar.Click += (_, _) =>
            {
                item.ShowBar = !item.ShowBar;
                btnBar.Variant = item.ShowBar ? FluentButtonVariant.Accent : FluentButtonVariant.Standard;
                NotifyChange();
            };

            row.Controls.Add(btnActive);
            row.Controls.Add(lblName);
            row.Controls.Add(txtCustom);
            row.Controls.Add(btnColorLbl);
            row.Controls.Add(btnColorVal);
            row.Controls.Add(btnDynamic);
            row.Controls.Add(btnBar);

            if (item.Id == "ram_used")
            {
                string fmtLabel = item.DisplayFormat switch
                {
                    1 => "[Só GB]",
                    2 => "[Usado / Total]",
                    _ => "[GB + %]"
                };
                FluentButton btnFmt = PageMetrics.FitButton(new FluentButton { Text = fmtLabel }, 28);
                btnFmt.Click += (_, _) =>
                {
                    item.DisplayFormat = (item.DisplayFormat + 1) % 3;
                    btnFmt.Text = item.DisplayFormat switch
                    {
                        1 => "[Só GB]",
                        2 => "[Usado / Total]",
                        _ => "[GB + %]"
                    };
                    PageMetrics.FitButton(btnFmt, 28);
                    NotifyChange();
                };
                row.Controls.Add(btnFmt);
            }

            FluentButton btnUp = new()
            {
                Text = "[^]",
                Width = LogicalToDeviceUnits(32),
                Height = LogicalToDeviceUnits(28),
                Enabled = index > 0
            };
            btnUp.Click += (_, _) =>
            {
                if (index > 0)
                {
                    (sorted[index].Order, sorted[index - 1].Order) = (sorted[index - 1].Order, sorted[index].Order);
                    RebuildMetricsList();
                    NotifyChange();
                }
            };

            FluentButton btnDown = new()
            {
                Text = "[v]",
                Width = LogicalToDeviceUnits(32),
                Height = LogicalToDeviceUnits(28),
                Enabled = index < sorted.Count - 1
            };
            btnDown.Click += (_, _) =>
            {
                if (index < sorted.Count - 1)
                {
                    (sorted[index].Order, sorted[index + 1].Order) = (sorted[index + 1].Order, sorted[index].Order);
                    RebuildMetricsList();
                    NotifyChange();
                }
            };

            row.Controls.Add(btnUp);
            row.Controls.Add(btnDown);

            _metricsList.Controls.Add(row);
        }

        _metricsList.ResumeLayout(true);
        _metricsCard.PerformLayout();
    }

    private void NotifyChange()
    {
        _previewCanvas.Invalidate();
        if (_overlayForm != null && _overlayForm.Visible)
            _overlayForm.RenderLayeredOverlay();

        _onConfigChanged?.Invoke();
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        _page.ApplyTheme(theme);
        _host.ApplyTheme(theme);
    }

    /// <summary>
    /// Custom interactive canvas rendering live HUD simulation with drag-and-drop metric positioning.
    /// </summary>
    private sealed class PreviewCanvas : Control
    {
        private readonly OverlayConfig _config;
        private readonly Action _onDragged;

        private OverlayMetricItem _draggedItem;
        private Point _dragOffset;
        private bool _isDragging;

        public PreviewCanvas(OverlayConfig config, Action onDragged)
        {
            _config = config;
            _onDragged = onDragged;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            var active = _config.MetricItems.Where(m => m.Enabled).OrderBy(m => m.Order).ToList();
            int itemH = LogicalToDeviceUnits(22);
            int padding = LogicalToDeviceUnits(12);

            for (int i = active.Count - 1; i >= 0; i--)
            {
                OverlayMetricItem item = active[i];
                Rectangle itemRect;
                if (_config.Layout == OverlayLayout.FreeCanvas)
                {
                    itemRect = new Rectangle(item.GridX, item.GridY, LogicalToDeviceUnits(180), itemH);
                }
                else if (_config.Layout == OverlayLayout.HorizontalBar)
                {
                    int itemW = LogicalToDeviceUnits(140);
                    itemRect = new Rectangle(padding + (i * (itemW + padding)), padding, itemW, itemH);
                }
                else if (_config.Layout == OverlayLayout.CompactGrid)
                {
                    int col = i % 2;
                    int row = i / 2;
                    int itemW = LogicalToDeviceUnits(160);
                    itemRect = new Rectangle(padding + (col * (itemW + padding)), padding + (row * (itemH + 4)), itemW, itemH);
                }
                else
                {
                    itemRect = new Rectangle(padding, padding + (i * (itemH + 4)), LogicalToDeviceUnits(220), itemH);
                }

                if (itemRect.Contains(e.Location))
                {
                    _draggedItem = item;
                    _dragOffset = new Point(e.X - itemRect.X, e.Y - itemRect.Y);
                    _isDragging = true;
                    Capture = true;
                    Cursor = Cursors.SizeAll;
                    Invalidate();
                    break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging && _draggedItem != null)
            {
                int snapX = Math.Max(12, ((e.X - _dragOffset.X + 4) / 8) * 8);
                int snapY = Math.Max(12, ((e.Y - _dragOffset.Y + 4) / 8) * 8);

                _draggedItem.GridX = snapX;
                _draggedItem.GridY = snapY;
                _config.Layout = OverlayLayout.FreeCanvas;

                _onDragged?.Invoke();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                _draggedItem = null;
                Capture = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Theme theme = Theme.Current;
            Rectangle bounds = ClientRectangle;

            // Draw game canvas simulation backdrop
            using (SolidBrush meshBg = new(Color.FromArgb(16, 18, 22)))
                g.FillRectangle(meshBg, bounds);

            // Subtle grid mesh lines representing a running game
            using (Pen meshPen = new(Color.FromArgb(28, 32, 40), 1f))
            {
                for (int x = 0; x < bounds.Width; x += 32)
                    g.DrawLine(meshPen, x, 0, x, bounds.Height);
                for (int y = 0; y < bounds.Height; y += 32)
                    g.DrawLine(meshPen, 0, y, bounds.Width, y);
            }

            var activeItems = _config.MetricItems.Where(m => m.Enabled).OrderBy(m => m.Order).ToList();
            if (activeItems.Count == 0)
            {
                TextRenderer.DrawText(g, "Nenhuma métrica ativada no HUD.", FluentStyle.UiFont, bounds, theme.MutedForegroundColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            int itemH = LogicalToDeviceUnits(22);
            int pad = LogicalToDeviceUnits(12);

            // Compute HUD container bounds
            Rectangle hudRect;
            if (_config.Layout == OverlayLayout.FreeCanvas)
            {
                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = 0, maxY = 0;
                foreach (OverlayMetricItem m in activeItems)
                {
                    minX = Math.Min(minX, m.GridX);
                    minY = Math.Min(minY, m.GridY);
                    maxX = Math.Max(maxX, m.GridX + LogicalToDeviceUnits(180));
                    maxY = Math.Max(maxY, m.GridY + itemH);
                }
                int hx = Math.Max(pad, minX - pad / 2);
                int hy = Math.Max(pad, minY - pad / 2);
                int hw = Math.Min(bounds.Width - hx - pad, Math.Max(120, (maxX - minX) + pad));
                int hh = Math.Min(bounds.Height - hy - pad, Math.Max(30, (maxY - minY) + pad));
                hudRect = new Rectangle(hx, hy, hw, hh);
            }
            else if (_config.Layout == OverlayLayout.HorizontalBar)
            {
                int itemW = LogicalToDeviceUnits(140);
                int hw = (activeItems.Count * (itemW + pad)) + pad;
                hudRect = new Rectangle(pad, pad, Math.Min(bounds.Width - (pad * 2), hw), itemH + (pad * 2));
            }
            else if (_config.Layout == OverlayLayout.CompactGrid)
            {
                int itemW = LogicalToDeviceUnits(160);
                int rows = (int)Math.Ceiling(activeItems.Count / 2.0);
                hudRect = new Rectangle(pad, pad, (itemW * 2) + (pad * 3), (rows * (itemH + 4)) + (pad * 2));
            }
            else // VerticalList
            {
                int hw = LogicalToDeviceUnits(240);
                int hh = (activeItems.Count * (itemH + 4)) + (pad * 2);
                hudRect = new Rectangle(pad, pad, hw, Math.Min(bounds.Height - (pad * 2), hh));
            }

            int alpha = (int)(_config.BackgroundOpacity / 100f * 255);
            Color hudBg = Color.FromArgb(alpha, _config.BackgroundColor);

            using (GraphicsPath path = FluentStyle.RoundedRectangle(hudRect, _config.CornerRadius))
            {
                if (alpha > 0)
                {
                    using SolidBrush bBrush = new(hudBg);
                    g.FillPath(bBrush, path);
                }

                if (_config.BorderThickness > 0 && _config.BorderColor != Color.Transparent)
                {
                    using Pen bPen = new(_config.BorderColor, _config.BorderThickness);
                    g.DrawPath(bPen, path);
                }
            }

            // Magnetic alignment guidelines during drag
            if (_isDragging && _draggedItem != null)
            {
                using Pen guidePen = new(Color.FromArgb(160, theme.AccentColor), 1f) { DashStyle = DashStyle.Dash };
                g.DrawLine(guidePen, 0, _draggedItem.GridY, bounds.Width, _draggedItem.GridY);
                g.DrawLine(guidePen, _draggedItem.GridX, 0, _draggedItem.GridX, bounds.Height);
            }

            Font lblFont = FluentStyle.CreateUiFont(FontStyle.Regular, _config.FontSize);
            Font valFont = _config.UseMonospaceNumbers
                ? FluentStyle.CreateMonoFont(_config.FontSize, FontStyle.Bold)
                : FluentStyle.CreateUiFont(FontStyle.Bold, _config.FontSize);

            using SolidBrush shadowBrush = new(Color.FromArgb(180, Color.Black));

            for (int i = 0; i < activeItems.Count; i++)
            {
                OverlayMetricItem item = activeItems[i];
                string val = GetMockValue(item);

                Rectangle itemRect;
                if (_config.Layout == OverlayLayout.FreeCanvas)
                {
                    itemRect = new Rectangle(item.GridX, item.GridY, LogicalToDeviceUnits(180), itemH);
                }
                else if (_config.Layout == OverlayLayout.HorizontalBar)
                {
                    int itemW = LogicalToDeviceUnits(140);
                    itemRect = new Rectangle(pad + (i * (itemW + pad)), pad + pad / 2, itemW, itemH);
                }
                else if (_config.Layout == OverlayLayout.CompactGrid)
                {
                    int col = i % 2;
                    int row = i / 2;
                    int itemW = LogicalToDeviceUnits(160);
                    itemRect = new Rectangle(pad + (col * (itemW + pad)), pad + (row * (itemH + 4)), itemW, itemH);
                }
                else
                {
                    itemRect = new Rectangle(pad + 4, pad + (i * (itemH + 4)), hudRect.Width - 8, itemH);
                }

                if (itemRect.Bottom > bounds.Bottom)
                    break;

                Color lblColor = item.CustomLabelColor ?? _config.LabelColor;
                Color valColor = item.CustomValueColor ?? _config.ValueColor;
                if (item.UseDynamicColor && _config.ColorBySeverity)
                {
                    if (item.Id.Contains("temp"))
                        valColor = _config.WarmColor;
                    else if (item.Id.Contains("load"))
                        valColor = _config.CoolColor;
                }

                using SolidBrush lblBrush = new(lblColor);
                using SolidBrush valBrush = new(valColor);

                SizeF lblSize = g.MeasureString(item.DisplayName, lblFont, PointF.Empty, FluentStyle.FormatNearCenter);
                SizeF valSize = g.MeasureString(val, valFont, PointF.Empty, FluentStyle.FormatFarCenter);

                int lw = (int)Math.Ceiling(lblSize.Width);
                int vw = (int)Math.Ceiling(valSize.Width);

                Rectangle lr = new(itemRect.X, itemRect.Y, Math.Min(itemRect.Width, lw + 4), itemRect.Height);
                Rectangle vr = new(itemRect.Right - vw - 2, itemRect.Y, vw + 2, itemRect.Height);

                if (_config.ShowTextShadow)
                {
                    Rectangle ls = new(lr.X + 1, lr.Y + 1, lr.Width, lr.Height);
                    Rectangle vs = new(vr.X + 1, vr.Y + 1, vr.Width, vr.Height);
                    g.DrawString(item.DisplayName, lblFont, shadowBrush, ls, FluentStyle.FormatNearCenter);
                    g.DrawString(val, valFont, shadowBrush, vs, FluentStyle.FormatFarCenter);
                }

                g.DrawString(item.DisplayName, lblFont, lblBrush, lr, FluentStyle.FormatNearCenter);
                g.DrawString(val, valFont, valBrush, vr, FluentStyle.FormatFarCenter);

                if (_config.ShowProgressBars && item.ShowBar)
                {
                    int barH = Math.Max(2, (int)(_config.FontSize * 0.2f));
                    int barY = lr.Bottom - barH;
                    using SolidBrush barBg = new(Color.FromArgb(50, lblColor));
                    using SolidBrush barFill = new(valColor);
                    g.FillRectangle(barBg, itemRect.X, barY, itemRect.Width, barH);
                    g.FillRectangle(barFill, itemRect.X, barY, itemRect.Width * 0.60f, barH);
                }

                if (_draggedItem == item)
                {
                    using Pen selPen = new(theme.AccentColor, 1.5f);
                    g.DrawRectangle(selPen, itemRect.X - 2, itemRect.Y - 2, itemRect.Width + 4, itemRect.Height + 4);
                }
            }

            lblFont.Dispose();
            valFont.Dispose();
        }

        private static string GetMockValue(OverlayMetricItem item)
        {
            return item.Id switch
            {
                "cpu_temp" => "62 °C",
                "cpu_load" => "48 %",
                "cpu_power" => "85 W",
                "cpu_clock" => "5200 MHz",
                "gpu_temp" => "65 °C",
                "gpu_load" => "72 %",
                "gpu_power" => "175 W",
                "gpu_clock" => "1850 MHz",
                "vram_used" => "2,1 GB",
                "ram_used" => item.DisplayFormat switch
                {
                    1 => "9,4 GB",
                    2 => "9,4 / 15,7 GB",
                    _ => "9,4 GB (60%)"
                },
                "fan_rpm" => "1650 RPM",
                "net_speed" => "D:12 M/s U:2 M/s",
                "system_time" => DateTime.Now.ToString("HH:mm:ss"),
                _ => "100"
            };
        }
    }
}
