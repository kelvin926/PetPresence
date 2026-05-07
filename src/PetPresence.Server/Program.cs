using PetPresence.Contracts;
using PetPresence.Server.Auth;
using PetPresence.Server.Friends;
using PetPresence.Server.Hubs;
using PetPresence.Server.Presence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<DevelopmentUserContext>();
builder.Services.AddSingleton<FriendshipStore>();
builder.Services.AddSingleton(new PresenceStore(TimeSpan.FromSeconds(75)));
builder.Services.AddHostedService<PresenceCleanupService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "PetPresence.Server", version = "v2" }));

app.MapPost("/friends/request", (FriendRequestDto request, HttpContext http, DevelopmentUserContext users, FriendshipStore friends) =>
{
    var userId = users.GetRequiredUserId(http);
    return Results.Ok(friends.RequestFriend(userId, request.FriendUserId));
});

app.MapPost("/friends/accept", (FriendRequestDto request, HttpContext http, DevelopmentUserContext users, FriendshipStore friends) =>
{
    var userId = users.GetRequiredUserId(http);
    return Results.Ok(friends.AcceptFriend(userId, request.FriendUserId));
});

app.MapPost("/friends/block", (FriendRequestDto request, HttpContext http, DevelopmentUserContext users, FriendshipStore friends) =>
{
    var userId = users.GetRequiredUserId(http);
    return Results.Ok(friends.BlockFriend(userId, request.FriendUserId));
});

app.MapGet("/friends", (HttpContext http, DevelopmentUserContext users, FriendshipStore friends) =>
{
    var userId = users.GetRequiredUserId(http);
    return Results.Ok(friends.GetFriends(userId));
});

app.MapHub<PresenceHub>("/presence");

app.Run();
