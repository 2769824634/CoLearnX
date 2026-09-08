namespace CoLearnX.Server.Domain.Enums;

// JWT role claim values. Shared: AppRole
public enum AppRole
{
    Member = 1,
    Trainer = 2,
    Creator = 3
}

public enum CourseStatus
{
    Draft = 0,
    PendingApproval = 1,
    Published = 2,
    Archived = 3,
    Rejected = 4
}

public enum CourseIntakeStatus
{
    Draft = 0,
    PendingApproval = 1,
    Published = 2,
    InProgress = 3,
    Completed = 4,
    Rejected = 5,
    Cancelled = 6
}

public enum CourseIntakeApplicationKind
{
    Initial = 0,
    Change = 1
}

public enum CourseIntakeApplicationStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2
}

public enum MaterialStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3
}

public enum EnrollmentStatus
{
    Active = 0,
    Completed = 1,
    Cancelled = 2,
    Refunded = 3
}

public enum RoleRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum CreditTransactionType
{
    TopUp = 1,
    Enrolment = 2,
    Refund = 3,
    Royalty = 4,
    AdminAdjustment = 5
}

public enum DisputeStatus
{
    Open = 0,
    ResolvedRefund = 1,
    Rejected = 2
}

public enum AttendanceStatus
{
    Present = 0,
    Absent = 1,
    Late = 2
}

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}
