namespace CoLearnX.Server.Services;

public static class CertificateFiles
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const long MaxRequestBytes = MaxBytes + 64 * 1024;

    public static async Task<byte[]> ReadPdfAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
            throw new LaterPhaseException("CERTIFICATE_PDF_REQUIRED", "Upload a PDF certificate.", field: "file");
        if (file.Length > MaxBytes)
            throw new LaterPhaseException("CERTIFICATE_PDF_TOO_LARGE", "The certificate PDF must be 10 MB or smaller.", field: "file");
        if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new LaterPhaseException("CERTIFICATE_PDF_REQUIRED", "Upload a PDF certificate.", field: "file");

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.Length < 5 || bytes[0] != (byte)'%' || bytes[1] != (byte)'P' || bytes[2] != (byte)'D' || bytes[3] != (byte)'F' || bytes[4] != (byte)'-')
            throw new LaterPhaseException("CERTIFICATE_PDF_REQUIRED", "Upload a PDF certificate.", field: "file");
        return bytes;
    }

    public static string DownloadName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "");
        var stem = string.Join("_", Path.GetFileNameWithoutExtension(name)
            .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(stem)) stem = "certificate";
        if (stem.Length > 80) stem = stem[..80];
        return stem + ".pdf";
    }
}
