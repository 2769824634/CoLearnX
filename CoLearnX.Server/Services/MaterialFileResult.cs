namespace CoLearnX.Server.Services;

public sealed record MaterialFileResult(Stream Stream, string ContentType, string DownloadName);
