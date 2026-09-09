using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/creator/courses")]
[Authorize(Policy = CreatorAuthorization.PolicyName)]
[CreatorCourseApiErrors]
public sealed class CreatorCoursesController(ICourseService courses) : ControllerBase
{
    [HttpGet("options")]
    public async Task<ActionResult<CreatorCourseOptionsDto>> Options(CancellationToken ct)
        => Ok(await courses.GetCreatorOptionsAsync(User.GetUserId(), ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreatorCourseDto>>> List(CancellationToken ct)
        => Ok(await courses.ListForCreatorAsync(User.GetUserId(), ct));

    [HttpGet("{courseId:int}")]
    public async Task<ActionResult<CreatorCourseDto>> Get(int courseId, CancellationToken ct)
        => Ok(await courses.GetForCreatorAsync(User.GetUserId(), courseId, ct));

    [HttpPost]
    public async Task<ActionResult<CreatorCourseDto>> Create(
        [FromBody] CreateCreatorCourseRequest request,
        CancellationToken ct)
    {
        var course = await courses.CreateForCreatorAsync(User.GetUserId(), request, ct);
        return Created($"/api/creator/courses/{course.Id}", course);
    }

    [HttpPut("{courseId:int}")]
    public async Task<ActionResult<CreatorCourseDto>> Update(
        int courseId,
        [FromBody] UpdateCreatorCourseRequest request,
        CancellationToken ct)
        => Ok(await courses.UpdateForCreatorAsync(User.GetUserId(), courseId, request, ct));

    [HttpPost("{courseId:int}/submit")]
    public async Task<ActionResult<CreatorCourseDto>> Submit(int courseId, CancellationToken ct)
        => Ok(await courses.SubmitForCreatorAsync(User.GetUserId(), courseId, ct));
}
