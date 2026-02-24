using System.Windows;
using System.Windows.Media;
using System.Threading;

namespace CrosshairFlex.Desktop;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Global\CrosshairFlex.SingleInstance";
    private const string ShowMainWindowEventName = @"Global\CrosshairFlex.ShowMainWindow";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showMainWindowEvent;
    private RegisteredWaitHandle? _showMainWindowRegistration;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            TrySignalExistingInstance();
            Shutdown();
            return;
        }

        _showMainWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowMainWindowEventName);
        _showMainWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            _showMainWindowEvent,
            (_, _) => Dispatcher.Invoke(BringMainWindowToFront),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);

        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static void TrySignalExistingInstance()
    {
        try
        {
            using var openEvent = EventWaitHandle.OpenExisting(ShowMainWindowEventName);
            openEvent.Set();
        }
        catch
        {
            // Ignore: if event doesn't exist yet we just exit.
        }
    }

    private void BringMainWindowToFront()
    {
        if (MainWindow is MainWindow mainWindow)
        {
            mainWindow.RestoreAndActivate();
            return;
        }

        MainWindow?.Show();
        MainWindow?.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showMainWindowRegistration?.Unregister(null);
        _showMainWindowRegistration = null;
        _showMainWindowEvent?.Dispose();
        _showMainWindowEvent = null;

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }
}

