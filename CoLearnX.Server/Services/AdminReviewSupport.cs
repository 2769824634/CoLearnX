namespace CoLearnX.Server.Services;

internal enum AdminReviewDecision
{
    Approve,
    Reject,
}

internal static class AdminReviewDecisionParser
{
    public static AdminReviewDecision Parse(string decision)
    {
        if (Enum.TryParse<AdminReviewDecision>(decision?.Trim(), true, out var parsed))
            return parsed;

        throw new AdminReviewValidationException(
            "INVALID_REVIEW_DECISION",
            "Decision must be either Approve or Reject.");
    }
}

public sealed class AdminReviewValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class AdminReviewConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
