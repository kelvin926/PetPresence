var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "PetPresence.Server", version = "v0-placeholder" }));

app.Run();
