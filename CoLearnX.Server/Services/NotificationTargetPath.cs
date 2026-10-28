namespace CoLearnX.Server.Services;

public static class NotificationTargetPath
{
    public static string? For(string code, int? intakeId = null, int? enrollmentId = null) => code switch
    {
        "CertificateSubmitted" or "CertificateTrainerApproved" or "CertificateTrainerRejected"
            or "CertificateIssued" or "CertificateAdminRejected" => "/member/badges",
        "N-01" => MemberProgramsPath("reserved", enrollmentId),
        "N-class-confirmed" or "N-class-reminder" => MemberProgramsPath("active", enrollmentId),
        "N-class-cancelled" or "N-postpone-offer" or "N-postponement-offered" => MemberProgramsPath("history", enrollmentId),
        "N-hold-released" or "N-withdraw-70" or "N-topup" => "/member/payment",
        "N-session-full" or "N-session-reopened" or "N-min-reached" or "N-under-enrolled" or "N-intake-confirmed" or "N-intake-cancelled"
            => intakeId > 0 ? $"/trainer/courses/intakes/{intakeId}" : "/trainer/courses",
        "N-intake-confirmed-creator" or "N-intake-cancelled-creator"
            => intakeId > 0 ? $"/creator/intakes/{intakeId}" : "/creator/courses",
        "N-learner-withdrew" => intakeId > 0 ? $"/trainer/courses/intakes/{intakeId}" : "/trainer/learners",
        "N-course-published" or "N-course-rejected" => "/creator/approvals",
        "N-course-invitation" or "N-course-trainer-removed" => "/trainer/invitations",
        "N-course-invitation-accepted" or "N-course-invitation-declined" => "/creator/courses",
        "N-intake-published" => intakeId > 0 ? $"/trainer/courses/intakes/{intakeId}" : "/trainer/courses",
        "N-09" => "/member/disputes",
        _ => null,
    };

    private static string MemberProgramsPath(string tab, int? enrollmentId)
        => enrollmentId is > 0
            ? $"/member/programs?tab={tab}&enrollmentId={enrollmentId}"
            : $"/member/programs?tab={tab}";
}
