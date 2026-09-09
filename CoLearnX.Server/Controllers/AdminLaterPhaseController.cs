using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
[LaterPhaseApiErrors]
public sealed class AdminLaterPhaseController(
    IMaterialVersionService materials,
    ICertificateWorkflowService certificates,
    IAdminFinanceService finance) : ControllerBase
{
    [HttpGet("material-versions")]
    public async Task<ActionResult<IReadOnlyList<MaterialVersionDto>>> MaterialVersions([FromQuery] string? status,
        CancellationToken ct)
        => Ok(await materials.ListForAdminAsync(status, ct));

    [HttpPost("material-versions/{versionId:int}/review")]
    public async Task<ActionResult<MaterialVersionDto>> ReviewMaterial(int versionId, [FromBody] WorkflowReviewRequest request,
        CancellationToken ct)
        => Ok(await materials.ReviewAsync(User.GetAdminAccountId(), versionId, request, ct));

    [HttpGet("certificate-requests")]
    public async Task<ActionResult<IReadOnlyList<CertificateRequestDto>>> CertificateRequests([FromQuery] string? status,
        CancellationToken ct)
        => Ok(await certificates.ListForAdminAsync(status, ct));

    [HttpPost("certificate-requests/{certificateRequestId:int}/review")]
    public async Task<ActionResult<CertificateRequestDto>> ReviewCertificate(int certificateRequestId,
        [FromBody] WorkflowReviewRequest request, CancellationToken ct)
        => Ok(await certificates.AdminReviewAsync(User.GetAdminAccountId(), certificateRequestId, request, ct));

    [HttpPost("credits/adjustments")]
    public async Task<ActionResult<AdminCreditMutationDto>> AdjustCredits([FromBody] AdminCreditAdjustmentRequest request,
        CancellationToken ct)
        => Ok(await finance.AdjustAsync(User.GetAdminAccountId(), request, ct));

    [HttpGet("disputes")]
    public async Task<ActionResult<IReadOnlyList<DisputeDto>>> Disputes([FromQuery] string? status, CancellationToken ct)
        => Ok(await finance.ListDisputesAsync(status, ct));

    [HttpPost("disputes/{disputeId:int}/review")]
    public async Task<ActionResult<DisputeDto>> ReviewDispute(int disputeId,
        [FromBody] AdminDisputeReviewRequest request, CancellationToken ct)
        => Ok(await finance.ReviewDisputeAsync(User.GetAdminAccountId(), disputeId, request, ct));
}
