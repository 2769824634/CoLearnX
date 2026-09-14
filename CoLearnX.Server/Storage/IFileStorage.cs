namespace CoLearnX.Server.Storage;

// Blob or local disk. Keep: IFileStorage
public interface IFileStorage
{
    string Provider { get; }
    bool CanIssueCloudLinks { get; }
    string? Container { get; }

    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream?> OpenAsync(string key, CancellationToken ct = default);
    Task<Uri?> TryCreateReadUriAsync(string key, TimeSpan lifetime, CancellationToken ct = default);
}
