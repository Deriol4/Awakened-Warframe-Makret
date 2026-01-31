using System.Windows;

namespace AwakenedWarframeMarket;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Set shutdown mode to explicit
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
}
