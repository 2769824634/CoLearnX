using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/staff/courses")]
[Authorize(Policy = StaffAuthorization.PolicyName)]
[CreatorCourseApiErrors]
public sealed class StaffCoursesController(
    IStaffCourseService courses,
    ICourseIntakeService intakes,
    ITrainerDeliveryService delivery,
    RecommendationService recommendations) : ControllerBase
{
    [HttpGet("options")]
    public async Task<ActionResult<CreatorCourseOptionsDto>> Options(CancellationToken ct)
        => Ok(await courses.OptionsAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreatorCourseDto>>> List(CancellationToken ct)
        => Ok(await courses.ListAsync(User.GetStaffAccountId(), ct));

    [HttpGet("interests")]
    public async Task<ActionResult<IReadOnlyList<InterestDto>>> Interests(CancellationToken ct)
        => Ok(await recommendations.InterestTreeAsync(ct));

    [HttpGet("{courseId:int}")]
    public async Task<ActionResult<CreatorCourseDto>> Get(int courseId, CancellationToken ct)
        => Ok(await courses.GetAsync(User.GetStaffAccountId(), courseId, ct));

    [HttpPost]
    public async Task<ActionResult<CreatorCourseDto>> Create(CreateCreatorCourseRequest request, CancellationToken ct)
    {
        var course = await courses.CreateAsync(User.GetStaffAccountId(), request, ct);
        return Created($"/api/staff/courses/{course.Id}", course);
    }

    [HttpPut("{courseId:int}")]
    public async Task<ActionResult<CreatorCourseDto>> Update(
        int courseId, UpdateCreatorCourseRequest request, CancellationToken ct)
        => Ok(await courses.UpdateAsync(User.GetStaffAccountId(), courseId, request, ct));

    [HttpPost("{courseId:int}/submit")]
    public async Task<ActionResult<CreatorCourseDto>> Submit(int courseId, CancellationToken ct)
        => Ok(await courses.SubmitAsync(User.GetStaffAccountId(), courseId, ct));

    [HttpPost("{courseId:int}/intakes")]
    public async Task<ActionResult<CourseIntakeDetailDto>> CreateIntake(
        int courseId,
        CreateCourseIntakeRequest request,
        CancellationToken ct)
    {
        var intake = await intakes.CreateForStaffAsync(User.GetStaffAccountId(), courseId, request, ct);
        return Created($"/api/staff/intakes/{intake.Id}", intake);
    }

    [HttpGet("/api/staff/intakes")]
    public async Task<ActionResult<IReadOnlyList<CourseIntakeSummaryDto>>> ListIntakes(CancellationToken ct)
        => Ok(await intakes.ListForStaffAsync(User.GetStaffAccountId(), ct));

    [HttpGet("/api/staff/intakes/{courseIntakeId:int}")]
    public async Task<ActionResult<CourseIntakeDetailDto>> GetIntake(int courseIntakeId, CancellationToken ct)
        => Ok(await intakes.GetForStaffAsync(User.GetStaffAccountId(), courseIntakeId, ct));

    [HttpPost("/api/staff/intakes/{courseIntakeId:int}/sessions")]
    public async Task<ActionResult<CourseIntakeDetailDto>> AddSession(
        int courseIntakeId,
        CreateCourseSessionRequest request,
        CancellationToken ct)
        => Ok(await intakes.AddSessionForStaffAsync(User.GetStaffAccountId(), courseIntakeId, request, ct));

    [HttpPost("/api/staff/intakes/{courseIntakeId:int}/publish")]
    public async Task<ActionResult<CourseIntakeDetailDto>> PublishIntake(
        int courseIntakeId,
        SubmitCourseIntakeRequest request,
        CancellationToken ct)
        => Ok(await intakes.PublishForStaffAsync(User.GetStaffAccountId(), courseIntakeId, request.Version, ct));

    [HttpPatch("/api/staff/intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}/delivery")]
    public async Task<ActionResult<CourseIntakeDetailDto>> UpdateDelivery(
        int courseIntakeId,
        int courseSessionId,
        UpdateSessionDeliveryRequest request,
        CancellationToken ct)
        => Ok(await delivery.UpdateMeetingLinkAsync(User.GetUserId(), courseIntakeId, courseSessionId, request, ct));
}
