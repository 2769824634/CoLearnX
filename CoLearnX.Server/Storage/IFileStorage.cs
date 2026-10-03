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
    Task DeleteAsync(string key, CancellationToken ct = default)
        => Task.FromException(new NotSupportedException("This storage provider does not support file removal."));
    Task<StorageFileInfo?> GetInfoAsync(string key, CancellationToken ct = default)
        => Task.FromResult<StorageFileInfo?>(null);
}

public record StorageFileInfo(long SizeBytes);

// Azure container (or local folder) for role-application files. Not the materials store.
public interface IRoleRequestFileStorage : IFileStorage;
