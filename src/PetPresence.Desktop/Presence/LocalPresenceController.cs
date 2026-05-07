using System.Windows;
using PetPresence.Contracts;
using PetPresence.Desktop.Activity;
using PetPresence.Desktop.Overlay;
using PetPresence.Desktop.Privacy;

namespace PetPresence.Desktop.Presence;

public sealed class LocalPresenceController : IAsyncDisposable
{
    private readonly string _userId;
    private readonly FriendPetViewModel _ownPet;
    private readonly IForegroundWindowReader _foregroundWindowReader;
    private readonly IIdleTimeReader _idleTimeReader;
    private readonly IAudioSessionReader _audioSessionReader;
    private readonly ActivityClassifier _classifier;
    private readonly AudioAwareActivityClassifier _audioAwareClassifier;
    private readonly ActivityStabilizer _stabilizer;
    private readonly PrivacyFilter _privacyFilter;
    private readonly PrivacySettings _privacySettings;
    private readonly IPresenceClient? _presenceClient;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _minimumSendInterval;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _minimumBubbleDisplay;
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private PresenceStatus? _lastSentStatus;
    private DateTimeOffset _lastSentAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastBubbleChangedAt = DateTimeOffset.MinValue;
    private ActivityState _lastRenderedState = ActivityState.Unknown;

    public LocalPresenceController(
        string userId,
        FriendPetViewModel ownPet,
        IForegroundWindowReader foregroundWindowReader,
        IIdleTimeReader idleTimeReader,
        IAudioSessionReader audioSessionReader,
        ActivityClassifier classifier,
        AudioAwareActivityClassifier audioAwareClassifier,
        ActivityStabilizer stabilizer,
        PrivacyFilter privacyFilter,
        PrivacySettings privacySettings,
        IPresenceClient? presenceClient = null,
        TimeSpan? pollInterval = null,
        TimeSpan? minimumSendInterval = null,
        TimeSpan? heartbeatInterval = null,
        TimeSpan? minimumBubbleDisplay = null)
    {
        _userId = userId;
        _ownPet = ownPet;
        _foregroundWindowReader = foregroundWindowReader;
        _idleTimeReader = idleTimeReader;
        _audioSessionReader = audioSessionReader;
        _classifier = classifier;
        _audioAwareClassifier = audioAwareClassifier;
        _stabilizer = stabilizer;
        _privacyFilter = privacyFilter;
        _privacySettings = privacySettings;
        _presenceClient = presenceClient;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _minimumSendInterval = minimumSendInterval ?? TimeSpan.FromSeconds(10);
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30);
        _minimumBubbleDisplay = minimumBubbleDisplay ?? TimeSpan.FromSeconds(5);
    }

    public void Start()
    {
        _loop ??= RunAsync(_stop.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _stop.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await PollOnceAsync(DateTimeOffset.UtcNow, cancellationToken);
        }
    }

    internal async Task PollOnceAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var snapshot = _foregroundWindowReader.Read();
        var idleTime = _idleTimeReader.GetIdleTime();
        var foregroundState = _classifier.Classify(snapshot, idleTime);
        var audioAwareState = _audioAwareClassifier.Merge(foregroundState, _audioSessionReader.ReadActiveSessions());
        var privacyDecision = _privacyFilter.Apply(snapshot, audioAwareState, _privacySettings, now);
        var stableState = _stabilizer.Update(privacyDecision.State, now);

        RenderOwnPet(stableState, now);

        if (!privacyDecision.ShouldSuppress)
        {
            await SendPresenceIfDueAsync(stableState, now, cancellationToken);
        }
    }

    private void RenderOwnPet(ActivityState state, DateTimeOffset now)
    {
        if (state.Kind == _lastRenderedState.Kind && now - _lastBubbleChangedAt < _minimumBubbleDisplay)
        {
            return;
        }

        _lastRenderedState = state;
        _lastBubbleChangedAt = now;
        Dispatch(() =>
        {
            _ownPet.StatusText = state.StatusText;
            _ownPet.AnimationKey = state.AnimationKey;
        });
    }

    private async Task SendPresenceIfDueAsync(ActivityState state, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_presenceClient is null)
        {
            return;
        }

        var status = PresenceMapping.ToPresenceStatus(state.Kind);
        var statusChanged = _lastSentStatus != status;
        var minimumIntervalPassed = now - _lastSentAt >= _minimumSendInterval;
        var heartbeatDue = now - _lastSentAt >= _heartbeatInterval;

        if (!((statusChanged && minimumIntervalPassed) || heartbeatDue))
        {
            return;
        }

        var update = new PresenceUpdateDto(_userId, status, state.StatusText, state.AnimationKey, state.Confidence, now);
        await _presenceClient.UpdatePresenceAsync(update, cancellationToken);
        _lastSentStatus = status;
        _lastSentAt = now;
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
