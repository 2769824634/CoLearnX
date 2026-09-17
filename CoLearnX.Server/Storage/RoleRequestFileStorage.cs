namespace CoLearnX.Server.Storage;

public sealed class RoleRequestFileStorage(IFileStorage inner) : IRoleRequestFileStorage
{
    public string Provider => inner.Provider;
    public bool CanIssueCloudLinks => inner.CanIssueCloudLinks;
    public string? Container => inner.Container;

    public Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        => inner.SaveAsync(key, content, contentType, ct);

    public Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
        => inner.OpenAsync(key, ct);

    public Task<Uri?> TryCreateReadUriAsync(string key, TimeSpan lifetime, CancellationToken ct = default)
        => inner.TryCreateReadUriAsync(key, lifetime, ct);
}
