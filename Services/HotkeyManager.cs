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

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, mod, vk);
        return _isRegistered;
    }

    public bool UpdateHotkey(ModifierKeys modifiers, Key key)
    {
        if (_hotkeyAction == null)
            throw new InvalidOperationException("No hotkey action registered");

        return RegisterHotkey(modifiers, key, _hotkeyAction);
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

    public static ModifierKeys ParseModifier(string modifier)
    {
        return modifier.ToLower() switch
        {
            "control" or "ctrl" => ModifierKeys.Control,
            "alt" => ModifierKeys.Alt,
            "shift" => ModifierKeys.Shift,
            "control+shift" or "ctrl+shift" => ModifierKeys.Control | ModifierKeys.Shift,
            "control+alt" or "ctrl+alt" => ModifierKeys.Control | ModifierKeys.Alt,
            "alt+shift" => ModifierKeys.Alt | ModifierKeys.Shift,
            _ => ModifierKeys.Control
        };
    }

    public static Key ParseKey(string key)
    {
        if (Enum.TryParse<Key>(key, true, out var result))
            return result;
        return Key.D;
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
