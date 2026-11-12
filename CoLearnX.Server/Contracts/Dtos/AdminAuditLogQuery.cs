using System.ComponentModel.DataAnnotations;

namespace CoLearnX.Server.Contracts.Dtos;

public sealed class AdminAuditLogQuery
{
    [Range(1, 500)]
    public int Limit { get; init; } = 100;
    [Range(1, int.MaxValue)]
    public int? BeforeId { get; init; }
    [MaxLength(128)]
    public string? EntityType { get; init; }
    [MaxLength(32)]
    public string? Result { get; init; }
    [RegularExpression("^(Admin|User)$")]
    public string? ActorType { get; init; }
}
