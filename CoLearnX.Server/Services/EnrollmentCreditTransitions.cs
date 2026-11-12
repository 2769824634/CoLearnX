using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public readonly record struct EnrollmentWalletSnapshot(int BalanceAfter, int HeldAfter);

public sealed class EnrollmentCreditTransitions(CoLearnXDbContext db)
{
    public async Task<EnrollmentWalletSnapshot> HoldCreditsAndSeatAsync(int userId, int sessionId, int cost,
        CancellationToken ct = default, IReadOnlyList<int>? physicalSessionIds = null)
    {
        if (db.Database.CurrentTransaction is not null)
            return await HoldCoreAsync(userId, sessionId, cost, ct, physicalSessionIds);

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var result = await HoldCoreAsync(userId, sessionId, cost, ct, physicalSessionIds);
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    private async Task<EnrollmentWalletSnapshot> HoldCoreAsync(int userId, int sessionId, int cost,
        CancellationToken ct, IReadOnlyList<int>? physicalSessionIds)
    {
        var updatedUser = await db.Users.Where(u => u.Id == userId && u.IsActive && u.CreditBalance >= cost)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.CreditBalance, u => u.CreditBalance - cost)
                .SetProperty(u => u.HeldCredits, u => u.HeldCredits + cost), ct);
        if (updatedUser != 1)
            throw new CourseException("INSUFFICIENT_CREDITS", "Not enough available credits to reserve this place.");
        foreach (var seatId in physicalSessionIds ?? [sessionId])
        {
            var seats = db.CourseSessions.Where(s => s.Id == seatId);
            if (physicalSessionIds is null)
                seats = seats.Where(s => s.PhysicalCapacity == 0 || s.SeatsTaken < s.PhysicalCapacity);
            else
                seats = seats.Where(s => s.PhysicalCapacity == 0 || (s.SeatsTaken < s.PhysicalCapacity
                    && (s.PhysicalBookingDeadline == null || s.PhysicalBookingDeadline >= DateTime.UtcNow)));
            var updatedSeat = await seats.ExecuteUpdateAsync(setters => setters.SetProperty(s => s.SeatsTaken, s => s.SeatsTaken + 1), ct);
            if (updatedSeat != 1)
                throw new CourseException("SESSION_FULL", "A selected physical session is full or no longer available.");
        }
        var wallet = await db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.CreditBalance, u.HeldCredits }).SingleAsync(ct);
        return new EnrollmentWalletSnapshot(wallet.CreditBalance, wallet.HeldCredits);
    }

    public void RecordHold(int userId, int enrollmentId, int cost, int balanceAfter, int heldAfter, string description)
        => AddLedger(userId, CreditTransactionType.Hold, description, -cost, balanceAfter, heldAfter, enrollmentId);

    public void Release(User user, Enrollment enrollment, string description, bool inconsistentHoldIsDataError = false)
    {
        var cost = enrollment.CreditsSpent;
        if (user.HeldCredits < cost)
        {
            if (inconsistentHoldIsDataError)
                throw new InvalidOperationException($"Held credits are inconsistent for enrollment {enrollment.Id}.");
            throw new CourseException("RESERVATION_BALANCE_CONFLICT",
                "Reservation balances need review before cancellation.", 409);
        }
        ReleaseSeats(enrollment, "RESERVATION_BALANCE_CONFLICT", "Reservation balances need review before cancellation.");
        user.HeldCredits -= cost;
        user.CreditBalance += cost;
        enrollment.Status = EnrollmentStatus.Cancelled;
        AddLedger(user.Id, CreditTransactionType.Release, description, cost, user.CreditBalance, user.HeldCredits,
            enrollment.Id);
    }

    public void Capture(User user, Enrollment enrollment, string description)
    {
        var cost = enrollment.CreditsSpent;
        if (user.HeldCredits < cost)
            throw new InvalidOperationException($"Held credits are inconsistent for enrollment {enrollment.Id}.");
        user.HeldCredits -= cost;
        enrollment.Status = EnrollmentStatus.Active;
        AddLedger(user.Id, CreditTransactionType.Capture, description, 0, user.CreditBalance, user.HeldCredits,
            enrollment.Id);
    }

    public void Refund(User user, Enrollment enrollment, int refundAmount, string description,
        string? forfeitDescription = null, int? relatedDisputeId = null, int? adminAccountId = null,
        Guid? idempotencyKey = null)
    {
        ReleaseSeats(enrollment, "ENROLLMENT_SEAT_CONFLICT", "Enrollment seats need review before withdrawal.");
        user.CreditBalance += refundAmount;
        enrollment.Status = EnrollmentStatus.Refunded;
        AddLedger(user.Id, CreditTransactionType.Refund, description, refundAmount, user.CreditBalance, user.HeldCredits,
            enrollment.Id, relatedDisputeId, adminAccountId, idempotencyKey);
        var forfeited = enrollment.CreditsSpent - refundAmount;
        if (forfeited > 0 && forfeitDescription is not null)
            AddLedger(user.Id, CreditTransactionType.Forfeit, forfeitDescription, 0, user.CreditBalance,
                user.HeldCredits, enrollment.Id);
    }

    private static void ReleaseSeats(Enrollment enrollment, string code, string message)
    {
        var sessions = enrollment.DeliveryChoices.Count > 0
            ? enrollment.DeliveryChoices.Select(choice => choice.CourseSession).ToList()
            : [enrollment.CourseSession];
        if (sessions.Any(session => session.SeatsTaken <= 0)) throw new CourseException(code, message, 409);
        foreach (var session in sessions) session.SeatsTaken--;
    }

    private void AddLedger(int userId, CreditTransactionType type, string description, int delta, int balanceAfter,
        int heldAfter, int enrollmentId, int? relatedDisputeId = null, int? adminAccountId = null,
        Guid? idempotencyKey = null)
        => db.CreditTransactions.Add(new CreditTransaction
        {
            UserId = userId,
            Type = type,
            Description = description,
            Delta = delta,
            BalanceAfter = balanceAfter,
            HeldAfter = heldAfter,
            RelatedEnrollmentId = enrollmentId,
            RelatedDisputeId = relatedDisputeId,
            AdminAccountId = adminAccountId,
            IdempotencyKey = idempotencyKey,
        });
}
