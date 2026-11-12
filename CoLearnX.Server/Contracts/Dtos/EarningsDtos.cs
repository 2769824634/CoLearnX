using System.Text.Json.Serialization;

namespace CoLearnX.Server.Contracts.Dtos;

public record EarningRowDto(int IntakeId, string CourseCode, string CourseTitle, DateTime StartsAt,
    int LearnerCount, long GrossCredits, long Credits, bool IsReversal, DateTime CreatedAt);
public record EarningClassDto(int CourseIntakeId, string CourseCode, string CourseTitle, DateTime EndsAt,
    int Learners, long Credits, int? SharePercent, string Status, DateTime AvailableAt);
public record PayoutRequestDto(int Id, long Credits, string PayoutMethod, string Status,
    DateTime RequestedAt, DateTime? ReviewedAt, string? Reference);
public record AdminPayoutDto(int Id, int UserId, string UserName, string UserEmail, string Role, long Credits,
    string PayoutMethod, string Status, DateTime RequestedAt, DateTime? ReviewedAt, string? Reference);
public record EarningsSummaryDto(long TotalCredits, long PendingCredits, long AvailableCredits,
    long RequestedCredits, long PaidCredits, int MinimumPayoutCredits, decimal CashPerCredit,
    IReadOnlyList<EarningClassDto> Classes, IReadOnlyList<PayoutRequestDto> Payouts,
    long ThisMonthCredits, int ConfirmedClasses, int? SharePercent, IReadOnlyList<EarningRowDto> Rows);
public record CreatePayoutRequest([property: JsonRequired] string? PayoutMethod);
public record ReviewPayoutRequest([property: JsonRequired] string? Decision, string? Reference);
