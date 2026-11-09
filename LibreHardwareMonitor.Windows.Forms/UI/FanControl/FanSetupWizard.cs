// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>
/// Two-page assistant. Page 1 walks through the channels one by one: identify (the channel goes to 100 % for a few
/// seconds), say what is connected, name it. Page 2 is an optional calibration of the channels the user ticks.
/// Nothing runs by itself; closing the window cancels a running calibration and hands the channel back.
/// </summary>
public sealed class FanSetupWizard : Form
{
    private readonly FanControlEngine _engine;
    private readonly List<FanChannelRuntime> _channels;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 400 };
    private int _index;
    private bool _identifyPageActive = true;
    private bool _loading;

    // chrome
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly FluentButton _btnBack;
    private readonly FluentButton _btnNext;

    // page 1
    private readonly Panel _pageIdentify;
    private readonly Label _stepLabel;
    private readonly Label _channelLabel;
    private readonly Label _liveLabel;
    private readonly Label _identifyHint;
    private readonly FluentButton _btnIdentify;
    private readonly FluentComboBox _roleCombo;
    private readonly FluentTextBox _nameBox;
    private readonly FluentNumberBox _countBox;

    // page 2
    private readonly Panel _pageCalibrate;
    private readonly Label _calibrateInfo;
    private readonly ScrollHost _list;
    private readonly Panel _listContent;
    private readonly Dictionary<FanChannelRuntime, CheckBox> _checks = new();
    private readonly Label _calibrateStatus;
    private readonly ThinProgress _progress;
    private readonly Label _results;
    private readonly FluentButton _btnCalibrate;
    private readonly Queue<FanChannelRuntime> _queue = new();
    private FanChannelRuntime _current;
    private bool _calibrating;
    private int _total;
    private int _done;

    public FanSetupWizard(FanControlEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _channels = _engine.Channels.ToList();

        Theme theme = Theme.Current;
        Text = "Assistente de configuração das ventoinhas";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;
        Font = FluentStyle.UiFont;
        AutoScaleMode = AutoScaleMode.None; // sizes below are already in device units

        int pad = LogicalToDeviceUnits(22);
        int width = LogicalToDeviceUnits(560);
        ClientSize = new Size(width, LogicalToDeviceUnits(520));
        int inner = width - (pad * 2);
        int gap = LogicalToDeviceUnits(8);

        _title = new Label { Text = "Assistente de configuração", AutoSize = false, Font = FluentStyle.CreateUiFont(FontStyle.Bold, 12f), Location = new Point(pad, pad), Size = new Size(inner, LogicalToDeviceUnits(26)), BackColor = theme.BackgroundColor };
        _subtitle = new Label { AutoSize = false, Font = FluentStyle.UiFont, ForeColor = theme.MutedForegroundColor, Location = new Point(pad, pad + LogicalToDeviceUnits(28)), Size = new Size(inner, LogicalToDeviceUnits(36)), BackColor = theme.BackgroundColor };
        int top = _subtitle.Bottom + LogicalToDeviceUnits(14);
        int pageHeight = LogicalToDeviceUnits(362);

        // ---- page 1: identify
        _pageIdentify = new Panel { Location = new Point(pad, top), Size = new Size(inner, pageHeight), BackColor = theme.BackgroundColor };
        _stepLabel = new Label { AutoSize = false, Font = FluentStyle.UiFontSemibold, Location = new Point(0, 0), Size = new Size(inner, LogicalToDeviceUnits(22)), BackColor = theme.BackgroundColor };
        _channelLabel = new Label { AutoSize = false, AutoEllipsis = true, ForeColor = theme.MutedForegroundColor, Location = new Point(0, LogicalToDeviceUnits(22)), Size = new Size(inner, LogicalToDeviceUnits(20)), BackColor = theme.BackgroundColor };
        _liveLabel = new Label { AutoSize = false, Font = FluentStyle.CreateDisplayFont(14f), Location = new Point(0, LogicalToDeviceUnits(56)), Size = new Size(inner, LogicalToDeviceUnits(30)), BackColor = theme.BackgroundColor };

        _btnIdentify = new FluentButton { Text = "Identificar (8 s)", Variant = FluentButtonVariant.Accent };
        _btnIdentify.Width = TextRenderer.MeasureText("Parar identificação (8 s)", FluentStyle.UiFontSemibold).Width + LogicalToDeviceUnits(40);
        _btnIdentify.Location = new Point(0, LogicalToDeviceUnits(100));
        _btnIdentify.Click += (_, _) =>
        {
            FanChannelRuntime c = _channels[_index];
            if (c.IsIdentifying) _engine.CancelIdentify(c); else _engine.Identify(c);
            RefreshLive();
        };
        _identifyHint = new Label
        {
            AutoSize = false,
            ForeColor = theme.MutedForegroundColor,
            Location = new Point(_btnIdentify.Right + gap, LogicalToDeviceUnits(98)),
            Size = new Size(inner - _btnIdentify.Width - gap, LogicalToDeviceUnits(40)),
            Text = "O canal sobe a 100 % durante 8 s e depois volta ao que estava. Veja ou ouça qual ventoinha acelera e se as RPM sobem.",
            BackColor = theme.BackgroundColor
        };

        int y = LogicalToDeviceUnits(150);
        Label roleCaption = Caption("O que está ligado a este canal", y);
        _roleCombo = new FluentComboBox { Location = new Point(0, y + LogicalToDeviceUnits(22)), Width = inner };
        foreach (FanChannelRole role in Enum.GetValues(typeof(FanChannelRole)))
            _roleCombo.Items.Add(FanRolePolicy.DisplayName(role));

        y += LogicalToDeviceUnits(70);
        Label nameCaption = Caption("Nome (opcional)", y);
        _nameBox = new FluentTextBox
        {
            MaxLength = 40,
            Location = new Point(0, y + LogicalToDeviceUnits(22)),
            Width = inner
        };

        y += LogicalToDeviceUnits(70);
        Label countCaption = Caption("Ventoinhas neste canal (hub ou splitter)", y);
        _countBox = new FluentNumberBox
        {
            Minimum = 1,
            Maximum = 12,
            Location = new Point(0, y + LogicalToDeviceUnits(22)),
            Width = LogicalToDeviceUnits(110)
        };

        _pageIdentify.Controls.AddRange(new Control[] { _stepLabel, _channelLabel, _liveLabel, _btnIdentify, _identifyHint, roleCaption, _roleCombo, nameCaption, _nameBox, countCaption, _countBox });

        // ---- page 2: calibrate
        _pageCalibrate = new Panel { Location = new Point(pad, top), Size = new Size(inner, pageHeight), BackColor = theme.BackgroundColor, Visible = false };
        _calibrateInfo = new Label
        {
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(inner, LogicalToDeviceUnits(64)),
            ForeColor = theme.MutedForegroundColor,
            BackColor = theme.BackgroundColor,
            Text = "A calibração mede a que velocidade cada ventoinha ainda roda e se responde ao PWM (cerca de 1 minuto por canal; as ventoinhas vão variar de velocidade). " +
                   "Pára sozinha se o CPU passar dos 80 °C ou a GPU dos 75 °C, e no fim cada canal volta ao estado anterior. É opcional."
        };
        _listContent = new Panel { BackColor = theme.BackgroundColor, Size = new Size(inner, LogicalToDeviceUnits(120)) };
        _list = new ScrollHost(_listContent) { Location = new Point(0, _calibrateInfo.Bottom + gap), Size = new Size(inner, LogicalToDeviceUnits(120)) };
        _calibrateStatus = new Label { AutoSize = false, Location = new Point(0, _list.Bottom + gap), Size = new Size(inner, LogicalToDeviceUnits(20)), Font = FluentStyle.UiFontSemibold, BackColor = theme.BackgroundColor };
        _progress = new ThinProgress { Location = new Point(0, _calibrateStatus.Bottom + LogicalToDeviceUnits(4)), Size = new Size(inner, LogicalToDeviceUnits(6)) };
        _results = new Label { AutoSize = false, Location = new Point(0, _progress.Bottom + gap), Size = new Size(inner, LogicalToDeviceUnits(60)), ForeColor = theme.MutedForegroundColor, BackColor = theme.BackgroundColor };
        _btnCalibrate = PageMetrics.FitButton(new FluentButton { Text = "Calibrar selecionados", Variant = FluentButtonVariant.Accent });
        _btnCalibrate.Width = Math.Max(_btnCalibrate.Width, TextRenderer.MeasureText("Parar calibração", FluentStyle.UiFontSemibold).Width + LogicalToDeviceUnits(40));
        _btnCalibrate.Location = new Point(0, _results.Bottom + gap);
        _btnCalibrate.Click += BtnCalibrate_Click;
        _pageCalibrate.Controls.AddRange(new Control[] { _calibrateInfo, _list, _calibrateStatus, _progress, _results, _btnCalibrate });

        // ---- navigation
        _btnNext = PageMetrics.FitButton(new FluentButton { Text = "Seguinte", Variant = FluentButtonVariant.Accent });
        _btnBack = PageMetrics.FitButton(new FluentButton { Text = "Anterior" });
        _btnNext.Width = Math.Max(_btnNext.Width, TextRenderer.MeasureText("Ir para a calibração", FluentStyle.UiFontSemibold).Width + LogicalToDeviceUnits(36));
        int buttonsY = _pageIdentify.Bottom + LogicalToDeviceUnits(12);
        _btnNext.Location = new Point(ClientSize.Width - pad - _btnNext.Width, buttonsY);
        _btnBack.Location = new Point(_btnNext.Left - gap - _btnBack.Width, buttonsY);
        _btnNext.Click += BtnNext_Click;
        _btnBack.Click += BtnBack_Click;

        ClientSize = new Size(width, _btnNext.Bottom + pad);
        Controls.AddRange(new Control[] { _title, _subtitle, _pageIdentify, _pageCalibrate, _btnBack, _btnNext });

        _timer.Tick += (_, _) => Tick();
        FormClosing += (_, _) => StopEverything();

        // Start at the first channel that still needs identifying.
        int first = _channels.FindIndex(c => c.Config.Role == FanChannelRole.Unassigned);
        _index = first >= 0 ? first : 0;

        if (_channels.Count == 0)
        {
            _subtitle.Text = "Não foram encontrados canais de ventoinha neste PC.";
            _btnNext.Text = "Fechar";
            _btnBack.Visible = false;
            _pageIdentify.Visible = false;
        _identifyPageActive = false;
            return;
        }

        ShowChannel();
        _timer.Start();
    }

    private Label Caption(string text, int y) =>
        new() { Text = text, AutoSize = true, Location = new Point(0, y), BackColor = Theme.Current.BackgroundColor };

    // ------------------------------------------------------------------ page 1

    private void ShowChannel()
    {
        FanChannelRuntime c = _channels[_index];
        _loading = true;
        try
        {
            _subtitle.Text = "Para cada canal: carregue em «Identificar», veja qual ventoinha acelera e diga o que está ligado. " +
                             "Canais por identificar ficam sempre na BIOS.";
            _stepLabel.Text = $"Canal {_index + 1} de {_channels.Count}";
            _channelLabel.Text = $"{c.HardwareName} · {c.ChannelName}";
            _roleCombo.SelectedIndex = (int)c.Config.Role;
            _nameBox.Text = c.Config.Name ?? string.Empty;
            _countBox.Value = Math.Max(1, Math.Min(12, c.Config.FanCount));
        }
        finally
        {
            _loading = false;
        }

        _btnBack.Visible = true;
        _btnBack.Enabled = _index > 0;
        _btnNext.Text = _index == _channels.Count - 1 ? "Ir para a calibração" : "Seguinte";
        RefreshLive();
    }

    private void RefreshLive()
    {
        if (!_identifyPageActive || _channels.Count == 0)
            return;

        FanChannelRuntime c = _channels[_index];
        float pwm = c.Control.ControlMode == ControlMode.Software ? c.LastAppliedPwm : (c.Control.Sensor?.Value ?? 0f);
        float? rpm = c.RpmSensor?.Value;
        string rpmText = !rpm.HasValue ? "sem leitura de RPM" : (rpm.Value < 1f && !c.HasSeenRpm ? "sem rotação" : string.Format(CultureInfo.CurrentCulture, "{0:N0} RPM", rpm.Value));
        _liveLabel.Text = string.Format(CultureInfo.CurrentCulture, "{0:F0} %   ·   {1}", pwm, rpmText);

        _btnIdentify.Text = c.IsIdentifying
            ? $"Parar identificação ({Math.Ceiling(c.IdentifyRemainingMs(Environment.TickCount64) / 1000.0):0} s)"
            : "Identificar (8 s)";
        _btnIdentify.IsActive = c.IsIdentifying;
    }

    /// <summary>Writes the page's role, name and fan count to the channel.</summary>
    private void CommitChannel()
    {
        if (_loading || _channels.Count == 0 || !_identifyPageActive)
            return;

        FanChannelRuntime c = _channels[_index];
        FanChannelRole role = (FanChannelRole)Math.Max(0, _roleCombo.SelectedIndex);
        if (role != c.Config.Role)
            _engine.SetChannelRole(c, role);

        string name = _nameBox.Text.Trim();
        int count = (int)_countBox.Value;
        _engine.Modify(() =>
        {
            c.Config.Name = name;
            c.Config.FanCount = count;
        });
        _engine.SaveSettings();
    }

    private void BtnNext_Click(object sender, EventArgs e)
    {
        if (_channels.Count == 0 || _pageCalibrate.Visible)
        {
            Close();
            return;
        }

        CommitChannel();
        if (_index < _channels.Count - 1)
        {
            _index++;
            ShowChannel();
        }
        else
        {
            ShowCalibration();
        }
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        if (_calibrating)
            return;

        if (_pageCalibrate.Visible)
        {
            _pageCalibrate.Visible = false;
            _pageIdentify.Visible = true;
            _identifyPageActive = true;
            ShowChannel();
            return;
        }

        CommitChannel();
        if (_index > 0)
        {
            _index--;
            ShowChannel();
        }
    }

    // ------------------------------------------------------------------ page 2

    private void ShowCalibration()
    {
        _pageIdentify.Visible = false;
        _identifyPageActive = false;
        _pageCalibrate.Visible = true;
        _subtitle.Text = "Opcional: meça a velocidade mínima de cada ventoinha para que as curvas nunca a deixem parar.";
        _btnNext.Text = "Concluir";
        _btnBack.Visible = true;
        _btnBack.Enabled = true;
        _results.Text = string.Empty;
        _calibrateStatus.Text = string.Empty;
        _progress.Value = 0;
        BuildList();
    }

    private void BuildList()
    {
        _listContent.Controls.Clear();
        _checks.Clear();
        Theme theme = Theme.Current;
        int y = LogicalToDeviceUnits(4);
        int rowHeight = LogicalToDeviceUnits(26);

        foreach (FanChannelRuntime c in _channels)
        {
            bool eligible = FanRolePolicy.AllowsCurve(c.Config.Role) && c.RpmSensor != null;
            bool spinning = c.RpmSensor?.Value > FanCalibrationRun.SpinRpm;
            bool canRun = eligible && spinning;
            string state = !FanRolePolicy.AllowsCurve(c.Config.Role) ? "não calibrável (" + FanRolePolicy.DisplayName(c.Config.Role).ToLowerInvariant() + ")"
                : c.RpmSensor == null ? "sem leitura de RPM"
                : !spinning ? "parada neste momento"
                : c.Config.Calibration != null ? "já calibrado" : "pronto";

            // Rows that cannot be calibrated stay enabled but cannot be ticked, so their text keeps a readable color
            // (a disabled check box would be drawn in the system gray).
            CheckBox box = new()
            {
                Text = $"{c.DisplayName} · {state}",
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(LogicalToDeviceUnits(4), y),
                Size = new Size(_list.Width - LogicalToDeviceUnits(30), rowHeight),
                Tag = canRun,
                AutoCheck = canRun,
                Checked = canRun && c.Config.Calibration == null,
                BackColor = theme.BackgroundColor,
                ForeColor = canRun ? theme.ForegroundColor : theme.MutedForegroundColor
            };
            _listContent.Controls.Add(box);
            _checks[c] = box;
            y += rowHeight;
        }

        _listContent.Height = Math.Max(_list.Height, y + LogicalToDeviceUnits(4));
        _list.PerformLayout();
    }

    private void BtnCalibrate_Click(object sender, EventArgs e)
    {
        if (_calibrating)
        {
            // Stop: the running channel is handed back by the engine, the queue is dropped.
            _queue.Clear();
            if (_current != null)
                _engine.CancelCalibration(_current);

            return;
        }

        _queue.Clear();
        foreach (KeyValuePair<FanChannelRuntime, CheckBox> pair in _checks)
        {
            if (pair.Value.Checked && (bool)pair.Value.Tag)
                _queue.Enqueue(pair.Key);
        }

        if (_queue.Count == 0)
        {
            _calibrateStatus.Text = "Escolha pelo menos um canal.";
            return;
        }

        _total = _queue.Count;
        _done = 0;
        _results.Text = string.Empty;
        SetCalibrating(true);
        StartNext();
    }

    private void SetCalibrating(bool value)
    {
        _calibrating = value;
        _btnCalibrate.Text = value ? "Parar calibração" : "Calibrar selecionados";
        _btnCalibrate.Variant = value ? FluentButtonVariant.Danger : FluentButtonVariant.Accent;
        _btnNext.Enabled = !value;
        _btnBack.Enabled = !value;
        foreach (CheckBox box in _checks.Values)
            box.AutoCheck = !value && (bool)box.Tag;
    }

    private void StartNext()
    {
        while (_queue.Count > 0)
        {
            FanChannelRuntime next = _queue.Dequeue();
            if (_engine.StartCalibration(next, out string error))
            {
                _current = next;
                return;
            }

            AppendResult($"{next.DisplayName}: não iniciada ({error})");
            _done++;
        }

        FinishAll();
    }

    private void FinishAll()
    {
        _current = null;
        SetCalibrating(false);
        _progress.Value = 1;
        _calibrateStatus.Text = "Calibração terminada.";
        BuildList();
    }

    private void AppendResult(string line) =>
        _results.Text = string.IsNullOrEmpty(_results.Text) ? line : _results.Text + Environment.NewLine + line;

    // ------------------------------------------------------------------ ticking

    private void Tick()
    {
        if (_identifyPageActive)
        {
            RefreshLive();
            return;
        }

        if (!_calibrating || _current == null)
            return;

        if (_current.IsCalibrating)
        {
            double each = 1.0 / Math.Max(1, _total);
            _progress.Value = (float)((_done + _current.CalibrationProgress) * each);
            _calibrateStatus.Text = $"{_current.DisplayName}: {_current.CalibrationStatus}";
            return;
        }

        // Finished (or aborted): report and move on.
        FanCalibrationResult result = _current.Config.Calibration;
        string abort = _current.CalibrationAbortReason;
        if (abort != null)
            AppendResult($"{_current.DisplayName}: interrompida ({abort})");
        else if (result != null)
            AppendResult(result.Responsive
                ? string.Format(CultureInfo.CurrentCulture, "{0}: roda a partir de {1:F0} %, máx. {2:N0} RPM (mínimo seguro {3:F0} %)", _current.DisplayName, result.MinSpinPwm, result.MaxRpm, result.SafeFloorPwm)
                : $"{_current.DisplayName}: a velocidade não acompanha o PWM");

        _done++;
        _current = null;
        if (_queue.Count > 0 && abort == null)
            StartNext();
        else
            FinishAll();
    }

    private void StopEverything()
    {
        _timer.Stop();
        _queue.Clear();
        if (_current != null && _current.IsCalibrating)
            _engine.CancelCalibration(_current);

        // Never leave a channel at 100 % because the window was closed in the middle of an identification.
        foreach (FanChannelRuntime c in _channels)
        {
            if (c.IsIdentifying)
                _engine.CancelIdentify(c);
        }

        CommitChannel();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();

        base.Dispose(disposing);
    }

    /// <summary>Thin rounded progress bar in the accent color.</summary>
    private sealed class ThinProgress : Control
    {
        private float _value;

        public ThinProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public float Value
        {
            get => _value;
            set
            {
                _value = Math.Max(0f, Math.Min(1f, value));
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Theme theme = Theme.Current;
            e.Graphics.Clear(theme.BackgroundColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            RectangleF track = new(0, 0, Width - 1, Height - 1);
            FluentStyle.FillRounded(e.Graphics, FluentStyle.Blend(theme.BackgroundColor, theme.ForegroundColor, 0.12), track, track.Height / 2);
            if (_value > 0f)
                FluentStyle.FillRounded(e.Graphics, theme.AccentColor, new RectangleF(0, 0, Math.Max(track.Height, track.Width * _value), track.Height), track.Height / 2);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Current.ApplyTitleBar(this);
    }
}
