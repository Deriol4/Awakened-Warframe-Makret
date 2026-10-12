using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AwakenedWarframeMarket.Services;

public class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000;
    private const uint MOD_NOREPEAT = 0x4000;

    public const ModifierKeys DefaultModifiers = ModifierKeys.Alt;
    public const Key DefaultKey = Key.X;

    /// <summary>
    /// Why a combination can't be used as the price-check hotkey, or null if it's fine.
    /// The hotkey is held while Warframe is in front, so it must not collide with typing or movement.
    /// </summary>
    public static string? ValidationError(ModifierKeys modifiers, Key key)
    {
        bool letterOrDigit = key is >= Key.A and <= Key.Z or >= Key.D0 and <= Key.D9;
        bool functionKey = key is >= Key.F1 and <= Key.F12;

        if (!letterOrDigit && !functionKey)
            return "Use a letter, a digit or an F-key";
        if (key == Key.F12)
            return "F12 is reserved by Windows";
        if (key == Key.F4 && modifiers.HasFlag(ModifierKeys.Alt))
            return "Alt + F4 closes windows";
        if (letterOrDigit && !modifiers.HasFlag(ModifierKeys.Control) && !modifiers.HasFlag(ModifierKeys.Alt))
            return "Add Ctrl or Alt: a letter alone or with Shift fires while you type in chat";
        if (key is Key.W or Key.A or Key.S or Key.D &&
            (modifiers.HasFlag(ModifierKeys.Control) || modifiers.HasFlag(ModifierKeys.Shift)))
            return "Ctrl or Shift with W, A, S, D fires while you crouch or sprint in the game";
        return null;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private IntPtr _windowHandle;
    private Action? _hotkeyAction;
    private bool _disposed;
    private bool _isRegistered;

    public bool IsRegistered => _isRegistered;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _windowHandle = helper.Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);
    }

    public bool RegisterHotkey(ModifierKeys modifiers, Key key, Action action)
    {
        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("HotkeyManager not initialized. Call Initialize first.");

        _hotkeyAction = action;

        // Unregister existing hotkey
        if (_isRegistered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
        }

        uint mod = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mod |= 0x0001;
        if (modifiers.HasFlag(ModifierKeys.Control)) mod |= 0x0002;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mod |= 0x0004;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mod |= 0x0008;
        mod |= MOD_NOREPEAT; // holding the keys must not fire a burst of price checks

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, mod, vk);
        return _isRegistered;
    }

    /// <summary>Release the key combination so other apps can use it (e.g. while Warframe isn't in front).</summary>
    public void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            _hotkeyAction?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>"Control", "Control+Shift", "Ctrl+Alt+Shift", "None" ...</summary>
    public static ModifierKeys ParseModifier(string modifier)
    {
        if (string.IsNullOrWhiteSpace(modifier))
            return DefaultModifiers;

        var result = ModifierKeys.None;
        foreach (var part in modifier.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "control":
                case "ctrl":
                    result |= ModifierKeys.Control;
                    break;
                case "alt":
                    result |= ModifierKeys.Alt;
                    break;
                case "shift":
                    result |= ModifierKeys.Shift;
                    break;
                case "none":
                    break;
                default:
                    return DefaultModifiers; // unknown value in settings.json: fall back to the default
            }
        }
        return result;
    }

    /// <summary>Inverse of ParseModifier, for settings.json.</summary>
    public static string ModifierToSetting(ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Control");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        return parts.Count == 0 ? "None" : string.Join("+", parts);
    }

    /// <summary>"Ctrl + D", "Ctrl + Shift + 1", "F5"</summary>
    public static string Display(ModifierKeys modifiers, Key key)
    {
        var keyText = key is >= Key.D0 and <= Key.D9 ? ((int)(key - Key.D0)).ToString() : key.ToString();
        var mods = ModifierToString(modifiers);
        return string.IsNullOrEmpty(mods) ? keyText : $"{mods.Replace("+", " + ")} + {keyText}";
    }

    public static Key ParseKey(string key)
    {
        if (Enum.TryParse<Key>(key, true, out var result))
            return result;
        return DefaultKey;
    }

    public static string ModifierToString(ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        return string.Join("+", parts);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_isRegistered && _windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
            }
            _source?.RemoveHook(HwndHook);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
