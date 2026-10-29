using CoLearnX.Server.Controllers;
using CoLearnX.Server.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Net;

namespace CoLearnX.Server.Tests;

public sealed class StaffDeliveryRouteTests
{
    [Theory]
    [InlineData("Complete", "enrollments/{enrollmentId:int}/complete")]
    [InlineData("Reservations", "intakes/{courseIntakeId:int}/reservations")]
    [InlineData("SaveAttendance", "intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}/attendance")]
    [InlineData("Learners", "intakes/{courseIntakeId:int}/learners")]
    [InlineData("Assessments", "intakes/{courseIntakeId:int}/assessments")]
    [InlineData("CreateAssessment", "intakes/{courseIntakeId:int}/assessments")]
    [InlineData("Grade", "assessments/{assessmentId:int}/grades/{enrollmentId:int}")]
    [InlineData("CertificateRequests", "certificate-requests")]
    [InlineData("ReviewCertificate", "certificate-requests/{certificateRequestId:int}/review")]
    public void Staff_delivery_controller_exposes_the_complete_operations_workspace(string methodName, string template)
    {
        var method = typeof(StaffDeliveryResourcesController).GetMethod(methodName);

        Assert.NotNull(method);
        var route = Assert.Single(method!.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>());
        Assert.Equal(template, route.Template);
    }
}

public sealed class StaffDeliveryAuthorizationTests(CoLearnXApiFactory factory)
    : IClassFixture<CoLearnXApiFactory>
{
    [Fact]
    public async Task Staff_delivery_routes_reject_anonymous_and_regular_trainer_tokens()
    {
        using var anonymous = ApiClient.Anonymous(factory);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        const string path = "/api/staff/intakes/1/learners";

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Contains((await trainer.GetAsync(path)).StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }
}
