// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LibreHardwareMonitor.Windows.Forms.UI.Overlay;

/// <summary>
/// Native hotkey manager using user32.dll RegisterHotKey.
/// Provides zero CPU overhead and zero input lag without using low-level keyboard hooks (WH_KEYBOARD_LL).
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    public const int HotkeyOverlayToggle = 1001;
    public const int HotkeyClickThroughToggle = 1002;

    private const uint ModNone = 0x0000;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly IntPtr _windowHandle;
    private bool _overlayRegistered;
    private bool _clickThroughRegistered;
    private Keys _overlayKeys;
    private Keys _clickThroughKeys;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public GlobalHotkeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public event Action<int> HotkeyTriggered;

    public bool RegisterHotkey(int id, Keys keyCombo)
    {
        if (_windowHandle == IntPtr.Zero || keyCombo == Keys.None)
            return false;

        // Already registered with this combination: nothing to do (the editor reports every change, not only hotkey ones).
        if (id == HotkeyOverlayToggle ? _overlayRegistered && _overlayKeys == keyCombo : _clickThroughRegistered && _clickThroughKeys == keyCombo)
            return true;

        UnregisterHotkey(id);

        uint modifiers = ModNoRepeat;
        if ((keyCombo & Keys.Alt) == Keys.Alt)
            modifiers |= ModAlt;
        if ((keyCombo & Keys.Control) == Keys.Control)
            modifiers |= ModControl;
        if ((keyCombo & Keys.Shift) == Keys.Shift)
            modifiers |= ModShift;

        Keys baseKey = keyCombo & Keys.KeyCode;
        if (baseKey == Keys.None || baseKey == Keys.ControlKey || baseKey == Keys.ShiftKey || baseKey == Keys.Menu)
            return false;

        bool success = RegisterHotKey(_windowHandle, id, modifiers, (uint)baseKey);
        if (success)
        {
            if (id == HotkeyOverlayToggle)
            {
                _overlayRegistered = true;
                _overlayKeys = keyCombo;
            }
            else if (id == HotkeyClickThroughToggle)
            {
                _clickThroughRegistered = true;
                _clickThroughKeys = keyCombo;
            }
        }

        return success;
    }

    public void UnregisterHotkey(int id)
    {
        if (_windowHandle == IntPtr.Zero)
            return;

        UnregisterHotKey(_windowHandle, id);

        if (id == HotkeyOverlayToggle)
            _overlayRegistered = false;
        else if (id == HotkeyClickThroughToggle)
            _clickThroughRegistered = false;
    }

    public void HandleWmHotkey(int id)
    {
        HotkeyTriggered?.Invoke(id);
    }

    public static string FormatKeyCombo(Keys keyCombo)
    {
        if (keyCombo == Keys.None)
            return "Nenhum";

        string result = string.Empty;
        if ((keyCombo & Keys.Control) == Keys.Control)
            result += "Ctrl + ";
        if ((keyCombo & Keys.Alt) == Keys.Alt)
            result += "Alt + ";
        if ((keyCombo & Keys.Shift) == Keys.Shift)
            result += "Shift + ";

        Keys baseKey = keyCombo & Keys.KeyCode;
        if (baseKey != Keys.None && baseKey != Keys.ControlKey && baseKey != Keys.ShiftKey && baseKey != Keys.Menu)
            result += baseKey.ToString();
        else if (result.EndsWith(" + "))
            result = result.Substring(0, result.Length - 3);

        return result;
    }

    public static Keys ParseKeyCombo(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "Nenhum")
            return Keys.None;

        Keys combo = Keys.None;
        string[] parts = text.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawPart in parts)
        {
            string part = rawPart.Trim();
            if (string.Equals(part, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part, "Control", StringComparison.OrdinalIgnoreCase))
            {
                combo |= Keys.Control;
            }
            else if (string.Equals(part, "Alt", StringComparison.OrdinalIgnoreCase))
            {
                combo |= Keys.Alt;
            }
            else if (string.Equals(part, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                combo |= Keys.Shift;
            }
            else if (Enum.TryParse<Keys>(part, true, out Keys parsedKey))
            {
                combo |= parsedKey;
            }
        }

        return combo;
    }

    public void Dispose()
    {
        // Unconditional: UnregisterHotKey on an id that is not registered is harmless, and no id can leak.
        UnregisterHotkey(HotkeyOverlayToggle);
        UnregisterHotkey(HotkeyClickThroughToggle);
    }
}
