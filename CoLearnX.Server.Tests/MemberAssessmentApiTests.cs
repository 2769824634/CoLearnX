using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class MemberAssessmentApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public MemberAssessmentApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Member_sees_assessments_and_only_their_own_grade_for_the_enrolled_intake()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await db.Enrollments.Include(item => item.CourseSession)
            .FirstAsync(item => item.User.Email == SeedData.MemberEmail
                && item.Status == EnrollmentStatus.Active
                && item.CourseSession.CourseIntake.Trainer.Email == SeedData.TrainerEmail);
        var title = $"Visible assessment {Guid.NewGuid():N}";

        using var create = await trainer.PostAsJsonAsync(
            $"/api/trainer/intakes/{enrollment.CourseSession.CourseIntakeId}/assessments",
            new CreateAssessmentRequest(title, 100, 60, DateTime.UtcNow.AddDays(2)));
        create.EnsureSuccessStatusCode();
        var assessment = await create.Content.ReadFromJsonAsync<AssessmentDto>(ApiJson.Options);
        Assert.NotNull(assessment);

        using var beforeGrade = await member.GetAsync($"/api/enrollments/{enrollment.Id}/assessments");
        Assert.Equal(HttpStatusCode.OK, beforeGrade.StatusCode);
        var visible = await beforeGrade.Content.ReadFromJsonAsync<JsonArray>(ApiJson.Options);
        var pending = Assert.Single(visible!, item => item!["id"]!.GetValue<int>() == assessment.Id)!;
        Assert.Equal("Not graded", pending["status"]!.GetValue<string>());
        Assert.Null(pending["score"]);
        Assert.Null(pending["feedback"]);

        using var grade = await trainer.PutAsJsonAsync(
            $"/api/trainer/assessments/{assessment.Id}/grades/{enrollment.Id}",
            new GradeAssessmentRequest(72, "Clear evidence."));
        grade.EnsureSuccessStatusCode();

        var graded = await member.GetFromJsonAsync<JsonArray>(
            $"/api/enrollments/{enrollment.Id}/assessments", ApiJson.Options);
        var result = Assert.Single(graded!, item => item!["id"]!.GetValue<int>() == assessment.Id)!;
        Assert.Equal("Passed", result["status"]!.GetValue<string>());
        Assert.Equal(72, result["score"]!.GetValue<int>());
        Assert.Equal("Clear evidence.", result["feedback"]!.GetValue<string>());

        var otherEnrollmentId = await db.Enrollments
            .Where(item => item.User.Email == SeedData.MemberEmail && item.Id != enrollment.Id
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed))
            .Select(item => item.Id)
            .FirstAsync();
        var other = await member.GetFromJsonAsync<JsonArray>(
            $"/api/enrollments/{otherEnrollmentId}/assessments", ApiJson.Options);
        Assert.DoesNotContain(other!, item => item!["id"]!.GetValue<int>() == assessment.Id);
    }
}
