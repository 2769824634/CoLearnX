using Microsoft.AspNetCore.Http;

namespace CoLearnX.Server.Services;

public static class PasswordResetLinks
{
    public static string? ResolveOrigin(
        string? configured,
        bool allowLoopback,
        HttpRequest? request,
        string? websiteHostName = null)
    {
        if (TryHttpsOrigin(configured, allowLoopback, out var fromConfig))
            return fromConfig;
        if (request is not null
            && string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && TryHttpsOrigin($"{Uri.UriSchemeHttps}://{request.Host.Value}", allowLoopback, out var fromRequest))
            return fromRequest;
        if (!string.IsNullOrWhiteSpace(websiteHostName)
            && TryHttpsOrigin($"{Uri.UriSchemeHttps}://{websiteHostName.Trim()}", allowLoopback, out var fromSite))
            return fromSite;
        return null;
    }

    public static bool TryHttpsOrigin(string? value, bool allowLoopback, out string origin)
    {
        origin = "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || (!allowLoopback && uri.IsLoopback))
            return false;
        origin = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }
}
