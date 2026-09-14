using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace CoLearnX.Server.Storage;

// Azure Blob when Storage:ConnectionString is set.
public sealed class AzureBlobFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly BlobContainerClient _container = new(
        options.Value.ConnectionString,
        string.IsNullOrWhiteSpace(options.Value.Container) ? "materials" : options.Value.Container);

    public string Provider => "Azure";
    public bool CanIssueCloudLinks => _container.GetBlobClient("_").CanGenerateSasUri;
    public string? Container => _container.Name;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = _container.GetBlobClient(key);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                },
            },
            ct);
    }

    public async Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(key);
        if (!await blob.ExistsAsync(ct))
            return null;
        var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task<Uri?> TryCreateReadUriAsync(string key, TimeSpan lifetime, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(key);
        if (!CanIssueCloudLinks || !await blob.ExistsAsync(ct))
            return null;

        var builder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = key,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(lifetime),
        };
        builder.SetPermissions(BlobSasPermissions.Read);
        return blob.GenerateSasUri(builder);
    }
}
