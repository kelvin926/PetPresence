using PetPresence.Contracts;

namespace PetPresence.Desktop.Presence;

public interface IPresenceClient : IAsyncDisposable
{
    event EventHandler<PresenceUpdateDto>? FriendPresenceChanged;

    Task ConnectAsync(CancellationToken cancellationToken);
    Task UpdatePresenceAsync(PresenceUpdateDto update, CancellationToken cancellationToken);
}
