using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAdminPricingService
{
    Task<IReadOnlyList<PriceTierDto>> ListTiersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CourseDomainDto>> ListDomainsAsync(CancellationToken ct = default);
    Task<CourseDomainDto> CreateDomainAsync(int adminAccountId, UpsertCourseDomainRequest request, CancellationToken ct = default);
    Task<CourseDomainDto> UpdateDomainAsync(int adminAccountId, int domainId, UpsertCourseDomainRequest request, CancellationToken ct = default);
    Task<CourseDomainDto> DeactivateDomainAsync(int adminAccountId, int domainId, CancellationToken ct = default);
    Task<IReadOnlyList<PricePoolRowDto>> ListPoolAsync(CancellationToken ct = default);
}

public sealed class AdminPricingService(CoLearnXDbContext db, IAuditLogService auditLogs) : IAdminPricingService
{
    public async Task<IReadOnlyList<PriceTierDto>> ListTiersAsync(CancellationToken ct = default)
        => await db.PriceTiers.AsNoTracking()
            .OrderBy(tier => tier.SortOrder)
            .Select(tier => new PriceTierDto(
                tier.Id,
                tier.Code,
                tier.TheoryMinHourlyCredits,
                tier.TheoryMaxHourlyCredits,
                tier.PracticeMinHourlyCredits,
                tier.PracticeMaxHourlyCredits,
                tier.SortOrder))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CourseDomainDto>> ListDomainsAsync(CancellationToken ct = default)
        => await db.CourseDomains.AsNoTracking()
            .OrderBy(domain => domain.PriceTier.SortOrder)
            .ThenBy(domain => domain.Name)
            .Select(domain => new CourseDomainDto(
                domain.Id,
                domain.Name,
                domain.PriceTierId,
                domain.PriceTier.Code,
                domain.IsActive,
                domain.CreatedAt,
                domain.UpdatedAt))
            .ToListAsync(ct);

    public Task<CourseDomainDto> CreateDomainAsync(int adminAccountId, UpsertCourseDomainRequest request, CancellationToken ct = default)
        => ChangeAsync(adminAccountId, null, request, deactivate: false, ct);

    public Task<CourseDomainDto> UpdateDomainAsync(int adminAccountId, int domainId, UpsertCourseDomainRequest request, CancellationToken ct = default)
        => ChangeAsync(adminAccountId, domainId, request, deactivate: false, ct);

    public Task<CourseDomainDto> DeactivateDomainAsync(int adminAccountId, int domainId, CancellationToken ct = default)
        => ChangeAsync(adminAccountId, domainId, null, deactivate: true, ct);

    public async Task<IReadOnlyList<PricePoolRowDto>> ListPoolAsync(CancellationToken ct = default)
    {
        var rows = await db.PricePools.AsNoTracking()
            .OrderBy(row => row.Subject)
            .ThenBy(row => row.CourseLevel.SortOrder)
            .Select(row => new
            {
                row.Id,
                row.SaleMode,
                row.PriceTierId,
                TierCode = row.PriceTier != null ? row.PriceTier.Code : null,
                row.CourseType,
                row.CourseLevelId,
                LevelName = row.CourseLevel.Name,
                row.MinHourlyCredits,
                row.MaxHourlyCredits,
                row.MinHours,
                row.MaxHours,
                row.MinTotalCredits,
                row.MaxTotalCredits,
                row.ContentSharePercent,
                row.DeliverySharePercent,
                row.PlatformSharePercent,
                row.Subject,
            })
            .ToListAsync(ct);

        return rows.Select(row => new PricePoolRowDto(
            row.Id,
            row.SaleMode.ToString(),
            row.PriceTierId,
            row.TierCode,
            row.CourseType.ToString(),
            row.CourseLevelId,
            row.LevelName,
            row.MinHourlyCredits,
            row.MaxHourlyCredits,
            row.MinHours,
            row.MaxHours,
            row.MinTotalCredits,
            row.MaxTotalCredits,
            row.ContentSharePercent,
            row.DeliverySharePercent,
            row.PlatformSharePercent,
            SubjectName(row.Subject))).ToList();
    }

    private Task<CourseDomainDto> ChangeAsync(int adminAccountId, int? domainId, UpsertCourseDomainRequest? request, bool deactivate, CancellationToken ct)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var domain = domainId is int id ? await RequireDomainAsync(id, ct) : new CourseDomain();
            if (domainId is not null && !domain.IsActive)
                throw new LaterPhaseException("DOMAIN_INACTIVE", "Inactive domains cannot be changed.", StatusCodes.Status409Conflict);

            if (deactivate)
            {
                domain.IsActive = false;
                domain.UpdatedAt = DateTime.UtcNow;
                await auditLogs.AppendAdminAsync(adminAccountId, "CourseDomainDeactivated", nameof(CourseDomain), domain.Id.ToString(), "Success", $"Name: {domain.Name}", ct);
            }
            else
            {
                var name = NormalizeName(request?.Name);
                var tier = await RequireTierAsync(request!.PriceTierId, ct);
                await EnsureNameAvailableAsync(name, domainId, ct);
                var now = DateTime.UtcNow;
                var reason = domainId is null ? null : ChangeReason(domain.Name, name, domain.PriceTier.Code, tier.Code);
                domain.Name = name;
                domain.PriceTierId = tier.Id;
                domain.PriceTier = tier;
                domain.UpdatedAt = now;
                if (domainId is null)
                {
                    domain.IsActive = true;
                    domain.CreatedAt = now;
                    db.CourseDomains.Add(domain);
                    await db.SaveChangesAsync(ct);
                    await auditLogs.AppendAdminAsync(adminAccountId, "CourseDomainCreated", nameof(CourseDomain), domain.Id.ToString(), "Success", $"Name: {name}; Tier: {tier.Code}", ct);
                }
                else
                {
                    await auditLogs.AppendAdminAsync(adminAccountId, "CourseDomainUpdated", nameof(CourseDomain), domain.Id.ToString(), "Success", reason, ct);
                }
            }

            await transaction.CommitAsync(ct);
            return ToDto(domain);
        });

    private async Task<CourseDomain> RequireDomainAsync(int domainId, CancellationToken ct)
        => await db.CourseDomains.Include(domain => domain.PriceTier).SingleOrDefaultAsync(domain => domain.Id == domainId, ct)
            ?? throw new LaterPhaseException("DOMAIN_NOT_FOUND", "Domain not found.", StatusCodes.Status404NotFound);

    private async Task<PriceTier> RequireTierAsync(int priceTierId, CancellationToken ct)
        => await db.PriceTiers.SingleOrDefaultAsync(tier => tier.Id == priceTierId, ct)
            ?? throw new LaterPhaseException("PRICE_TIER_NOT_FOUND", "Choose a price tier.", StatusCodes.Status400BadRequest, "priceTierId");

    private async Task EnsureNameAvailableAsync(string name, int? exceptId, CancellationToken ct)
    {
        var lowered = name.ToLowerInvariant();
        var taken = await db.CourseDomains.AnyAsync(domain => domain.Id != exceptId && domain.Name.ToLower() == lowered, ct);
        if (taken)
            throw new LaterPhaseException("DOMAIN_NAME_TAKEN", "A domain with this name already exists.", StatusCodes.Status409Conflict, "name");
    }

    private static string NormalizeName(string? raw)
    {
        var name = raw?.Trim() ?? "";
        if (name.Length is 0 or > 80)
            throw new LaterPhaseException("DOMAIN_NAME_INVALID", "Enter a domain name up to 80 characters.", StatusCodes.Status400BadRequest, "name");
        return name;
    }

    private static string? ChangeReason(string oldName, string newName, string oldTier, string newTier)
    {
        var parts = new List<string>();
        if (!string.Equals(oldName, newName, StringComparison.Ordinal))
            parts.Add($"Name: {oldName} -> {newName}");
        if (!string.Equals(oldTier, newTier, StringComparison.Ordinal))
            parts.Add($"Tier: {oldTier} -> {newTier}");
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? SubjectName(SubjectArea? subject) => subject switch
    {
        SubjectArea.DigitalTechnology => "Digital Technology",
        SubjectArea.BusinessManagement => "Business and Management",
        SubjectArea.HumanitiesHealthCreative => "Humanities, Health and Creative",
        _ => null,
    };

    private static CourseDomainDto ToDto(CourseDomain domain)
        => new(domain.Id, domain.Name, domain.PriceTierId, domain.PriceTier.Code, domain.IsActive, domain.CreatedAt, domain.UpdatedAt);
}
