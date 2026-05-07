using Microsoft.AspNetCore.SignalR;

namespace PetPresence.Server.Auth;

public sealed class DevelopmentUserContext
{
    public const string HeaderName = "X-User-Id";

    private static readonly HashSet<string> KnownDevelopmentUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "local-user",
        "hyunseo",
        "friend-a",
        "friend-b"
    };

    public string GetRequiredUserId(HubCallerContext context)
    {
        var userId = context.GetHttpContext()?.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new HubException($"Missing development auth header {HeaderName}.");
        }

        return NormalizeAndValidate(userId);
    }

    public string GetRequiredUserId(HttpContext context)
    {
        var userId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new BadHttpRequestException($"Missing development auth header {HeaderName}.");
        }

        return NormalizeAndValidate(userId);
    }

    private static string NormalizeAndValidate(string userId)
    {
        var normalized = userId.Trim();
        if (normalized.Length is < 1 or > 64 || normalized.Any(char.IsWhiteSpace))
        {
            throw new HubException("Invalid development user id.");
        }

        // MVP has hard-coded test users, but allows additional local ids for manual testing.
        _ = KnownDevelopmentUsers.Contains(normalized);
        return normalized;
    }
}
