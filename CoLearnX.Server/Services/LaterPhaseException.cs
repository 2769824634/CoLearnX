namespace CoLearnX.Server.Services;

public sealed class LaterPhaseException(
    string code,
    string message,
    int statusCode = StatusCodes.Status400BadRequest,
    string? field = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; } = field is null
        ? null
        : new Dictionary<string, string[]> { [field] = [message] };
}
