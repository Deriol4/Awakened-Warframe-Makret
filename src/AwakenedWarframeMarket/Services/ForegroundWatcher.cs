using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AwakenedWarframeMarket.Services;

/// <summary>
/// Tracks which window is in front: Warframe, one of ours, or something else.
/// Uses a WinEvent hook (no polling); events arrive on the thread that called Start (the UI thread).
/// </summary>
public sealed class ForegroundWatcher : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // Kept in a field so the GC doesn't collect the delegate while Windows still calls it
    private readonly WinEventDelegate _callback;
    private readonly int _ownProcessId = Environment.ProcessId;
    private IntPtr _hook;

    public ForegroundWatcher()
    {
        _callback = OnForegroundChanged;
    }

    /// <summary>Raised when WarframeInFront or OwnWindowInFront changes.</summary>
    public event Action? Changed;

    public bool WarframeInFront { get; private set; }
    public bool OwnWindowInFront { get; private set; }

    public void Start()
    {
        if (_hook == IntPtr.Zero)
            _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _callback, 0, 0, WINEVENT_OUTOFCONTEXT);
        Evaluate(GetForegroundWindow());
    }

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        => Evaluate(hwnd);

    private void Evaluate(IntPtr hwnd)
    {
        bool warframe = false, own = false;

        if (hwnd != IntPtr.Zero)
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == _ownProcessId)
                own = true;
            else
                warframe = IsWarframe(pid);
        }

        if (warframe == WarframeInFront && own == OwnWindowInFront)
            return;

        WarframeInFront = warframe;
        OwnWindowInFront = own;
        Changed?.Invoke();
    }

    // The game's process is "Warframe.x64" (the launcher is "Launcher" and doesn't count)
    private static bool IsWarframe(uint pid)
    {
        if (pid == 0)
            return false;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName.StartsWith("Warframe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
