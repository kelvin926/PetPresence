using PetPresence.Server.Auth;
using PetPresence.Server.Hubs;
using PetPresence.Server.Presence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<DevelopmentUserContext>();
builder.Services.AddSingleton(new PresenceStore(TimeSpan.FromSeconds(75)));
builder.Services.AddHostedService<PresenceCleanupService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "PetPresence.Server", version = "v1" }));
app.MapHub<PresenceHub>("/presence");

app.Run();
