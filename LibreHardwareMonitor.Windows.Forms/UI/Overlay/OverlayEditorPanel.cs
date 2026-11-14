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

        // Card 1: Preview
        _previewCard = new FluentSectionCard
        {
            Title = "Pré-visualização do HUD em Tempo Real",
            Subtitle = "Simulação da sobreposição com a geometria, transparência e estilo configurados.",
            AutoHeight = true
        };
        _previewCanvas = new PreviewCanvas(_config)
        {
            Dock = DockStyle.Top,
            Height = LogicalToDeviceUnits(170)
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
            "Grelha Compacta (2 Colunas)"
        });
        _cmbLayout.SelectedIndex = (int)_config.Layout;
        _cmbLayout.SelectedIndexChanged += (_, _) =>
        {
            _config.Layout = (OverlayLayout)_cmbLayout.SelectedIndex;
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
        appearanceRows.Controls.Add(new SettingRow("Paleta de Cores", "Personalização cromática dos elementos", colorsRow, false));
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
                Width = LogicalToDeviceUnits(130),
                Height = LogicalToDeviceUnits(28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            FluentTextBox txtCustom = new()
            {
                Text = item.DisplayName,
                Width = LogicalToDeviceUnits(180),
                Height = LogicalToDeviceUnits(28)
            };
            txtCustom.TextChanged += (_, _) =>
            {
                item.CustomTitle = txtCustom.Text;
                NotifyChange();
            };

            FluentButton btnUp = new()
            {
                Text = "▲",
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
                Text = "▼",
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

            row.Controls.Add(btnActive);
            row.Controls.Add(lblName);
            row.Controls.Add(txtCustom);
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
    /// Custom canvas control rendering live overlay simulation inside the editor card.
    /// </summary>
    private sealed class PreviewCanvas : Control
    {
        private readonly OverlayConfig _config;

        public PreviewCanvas(OverlayConfig config)
        {
            _config = config;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Theme theme = Theme.Current;

            // Draw game canvas simulation backdrop
            Rectangle bounds = ClientRectangle;
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

            // HUD Container Bounds
            int hudPad = LogicalToDeviceUnits(16);
            var activeItems = _config.MetricItems.Where(m => m.Enabled).OrderBy(m => m.Order).Take(6).ToList();
            if (activeItems.Count == 0)
            {
                Rectangle emptyRect = new(hudPad, hudPad, bounds.Width - (hudPad * 2), bounds.Height - (hudPad * 2));
                TextRenderer.DrawText(g, "Nenhuma métrica ativada no HUD.", FluentStyle.UiFont, emptyRect, theme.MutedForegroundColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            int itemH = LogicalToDeviceUnits(22);
            int hudW = Math.Min(bounds.Width - (hudPad * 2), LogicalToDeviceUnits(260));
            int hudH = (activeItems.Count * itemH) + LogicalToDeviceUnits(16);

            Rectangle hudRect = new(hudPad, hudPad, hudW, Math.Min(bounds.Height - (hudPad * 2), hudH));

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

            // Simulated values
            string[] mockValues = { "62 °C", "48 %", "85 W", "5200 MHz", "68 °C", "72 %" };
            Font lblFont = FluentStyle.CreateUiFont(FontStyle.Regular, _config.FontSize);
            Font valFont = _config.UseMonospaceNumbers
                ? FluentStyle.CreateMonoFont(_config.FontSize, FontStyle.Bold)
                : FluentStyle.CreateUiFont(FontStyle.Bold, _config.FontSize);

            using SolidBrush lblBrush = new(_config.LabelColor);
            using SolidBrush valBrush = new(_config.ValueColor);
            using SolidBrush shadowBrush = new(Color.FromArgb(180, Color.Black));

            int rowY = hudRect.Y + LogicalToDeviceUnits(8);
            for (int i = 0; i < activeItems.Count; i++)
            {
                if (rowY + itemH > hudRect.Bottom)
                    break;

                OverlayMetricItem item = activeItems[i];
                string val = i < mockValues.Length ? mockValues[i] : "--";

                Rectangle lr = new(hudRect.X + LogicalToDeviceUnits(10), rowY, hudRect.Width / 2, itemH);
                Rectangle vr = new(hudRect.X + (hudRect.Width / 2), rowY, (hudRect.Width / 2) - LogicalToDeviceUnits(10), itemH);

                if (_config.ShowTextShadow)
                {
                    Rectangle ls = new(lr.X + 1, lr.Y + 1, lr.Width, lr.Height);
                    Rectangle vs = new(vr.X + 1, vr.Y + 1, vr.Width, vr.Height);
                    g.DrawString(item.DisplayName, lblFont, shadowBrush, ls, FluentStyle.FormatNearCenter);
                    g.DrawString(val, valFont, shadowBrush, vs, FluentStyle.FormatFarCenter);
                }

                g.DrawString(item.DisplayName, lblFont, lblBrush, lr, FluentStyle.FormatNearCenter);
                g.DrawString(val, valFont, valBrush, vr, FluentStyle.FormatFarCenter);

                if (_config.ShowProgressBars)
                {
                    int barH = Math.Max(2, (int)(_config.FontSize * 0.2f));
                    int barY = lr.Bottom - barH;
                    using SolidBrush barBg = new(Color.FromArgb(50, _config.LabelColor));
                    using SolidBrush barFill = new(_config.ValueColor);
                    g.FillRectangle(barBg, lr.X, barY, hudRect.Width - LogicalToDeviceUnits(20), barH);
                    g.FillRectangle(barFill, lr.X, barY, (hudRect.Width - LogicalToDeviceUnits(20)) * 0.55f, barH);
                }

                rowY += itemH;
            }

            lblFont.Dispose();
            valFont.Dispose();
        }
    }
}
