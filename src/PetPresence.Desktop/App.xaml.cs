using System.Windows;
using PetPresence.Desktop.Overlay;
using PetPresence.Desktop.Diagnostics;
using PetPresence.Desktop.Activity;
using PetPresence.Desktop.Presence;
using PetPresence.Desktop.Privacy;

namespace PetPresence.Desktop;

public partial class App : Application
{
    private OverlayWindow? _overlayWindow;
    private TrayIconHost? _trayIconHost;
    private LocalPresenceController? _localPresenceController;
    private IPresenceClient? _presenceClient;
    private PresenceOverlayController? _presenceOverlayController;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        new CrashLogService().RegisterGlobalHandlers();

        var viewModel = new OverlayViewModel();
        var ownPet = viewModel.GetOrAddFriend("local-user", "나");

        _overlayWindow = new OverlayWindow { DataContext = viewModel };
        _overlayWindow.Show();

        _trayIconHost = new TrayIconHost(_overlayWindow);
        _trayIconHost.Show();

        ConfigurePresenceClient(viewModel);

        _localPresenceController = new LocalPresenceController(
            userId: "local-user",
            ownPet: ownPet,
            foregroundWindowReader: new ForegroundWindowReader(),
            idleTimeReader: new IdleTimeReader(),
            audioSessionReader: new WindowsAudioSessionReader(),
            classifier: new ActivityClassifier(),
            audioAwareClassifier: new AudioAwareActivityClassifier(),
            stabilizer: new ActivityStabilizer(TimeSpan.FromSeconds(3)),
            privacyFilter: new PrivacyFilter(),
            privacySettings: new PrivacySettings(),
            presenceClient: _presenceClient);
        _localPresenceController.Start();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_localPresenceController is not null)
        {
            await _localPresenceController.DisposeAsync();
        }

        if (_presenceClient is not null)
        {
            await _presenceClient.DisposeAsync();
        }

        _trayIconHost?.Dispose();
        base.OnExit(e);
    }

    private void ConfigurePresenceClient(OverlayViewModel viewModel)
    {
        var serverUrl = Environment.GetEnvironmentVariable("PETPRESENCE_SERVER_URL");
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri))
        {
            return;
        }

        _presenceClient = new PresenceClient(serverUri, "local-user");
        _presenceOverlayController = new PresenceOverlayController(viewModel);
        _presenceOverlayController.Attach(_presenceClient);
        _ = Task.Run(async () =>
        {
            try
            {
                await _presenceClient.ConnectAsync(CancellationToken.None);
            }
            catch
            {
                // The desktop overlay still works locally when the development server is unavailable.
            }
        });
    }
}
