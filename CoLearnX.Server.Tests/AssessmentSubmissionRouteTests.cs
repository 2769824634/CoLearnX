using System.Net;

namespace CoLearnX.Server.Tests;

public sealed class AssessmentSubmissionRouteTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public AssessmentSubmissionRouteTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_member_cannot_submit_an_assignment()
    {
        using var client = ApiClient.Anonymous(_factory);
        using var form = new MultipartFormDataContent();
        using var response = await client.PostAsync("/api/enrollments/1/assessments/1/submission", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
