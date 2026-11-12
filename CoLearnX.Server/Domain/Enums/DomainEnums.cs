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

// Whether the Intake's Trainer has seen and agreed to the fixed session schedule.
public enum TrainerInvitationStatus
{
    NotSent = 0,
    Invited = 1,
    Accepted = 2,
    Declined = 3
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

public enum CourseTrainerStatus
{
    Invited = 0,
    Accepted = 1,
    Declined = 2,
    Removed = 3,
    Requested = 4
}

public enum CourseIntakeRequestStatus
{
    Pending = 0,
    Fulfilled = 1,
    Cancelled = 2
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
    Refunded = 3,
    Reserved = 4
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
    AdminAdjustment = 5,
    Hold = 6,
    Capture = 7,
    Release = 8,
    Forfeit = 9
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

public enum EarningRole
{
    Creator = 1,
    Trainer = 2
}

public enum EarningStatus
{
    Pending = 0,
    Available = 1,
    Requested = 2,
    Paid = 3,
    Voided = 4
}

public enum PayoutStatus
{
    Requested = 0,
    Paid = 1,
    Rejected = 2
}

public enum CourseType
{
    Theory = 0,
    Practice = 1
}

public enum SubjectArea
{
    DigitalTechnology = 1,
    BusinessManagement = 2,
    HumanitiesHealthCreative = 3
}

public enum SaleMode
{
    Intake = 0,
    CoursePack = 1
}
