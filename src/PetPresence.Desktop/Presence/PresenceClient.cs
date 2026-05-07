using Microsoft.AspNetCore.SignalR.Client;
using PetPresence.Contracts;

namespace PetPresence.Desktop.Presence;

public sealed class PresenceClient : IPresenceClient
{
    private readonly HubConnection _connection;

    public PresenceClient(Uri serverBaseUri, string userId)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(serverBaseUri, "/presence"), options =>
            {
                options.Headers.Add("X-User-Id", userId);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<PresenceUpdateDto>("FriendPresenceChanged", update =>
        {
            FriendPresenceChanged?.Invoke(this, update);
        });
    }

    public event EventHandler<PresenceUpdateDto>? FriendPresenceChanged;

    public Task ConnectAsync(CancellationToken cancellationToken) => _connection.StartAsync(cancellationToken);

    public Task UpdatePresenceAsync(PresenceUpdateDto update, CancellationToken cancellationToken) =>
        _connection.InvokeAsync("UpdatePresence", update, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
