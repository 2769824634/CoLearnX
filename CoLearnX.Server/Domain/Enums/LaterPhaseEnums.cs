namespace CoLearnX.Server.Domain.Enums;

public enum MaterialVersionStatus
{
    PendingApproval = 0,
    Approved = 1,
    Rejected = 2,
}

public enum CertificateRequestStatus
{
    Submitted = 0,
    TrainerApproved = 1,
    TrainerRejected = 2,
    AdminRejected = 3,
    Issued = 4,
}
