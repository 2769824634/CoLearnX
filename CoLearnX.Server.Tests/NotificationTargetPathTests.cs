using CoLearnX.Server.Services;

namespace CoLearnX.Server.Tests;

public class NotificationTargetPathTests
{
    [Theory]
    [InlineData("CertificateSubmitted", null, "/member/badges")]
    [InlineData("CertificateIssued", null, "/member/badges")]
    [InlineData("N-01", null, "/member/programs?tab=reserved")]
    [InlineData("N-class-confirmed", null, "/member/programs?tab=active")]
    [InlineData("N-class-reminder", null, "/member/programs?tab=active")]
    [InlineData("N-class-cancelled", null, "/member/programs?tab=history")]
    [InlineData("N-postponement-offered", null, "/member/programs?tab=history")]
    [InlineData("N-hold-released", null, "/member/payment")]
    [InlineData("N-withdraw-70", null, "/member/payment")]
    [InlineData("N-topup", null, "/member/payment")]
    [InlineData("N-09", null, "/member/disputes")]
    [InlineData("N-session-full", null, "/trainer/courses")]
    [InlineData("N-intake-cancelled-creator", null, "/creator/courses")]
    [InlineData("N-learner-withdrew", null, "/trainer/learners")]
    [InlineData("N-course-published", null, "/creator/approvals")]
    [InlineData("N-course-rejected", null, "/creator/approvals")]
    [InlineData("N-intake-published", null, "/trainer/courses")]
    [InlineData("N-course-invitation", null, "/trainer/invitations")]
    [InlineData("N-course-trainer-removed", null, "/trainer/invitations")]
    [InlineData("N-course-invitation-accepted", null, "/creator/courses")]
    [InlineData("N-course-invitation-declined", null, "/creator/courses")]
    [InlineData("N-intake-completed", null, "/trainer/courses")]
    [InlineData("N-class-completed", null, "/member/programs?tab=completed")]
    [InlineData("N-earnings-pending", null, "/trainer/earnings")]
    [InlineData("N-earnings-pending-creator", null, "/creator/earnings")]
    [InlineData("N-earnings-pending", 12, "/trainer/earnings")]
    [InlineData("N-intake-invitation-accepted", null, "/creator/courses")]
    [InlineData("N-intake-invitation-declined", null, "/creator/courses")]
    [InlineData("N-role-approved", null, null)]
    [InlineData("N-payout-paid", null, null)]
    [InlineData("N-payout-rejected", null, null)]
    [InlineData("N-role-rejected", null, null)]
    [InlineData("https://example.com", null, null)]
    public void Code_maps_to_the_inbox_path(string code, int? intakeId, string? path)
        => Assert.Equal(path, NotificationTargetPath.For(code, intakeId));

    [Theory]
    [InlineData("N-intake-confirmed", 12, "/trainer/courses/intakes/12")]
    [InlineData("N-intake-cancelled-creator", 12, "/creator/intakes/12")]
    [InlineData("N-learner-withdrew", 12, "/trainer/courses/intakes/12")]
    [InlineData("N-intake-published", 12, "/trainer/courses/intakes/12")]
    [InlineData("N-intake-completed", 12, "/trainer/courses/intakes/12")]
    [InlineData("N-intake-invitation-accepted", 12, "/creator/intakes/12")]
    public void Intake_codes_include_the_owned_resource(string code, int intakeId, string path)
        => Assert.Equal(path, NotificationTargetPath.For(code, intakeId));

    [Fact]
    public void Class_completed_selects_the_enrollment()
        => Assert.Equal("/member/programs?tab=completed&enrollmentId=7", NotificationTargetPath.For("N-class-completed", 12, 7));
}
