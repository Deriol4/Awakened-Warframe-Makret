using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AwakenedWarframeMarket.Services;

namespace AwakenedWarframeMarket;

public partial class App : Application
{
    private const string InstanceName = "AwakenedWarframeMarket.SingleInstance";
    private const string ShowSignalName = "AwakenedWarframeMarket.ShowWindow";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private RegisteredWaitHandle? _showWait;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One copy at a time: a second launch just brings the running one to the front
        _instanceMutex = new Mutex(true, InstanceName, out bool firstInstance);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        if (!firstInstance)
        {
            _showSignal.Set();
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) => args.SetObserved();
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            MessageBox.Show(L.MustClose((args.ExceptionObject as Exception)?.Message ?? ""),
                "Awakened Warframe Market", MessageBoxButton.OK, MessageBoxImage.Error);

        // Closing the main window (not hiding it to the tray) exits the app
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) =>
            Dispatcher.BeginInvoke(() => window.ShowFromTray()), null, Timeout.Infinite, false);
    }

    // Keep the app running after an error in an event handler, and say what happened in plain words
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(L.KeepsRunning(e.Exception.Message),
            "Awakened Warframe Market", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _showSignal?.Dispose();
        if (_instanceMutex != null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
