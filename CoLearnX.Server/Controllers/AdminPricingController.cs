using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin/pricing")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
[LaterPhaseApiErrors]
public sealed class AdminPricingController(IAdminPricingService pricing) : ControllerBase
{
    [HttpGet("tiers")]
    public async Task<ActionResult<IReadOnlyList<PriceTierDto>>> Tiers(CancellationToken ct)
        => Ok(await pricing.ListTiersAsync(ct));

    [HttpGet("domains")]
    public async Task<ActionResult<IReadOnlyList<CourseDomainDto>>> Domains(CancellationToken ct)
        => Ok(await pricing.ListDomainsAsync(ct));

    [HttpPost("domains")]
    public async Task<ActionResult<CourseDomainDto>> Create([FromBody] UpsertCourseDomainRequest request, CancellationToken ct)
    {
        var created = await pricing.CreateDomainAsync(User.GetAdminAccountId(), request, ct);
        return Created($"/api/admin/pricing/domains/{created.Id}", created);
    }

    [HttpPut("domains/{id:int}")]
    public async Task<ActionResult<CourseDomainDto>> Update(int id, [FromBody] UpsertCourseDomainRequest request, CancellationToken ct)
        => Ok(await pricing.UpdateDomainAsync(User.GetAdminAccountId(), id, request, ct));

    [HttpPost("domains/{id:int}/deactivate")]
    public async Task<ActionResult<CourseDomainDto>> Deactivate(int id, CancellationToken ct)
        => Ok(await pricing.DeactivateDomainAsync(User.GetAdminAccountId(), id, ct));

    [HttpGet("pool")]
    public async Task<ActionResult<IReadOnlyList<PricePoolRowDto>>> Pool(CancellationToken ct)
        => Ok(await pricing.ListPoolAsync(ct));
}
