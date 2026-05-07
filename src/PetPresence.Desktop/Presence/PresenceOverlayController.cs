using System.Windows;
using PetPresence.Contracts;
using PetPresence.Desktop.Overlay;

namespace PetPresence.Desktop.Presence;

public sealed class PresenceOverlayController
{
    private readonly OverlayViewModel _overlayViewModel;

    public PresenceOverlayController(OverlayViewModel overlayViewModel)
    {
        _overlayViewModel = overlayViewModel;
    }

    public void Attach(IPresenceClient client)
    {
        client.FriendPresenceChanged += (_, update) => ApplyFriendPresence(update);
    }

    public void ApplyFriendPresence(PresenceUpdateDto update)
    {
        Dispatch(() =>
        {
            var pet = _overlayViewModel.GetOrAddFriend(update.UserId, update.UserId);
            pet.StatusText = update.StatusText;
            pet.AnimationKey = update.AnimationKey;
        });
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
