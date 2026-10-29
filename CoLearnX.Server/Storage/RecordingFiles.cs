namespace CoLearnX.Server.Storage;

internal static class RecordingFiles
{
    public const long MaxBytes = 250L * 1024 * 1024;
    public const long MaxRequestBytes = MaxBytes + 1024 * 1024;

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov",
    };

    public static string RequireSafeExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !Allowed.Contains(extension))
            throw new InvalidOperationException("Allowed video types: MP4, WebM, MOV.");
        return extension.ToLowerInvariant();
    }

    public static string ContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mov" => "video/quicktime",
        _ => "application/octet-stream",
    };

    public static void RequireVideoContentType(string extension, string? contentType)
    {
        if (!string.Equals(ContentType(extension), contentType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The uploaded file content type does not match its video extension.");
    }

    public static string DownloadName(string title, string storageKey)
    {
        var stem = string.Join("_", title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return (string.IsNullOrWhiteSpace(stem) ? "recording" : stem) + Path.GetExtension(storageKey);
    }
}
