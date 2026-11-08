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
using LibreHardwareMonitor.Windows.Forms.UI.Icons;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>
/// Fan control page: KPI strip, toolbar, banners (faults, setup, unclean previous session) and the fan
/// channels grouped by what is connected to them (unidentified first, unused last and collapsed).
/// </summary>
public sealed class FanControlPanel : UserControl, IThemedControl
{
    // Display order of the groups. "Unassigned" comes first so it is the first thing the user sees.
    private static readonly FanChannelRole[] GroupOrder =
    {
        FanChannelRole.Unassigned, FanChannelRole.CpuCooler, FanChannelRole.Pump, FanChannelRole.Radiator,
        FanChannelRole.CaseFan, FanChannelRole.Gpu, FanChannelRole.NotConnected
    };

    private FanControlEngine _engine;
    private readonly List<FanChannelCard> _cards = new();
    private bool _loading;
    private bool _notConnectedCollapsed = true;
    private bool _uncleanDismissed;

    // Last valid readings, so the KPIs never flicker to "-" between sensor updates.
    private float _lastCpuTemp;
    private float _lastGpuTemp;

    private readonly StackPanel _page;
    private readonly ScrollHost _host;

    private readonly MetricStrip _metrics;
    private readonly FluentMetricCard _cpuTempCard;
    private readonly FluentMetricCard _gpuTempCard;
    private readonly FluentMetricCard _channelsCard;
    private readonly FluentMetricCard _avgPwmCard;

    private readonly ToolbarRow _toolbar;
    private readonly Label _presetLabel;
    private readonly FluentComboBox _presetCombo;
    private readonly FluentButton _btnWizard;
    private readonly FluentBadge _safetyBadge;

    private readonly WatchdogAlertPanel _faultBanner;
    private readonly Label _faultLabel;
    private readonly WatchdogAlertPanel _uncleanBanner;
    private readonly Label _uncleanLabel;
    private readonly FluentButton _uncleanDismiss;
    private readonly WatchdogAlertPanel _setupBanner;
    private readonly Label _setupLabel;

    private readonly StackPanel _channelsStack;
    private readonly FluentSectionCard _emptyCard;
    private readonly Label _emptyLabel;
    private readonly FluentSectionCard _infoCard;
    private readonly Label _infoLabel;

    public FanControlPanel() : this(null)
    {
    }

    public FanControlPanel(FanControlEngine engine)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Dock = DockStyle.Fill;

        // 1. KPI strip (same geometry as the dashboard summary).
        _metrics = new MetricStrip();
        _cpuTempCard = NewCard(FluentIcons.ForHardware(HardwareType.Cpu), "Temperatura CPU", "°C", "CPU Package");
        _gpuTempCard = NewCard(FluentIcons.ForSensorType(SensorType.Temperature), "Temperatura GPU", "°C", "GPU Core");
        _channelsCard = NewCard(FluentIcons.ForSensorType(SensorType.Fan), "Canais", "", "Controláveis por PWM");
        _avgPwmCard = NewCard(FluentIcons.ForSensorType(SensorType.Control), "Velocidade média", "%", "PWM dos canais em uso");
        _metrics.AddCard(_cpuTempCard);
        _metrics.AddCard(_gpuTempCard);
        _metrics.AddCard(_channelsCard);
        _metrics.AddCard(_avgPwmCard);

        // 2. Toolbar: global profile on the left, safety state on the right.
        _toolbar = new ToolbarRow();
        _presetLabel = new Label { Text = "Perfil global", AutoSize = true, Font = FluentStyle.UiFont };
        _presetCombo = new FluentComboBox { Width = LogicalToDeviceUnits(200) };
        _presetCombo.Items.AddRange(new object[] { "BIOS (padrão)", "Silencioso", "Equilibrado", "Desempenho", "Personalizado" });
        _presetCombo.SelectedIndex = 0;
        _presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;
        _btnWizard = PageMetrics.FitButton(new FluentButton { Text = "Assistente de configuração" });
        _btnWizard.Click += BtnWizard_Click;
        _safetyBadge = new FluentBadge { Height = LogicalToDeviceUnits(24), Tag = PageMetrics.AlignRight };
        _safetyBadge.SetBadge("Proteções ativas · CPU 90 °C · GPU 85 °C · sensor · paragem", BadgeStyle.Success);
        _toolbar.Controls.Add(_presetLabel);
        _toolbar.Controls.Add(_presetCombo);
        _toolbar.Controls.Add(_btnWizard);
        _toolbar.Controls.Add(_safetyBadge);
        // The row is as tall as its tallest control, and the button matches the combo box so their edges line up.
        _btnWizard.Height = _presetCombo.Height;
        _toolbar.Height = Math.Max(_presetCombo.Height, _safetyBadge.Height);

        // 3. Banners.
        _faultBanner = new WatchdogAlertPanel { Danger = true, Visible = false };
        _faultLabel = NewBannerLabel(FluentStyle.UiFontSemibold);
        _faultBanner.Controls.Add(_faultLabel);

        _uncleanBanner = new WatchdogAlertPanel { Danger = true, Visible = false };
        _uncleanLabel = NewBannerLabel(FluentStyle.UiFont);
        _uncleanLabel.Text = "A sessão anterior terminou sem devolver as ventoinhas à BIOS. Reinicie o PC para garantir o estado de fábrica.";
        _uncleanDismiss = PageMetrics.FitButton(new FluentButton { Text = "Entendi", Tag = PageMetrics.AlignRight });
        _uncleanDismiss.Click += (_, _) => { _uncleanDismissed = true; _uncleanBanner.Visible = false; };
        _uncleanBanner.Controls.Add(_uncleanLabel);
        _uncleanBanner.Controls.Add(_uncleanDismiss);

        _setupBanner = new WatchdogAlertPanel { Visible = false };
        _setupLabel = NewBannerLabel(FluentStyle.UiFont);
        _setupBanner.Controls.Add(_setupLabel);

        // 4. Channels (groups are rebuilt by RefreshChannels).
        _channelsStack = new StackPanel { Gap = PageMetrics.Gap };

        _emptyCard = new FluentSectionCard
        {
            Title = "Nenhum canal de ventilação detetado",
            Subtitle = "Não foram encontrados controladores Super I/O ou dispositivos com escrita PWM.",
            AutoHeight = true
        };
        _emptyLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = LogicalToDeviceUnits(40),
            Font = FluentStyle.UiFont,
            Text = "O controlo PWM requer suporte no chip Super I/O (Nuvoton, ITE, Fintek) ou um controlador dedicado " +
                   "(NZXT, Corsair, Aquacomputer). Em portáteis e motherboards OEM o firmware pode reservar este controlo."
        };
        _emptyCard.Controls.Add(_emptyLabel);

        _infoCard = new FluentSectionCard
        {
            Title = "Ventoinhas que o PC não vê",
            Subtitle = "Nem todas as ventoinhas ligadas ao PC aparecem nesta página.",
            AutoHeight = true
        };
        _infoLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = LogicalToDeviceUnits(58),
            Font = FluentStyle.UiFont,
            Text = "Ventoinhas ligadas a um hub, a um controlador próprio da caixa, ou alimentadas direto pela fonte (SATA/Molex), " +
                   "não enviam rotação à motherboard e não podem ser lidas nem controladas aqui. Se um hub tiver um cabo PWM ligado " +
                   "a um conector da motherboard, esse conector controla todas as ventoinhas do hub como um único canal."
        };
        _infoCard.Controls.Add(_infoLabel);

        _page = new StackPanel { Dock = DockStyle.Top };
        _page.Controls.Add(_metrics);
        _page.Controls.Add(_toolbar);
        _page.Controls.Add(_faultBanner);
        _page.Controls.Add(_uncleanBanner);
        _page.Controls.Add(_setupBanner);
        _page.Controls.Add(_emptyCard);
        _page.Controls.Add(_channelsStack);
        _page.Controls.Add(_infoCard);
        _host = new ScrollHost(_page) { Dock = DockStyle.Fill };
        Controls.Add(_host);

        if (engine != null)
            SetEngine(engine);

        ApplyTheme(Theme.Current);
    }

    private static Label NewBannerLabel(Font font) =>
        new() { AutoSize = false, Height = font.Height + 4, Font = font, AutoEllipsis = true, Tag = PageMetrics.Fill };

    private static FluentMetricCard NewCard(Image icon, string caption, string unit, string detail)
    {
        FluentMetricCard card = new() { Icon = icon };
        card.SetMetric(caption, "-", unit, detail);
        return card;
    }

    // ------------------------------------------------------------------ engine

    public void SetEngine(FanControlEngine engine)
    {
        if (_engine != null)
        {
            _engine.ChannelsChanged -= Engine_ChannelsChanged;
            _engine.PresetChanged -= Engine_PresetChanged;
            _engine.AlertsChanged -= Engine_AlertsChanged;
        }

        _engine = engine;
        if (_engine == null)
            return;

        _engine.ChannelsChanged += Engine_ChannelsChanged;
        _engine.PresetChanged += Engine_PresetChanged;
        _engine.AlertsChanged += Engine_AlertsChanged;

        SelectPreset(_engine.ActivePreset);
        RefreshChannels();
        UpdateAlerts();
    }

    private void BtnWizard_Click(object sender, EventArgs e)
    {
        if (_engine == null)
            return;

        using (FanSetupWizard wizard = new(_engine))
            wizard.ShowDialog(FindForm());

        RefreshChannels();
    }

    private void Engine_ChannelsChanged(object sender, EventArgs e) => RefreshChannels();

    private void Engine_PresetChanged(object sender, EventArgs e)
    {
        if (_engine != null)
            SelectPreset(_engine.ActivePreset);
    }

    private void Engine_AlertsChanged(object sender, EventArgs e) => UpdateAlerts();

    private void SelectPreset(GlobalFanPreset preset)
    {
        _loading = true;
        try
        {
            _presetCombo.SelectedIndex = (int)preset;
        }
        finally
        {
            _loading = false;
        }
    }

    private void PresetCombo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_loading || _engine == null || _presetCombo.SelectedIndex < 0 || _presetCombo.SelectedIndex > 4)
            return;

        GlobalFanPreset selected = (GlobalFanPreset)_presetCombo.SelectedIndex;
        if (selected == _engine.ActivePreset)
            return;

        if (selected is GlobalFanPreset.Silent or GlobalFanPreset.Balanced or GlobalFanPreset.Performance)
        {
            int targets = _engine.CountPresetTargets();
            string name = _presetCombo.SelectedItem?.ToString() ?? string.Empty;

            if (targets == 0)
            {
                MessageBox.Show(FindForm(), "Ainda não há canais com função escolhida onde um perfil possa ser aplicado.\n\nIdentifique os canais abaixo primeiro.",
                                "Perfil global", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SelectPreset(_engine.ActivePreset);
                return;
            }

            string text = $"Aplicar o perfil «{name}» a {targets} {(targets == 1 ? "canal" : "canais")}?\n\n" +
                          "A bomba, os canais por identificar e os canais não usados não são alterados.";
            if (MessageBox.Show(FindForm(), text, "Perfil global", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                SelectPreset(_engine.ActivePreset);
                return;
            }
        }

        _engine.ApplyPreset(selected);
        foreach (FanChannelCard card in _cards)
            card.SyncUiWithConfig();
    }

    // ------------------------------------------------------------------ groups

    private static string GroupTitle(FanChannelRole role) => role switch
    {
        FanChannelRole.Unassigned => "Por identificar",
        FanChannelRole.CpuCooler => "Cooler do CPU",
        FanChannelRole.Pump => "Bomba (watercooler)",
        FanChannelRole.Radiator => "Radiador",
        FanChannelRole.CaseFan => "Ventoinhas da caixa",
        FanChannelRole.Gpu => "Placa gráfica",
        _ => "Nada ligado / não usado"
    };

    private static Image GroupIcon(FanChannelRole role) => role switch
    {
        FanChannelRole.CpuCooler => FluentIcons.ForHardware(HardwareType.Cpu),
        FanChannelRole.Gpu => FluentIcons.ForHardware(HardwareType.GpuNvidia),
        FanChannelRole.Pump => FluentIcons.ForSensorType(SensorType.Flow),
        _ => FluentIcons.ForSensorType(SensorType.Fan)
    };

    public void RefreshChannels()
    {
        _page.SuspendLayout();
        _channelsStack.SuspendLayout();

        foreach (Control group in _channelsStack.Controls.Cast<Control>().ToList())
        {
            _channelsStack.Controls.Remove(group);
            group.Dispose();
        }

        _cards.Clear();

        IReadOnlyList<FanChannelRuntime> channels = _engine?.Channels ?? Array.Empty<FanChannelRuntime>();
        bool hasChannels = channels.Count > 0;
        _emptyCard.Visible = !hasChannels;
        _channelsStack.Visible = hasChannels;
        _infoCard.Visible = hasChannels;

        foreach (FanChannelRole role in GroupOrder)
        {
            List<FanChannelRuntime> members = channels.Where(c => c.Config.Role == role).ToList();
            if (members.Count == 0)
                continue;

            bool collapsible = role == FanChannelRole.NotConnected;
            GroupHeader header = new()
            {
                Text = GroupTitle(role),
                Detail = members.Count == 1 ? "· 1 canal" : $"· {members.Count} canais",
                Icon = GroupIcon(role),
                Collapsible = collapsible,
                Collapsed = collapsible && _notConnectedCollapsed
            };

            WrapGrid grid = new() { MinColumnWidth = 340, Visible = !header.Collapsed };
            foreach (FanChannelRuntime channel in members)
            {
                FanChannelCard card = new(channel, _engine);
                card.RoleChanged += Card_RoleChanged;
                _cards.Add(card);
                grid.Controls.Add(card);
            }

            if (collapsible)
            {
                header.CollapsedChanged += (_, _) =>
                {
                    _notConnectedCollapsed = header.Collapsed;
                    grid.Visible = !header.Collapsed;
                };
            }

            // A group = header + grid with a tighter gap between them.
            StackPanel block = new() { Gap = 8 };
            block.Controls.Add(header);
            block.Controls.Add(grid);
            _channelsStack.Controls.Add(block);
        }

        UpdateChannelMetrics();
        UpdateBanners();
        _channelsStack.ResumeLayout(true);
        _page.ResumeLayout(true);
        ApplyTheme(Theme.Current);
    }

    private void Card_RoleChanged(object sender, EventArgs e)
    {
        // Regroup after the card finished handling its own event (the card is disposed by the rebuild).
        BeginInvoke(new Action(RefreshChannels));
    }

    // ------------------------------------------------------------------ banners and metrics

    private void UpdateBanners()
    {
        int unassigned = _engine?.Channels.Count(c => c.Config.Role == FanChannelRole.Unassigned) ?? 0;
        _setupBanner.Visible = unassigned > 0;
        _setupLabel.Text = unassigned == 1
            ? "1 canal por identificar. Abra o «Assistente de configuração» ou use «Identificar» no cartão. Até lá a app não escreve nele e fica na BIOS."
            : $"{unassigned} canais por identificar. Abra o «Assistente de configuração» ou use «Identificar» nos cartões. Até lá a app não escreve neles e ficam na BIOS.";

        _uncleanBanner.Visible = _engine != null && _engine.PreviousSessionUnclean && !_uncleanDismissed;
    }

    private void UpdateAlerts()
    {
        IReadOnlyList<string> alerts = _engine?.CurrentAlerts ?? Array.Empty<string>();
        _faultBanner.Visible = alerts.Count > 0;
        _faultLabel.Text = alerts.Count == 0 ? string.Empty : string.Join("   ·   ", alerts);
    }

    private void UpdateChannelMetrics()
    {
        IReadOnlyList<FanChannelRuntime> channels = _engine?.Channels ?? Array.Empty<FanChannelRuntime>();
        int total = channels.Count;
        int managed = channels.Count(c => c.IsManaged);
        _channelsCard.SetMetric("Canais", total.ToString(), "", total == 0 ? "Nenhum controlador" : $"{managed} sob controlo da app");

        List<FanChannelRuntime> inUse = channels.Where(c => c.Config.Role != FanChannelRole.NotConnected).ToList();
        if (inUse.Count == 0)
        {
            _avgPwmCard.SetMetric("Velocidade média", "-", "%", "PWM dos canais em uso");
            return;
        }

        float avg = inUse.Average(c => c.Control.ControlMode == ControlMode.Software ? c.LastAppliedPwm : (c.Control.Sensor?.Value ?? 0f));
        MetricCardSeverity sev = avg >= 85f ? MetricCardSeverity.Warm : MetricCardSeverity.Normal;
        _avgPwmCard.SetMetric("Velocidade média", $"{avg:F0}", "%", "PWM dos canais em uso", sev, avg / 100f);
    }

    public void UpdateTelemetry()
    {
        if (_engine == null || !Visible)
            return;

        float cpu = ReadTemperature(_engine.LastCpuTemp, h => h.HardwareType == HardwareType.Cpu, ref _lastCpuTemp);
        SetTemperature(_cpuTempCard, "Temperatura CPU", "CPU Package", cpu, 75f, 85f);

        float gpu = ReadTemperature(_engine.LastGpuTemp,
                                    h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel,
                                    ref _lastGpuTemp);
        SetTemperature(_gpuTempCard, "Temperatura GPU", "GPU Core", gpu, 72f, 83f);

        UpdateChannelMetrics();
        UpdateAlerts();
        foreach (FanChannelCard card in _cards)
            card.UpdateTelemetry();
    }

    /// <summary>Uses the engine reading, falls back to the first temperature sensor of the hardware, then to the last good value.</summary>
    private float ReadTemperature(float engineValue, Func<IHardware, bool> match, ref float last)
    {
        float value = engineValue;
        if (value <= 0f && _engine.Computer != null)
        {
            IHardware hw = _engine.Computer.Hardware.FirstOrDefault(match);
            ISensor sensor = hw?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
            if (sensor?.Value != null)
                value = sensor.Value.Value;
        }

        if (value > 0f)
            last = value;

        return value > 0f ? value : last;
    }

    private static void SetTemperature(FluentMetricCard card, string caption, string detail, float value, float warm, float hot)
    {
        if (value <= 0f)
        {
            card.SetMetric(caption, "-", "°C", detail);
            return;
        }

        MetricCardSeverity sev = value >= hot ? MetricCardSeverity.Hot : (value >= warm ? MetricCardSeverity.Warm : MetricCardSeverity.Normal);
        card.SetMetric(caption, $"{value:F0}", "°C", detail, sev, value / 100f);
    }

    public void ApplyTheme(Theme theme)
    {
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;

        theme.Apply(_host);

        _presetLabel.ForeColor = theme.MutedForegroundColor;
        _emptyLabel.BackColor = theme.SurfaceColor;
        _emptyLabel.ForeColor = theme.MutedForegroundColor;
        _infoLabel.BackColor = theme.SurfaceColor;
        _infoLabel.ForeColor = theme.MutedForegroundColor;

        Invalidate(true);
    }
}
