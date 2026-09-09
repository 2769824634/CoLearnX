namespace CoLearnX.Server.Services;

public sealed class CourseException(
    string code,
    string message,
    int statusCode = 400,
    string? field = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; } = field is null
        ? null
        : new Dictionary<string, string[]> { [field] = [message] };
}

