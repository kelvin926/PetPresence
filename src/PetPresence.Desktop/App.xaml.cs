using System.Windows;
using PetPresence.Desktop.Overlay;
using PetPresence.Desktop.Diagnostics;

namespace PetPresence.Desktop;

public partial class App : Application
{
    private OverlayWindow? _overlayWindow;
    private TrayIconHost? _trayIconHost;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        new CrashLogService().RegisterGlobalHandlers();

        var viewModel = new OverlayViewModel();
        viewModel.Friends.Add(new FriendPetViewModel
        {
            UserId = "local-user",
            DisplayName = "나",
            StatusText = "상태 확인 중...",
            AnimationKey = "idle",
            X = 120,
            Y = 120
        });

        _overlayWindow = new OverlayWindow { DataContext = viewModel };
        _overlayWindow.Show();

        _trayIconHost = new TrayIconHost(_overlayWindow);
        _trayIconHost.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconHost?.Dispose();
        base.OnExit(e);
    }
}
