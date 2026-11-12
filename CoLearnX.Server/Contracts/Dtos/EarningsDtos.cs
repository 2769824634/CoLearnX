namespace CoLearnX.Server.Contracts.Dtos;

public record EarningRowDto(int IntakeId, string CourseCode, string CourseTitle, DateTime StartsAt,
    int LearnerCount, long GrossCredits, long Credits, bool IsReversal, DateTime CreatedAt);

public record EarningsSummaryDto(long TotalCredits, long ThisMonthCredits, int ConfirmedClasses, int? SharePercent,
    IReadOnlyList<EarningRowDto> Rows);
