using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CoLearnX.Server.Data;

internal static class CourseIntakeModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        var utc = new ValueConverter<DateTime, DateTime>(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        modelBuilder.Entity<CourseIntake>(e =>
        {
            e.ToTable("CourseIntakes", table =>
            {
                table.HasCheckConstraint("CK_CourseIntakes_Dates", "RegistrationOpensAt < RegistrationClosesAt AND RegistrationClosesAt <= StartsAt AND StartsAt < EndsAt");
                table.HasCheckConstraint("CK_CourseIntakes_Status", "Status BETWEEN 0 AND 6");
            });
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.RegistrationOpensAt).HasConversion(utc);
            e.Property(x => x.RegistrationClosesAt).HasConversion(utc);
            e.Property(x => x.StartsAt).HasConversion(utc);
            e.Property(x => x.EndsAt).HasConversion(utc);
            e.Property(x => x.SubmittedAt).HasConversion(utc);
            e.Property(x => x.ConfirmedAt).HasConversion(utc);
            e.Property(x => x.ConfirmationNote).HasMaxLength(512);
            e.HasIndex(x => new { x.TrainerId, x.Status });
            e.HasOne(x => x.Course).WithMany(x => x.Intakes).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ConfirmedByCreator).WithMany().HasForeignKey(x => x.ConfirmedByCreatorId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseSession>(e =>
        {
            e.ToTable("CourseSessions", table =>
            {
                table.HasCheckConstraint("CK_CourseSessions_Dates", "StartsAt < EndsAt");
                table.HasCheckConstraint("CK_CourseSessions_Capacity", "PhysicalCapacity >= 0 AND SeatsTaken >= 0");
            });
            e.Ignore(x => x.SeatsLeft);
            e.Property(x => x.StartsAt).HasConversion(utc);
            e.Property(x => x.EndsAt).HasConversion(utc);
            e.Property(x => x.PhysicalBookingDeadline).HasConversion(utc);
            e.Property(x => x.Label).HasMaxLength(128);
            e.Property(x => x.MeetingLink).HasMaxLength(2048);
            e.Property(x => x.PhysicalAddress).HasMaxLength(512);
            e.HasIndex(x => new { x.CourseIntakeId, x.StartsAt });
            e.HasOne(x => x.CourseIntake).WithMany(x => x.Sessions).HasForeignKey(x => x.CourseIntakeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CourseIntakeApplication>(e =>
        {
            e.ToTable("CourseIntakeApplications", table =>
            {
                table.HasCheckConstraint("CK_CourseIntakeApplications_Kind", "Kind BETWEEN 0 AND 1");
                table.HasCheckConstraint("CK_CourseIntakeApplications_Status", "Status BETWEEN 0 AND 2");
            });
            e.Property(x => x.SubmittedAt).HasConversion(utc);
            e.Property(x => x.ReviewedAt).HasConversion(utc);
            e.Property(x => x.ReviewNote).HasMaxLength(512);
            e.Property(x => x.ProposalJson).HasColumnType("TEXT");
            e.HasIndex(x => new { x.CourseIntakeId, x.ApplicationVersion }).IsUnique();
            e.HasIndex(x => x.CourseIntakeId).IsUnique().HasFilter("\"Status\" = 0");
            e.HasOne(x => x.CourseIntake).WithMany(x => x.Applications).HasForeignKey(x => x.CourseIntakeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SubmittedByTrainer).WithMany().HasForeignKey(x => x.SubmittedByTrainerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReviewedByCreator).WithMany().HasForeignKey(x => x.ReviewedByCreatorId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttendanceRecord>().HasOne(x => x.CourseSession).WithMany(x => x.AttendanceRecords)
            .HasForeignKey(x => x.CourseSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
