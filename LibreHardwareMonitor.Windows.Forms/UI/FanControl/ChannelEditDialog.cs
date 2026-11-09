// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using LibreHardwareMonitor.Windows.Forms.UI.Controls;
using LibreHardwareMonitor.Windows.Forms.UI.Themes;

namespace LibreHardwareMonitor.Windows.Forms.UI.FanControl;

/// <summary>Small themed dialog to name a fan channel and say how many fans share it.</summary>
public sealed class ChannelEditDialog : Form
{
    private readonly FluentTextBox _nameBox;
    private readonly FluentNumberBox _countBox;

    public string ChannelLabel => _nameBox.Text.Trim();

    public int FanCount => (int)_countBox.Value;

    public ChannelEditDialog(string hardwareName, string channelName, string currentName, int fanCount)
    {
        Theme theme = Theme.Current;

        Text = "Editar canal";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = theme.BackgroundColor;
        ForeColor = theme.ForegroundColor;
        Font = FluentStyle.UiFont;
        AutoScaleMode = AutoScaleMode.None; // sizes below are already in device units
        ClientSize = new Size(LogicalToDeviceUnits(380), LogicalToDeviceUnits(214));

        int pad = LogicalToDeviceUnits(18);
        int width = ClientSize.Width - (pad * 2);
        int y = pad;

        Label origin = new()
        {
            Text = $"{hardwareName} · {channelName}",
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(pad, y),
            Size = new Size(width, Font.Height + 4),
            ForeColor = theme.MutedForegroundColor,
            BackColor = theme.BackgroundColor
        };
        y += origin.Height + LogicalToDeviceUnits(10);

        Label nameCaption = new() { Text = "Nome", AutoSize = true, Location = new Point(pad, y), BackColor = theme.BackgroundColor };
        y += nameCaption.PreferredHeight + LogicalToDeviceUnits(4);

        _nameBox = new FluentTextBox
        {
            Text = currentName,
            MaxLength = 40,
            Location = new Point(pad, y),
            Width = width
        };
        y += _nameBox.Height + LogicalToDeviceUnits(12);

        Label countCaption = new() { Text = "Ventoinhas neste canal (hub ou splitter)", AutoSize = true, Location = new Point(pad, y), BackColor = theme.BackgroundColor };
        y += countCaption.PreferredHeight + LogicalToDeviceUnits(4);

        _countBox = new FluentNumberBox
        {
            Minimum = 1,
            Maximum = 12,
            Value = Math.Max(1, Math.Min(12, fanCount)),
            Location = new Point(pad, y),
            Width = LogicalToDeviceUnits(110)
        };
        y += _countBox.Height + LogicalToDeviceUnits(18);

        FluentButton cancel = PageMetrics.FitButton(new FluentButton { Text = "Cancelar" });
        FluentButton save = PageMetrics.FitButton(new FluentButton { Text = "Guardar", Variant = FluentButtonVariant.Accent });
        save.Location = new Point(ClientSize.Width - pad - save.Width, y);
        cancel.Location = new Point(save.Left - LogicalToDeviceUnits(8) - cancel.Width, y);

        ClientSize = new Size(ClientSize.Width, y + save.Height + pad);
        Controls.AddRange(new Control[] { origin, nameCaption, _nameBox, countCaption, _countBox, cancel, save });
        save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.Handled = true; }
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Current.ApplyTitleBar(this);
    }
}
