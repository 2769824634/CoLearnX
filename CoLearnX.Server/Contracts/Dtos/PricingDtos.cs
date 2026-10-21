namespace CoLearnX.Server.Contracts.Dtos;

public record PriceTierDto(
    int Id,
    string Code,
    int TheoryMinHourlyCredits,
    int TheoryMaxHourlyCredits,
    int PracticeMinHourlyCredits,
    int PracticeMaxHourlyCredits,
    int SortOrder);

public record CourseDomainDto(
    int Id,
    string Name,
    int PriceTierId,
    string TierCode,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UpsertCourseDomainRequest(string? Name, int PriceTierId);

public record PricePoolRowDto(
    int Id,
    string SaleMode,
    int? PriceTierId,
    string? TierCode,
    string CourseType,
    int CourseLevelId,
    string LevelName,
    int? MinHourlyCredits,
    int? MaxHourlyCredits,
    int? MinHours,
    int? MaxHours,
    int? MinTotalCredits,
    int? MaxTotalCredits,
    int ContentSharePercent,
    int DeliverySharePercent,
    int PlatformSharePercent);
