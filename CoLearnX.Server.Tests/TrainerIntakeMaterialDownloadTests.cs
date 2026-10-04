using System.Net;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class TrainerIntakeMaterialDownloadTests
{
    [Fact]
    public async Task Owned_trainer_downloads_the_attached_approved_version_and_rejects_other_access()
    {
        using var factory = new CoLearnXApiFactory();
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(factory);
        using var creator = await ApiClient.AsCreatorAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var intake = await db.CourseIntakes.Include(item => item.Course)
            .FirstAsync(item => item.Trainer.Email == SeedData.TrainerEmail);
        var otherIntake = await db.CourseIntakes.FirstAsync(item => item.Id != intake.Id);
        var payload = "%PNG trainer-attached"u8.ToArray();
        var attachedKey = $"materials/{intake.Course.CreatorId}/{Guid.NewGuid():N}.png";
        var newerKey = $"materials/{intake.Course.CreatorId}/{Guid.NewGuid():N}.png";
        await storage.SaveAsync(attachedKey, new MemoryStream(payload), "image/png");
        await storage.SaveAsync(newerKey, new MemoryStream("newer"u8.ToArray()), "image/png");

        var material = new LearningMaterial
        {
            CreatorId = intake.Course.CreatorId,
            Title = "L-07 Test Material",
            FilePath = attachedKey,
            Format = "PNG",
        };
        db.LearningMaterials.Add(material);
        await db.SaveChangesAsync();
        db.CourseMaterials.Add(new CourseMaterial { CourseId = intake.CourseId, LearningMaterialId = material.Id });
        var attached = new CourseMaterialVersion
        {
            LearningMaterialId = material.Id,
            VersionNumber = 1,
            FilePath = attachedKey,
            Format = "PNG",
            Status = MaterialVersionStatus.Approved,
        };
        var newer = new CourseMaterialVersion
        {
            LearningMaterialId = material.Id,
            VersionNumber = 2,
            FilePath = newerKey,
            Format = "PNG",
            Status = MaterialVersionStatus.Approved,
        };
        var pending = new CourseMaterialVersion
        {
            LearningMaterialId = material.Id,
            VersionNumber = 3,
            FilePath = attachedKey,
            Format = "PNG",
            Status = MaterialVersionStatus.PendingApproval,
        };
        db.CourseMaterialVersions.AddRange(attached, newer, pending);
        await db.SaveChangesAsync();
        db.CourseIntakeMaterials.Add(new CourseIntakeMaterial
        {
            CourseIntakeId = intake.Id,
            CourseMaterialVersionId = attached.Id,
            AttachedByTrainerId = intake.TrainerId,
        });
        await db.SaveChangesAsync();

        var path = $"/api/trainer/intakes/{intake.Id}/learning-materials/{attached.Id}/file";
        using var file = await trainer.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(payload, await file.Content.ReadAsByteArrayAsync());
        var fileName = (file.Content.Headers.ContentDisposition?.FileNameStar
            ?? file.Content.Headers.ContentDisposition?.FileName ?? string.Empty).Trim('"');
        Assert.Contains("L-07", fileName);
        Assert.EndsWith(".png", fileName);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ApiClient.Anonymous(factory).GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await trainer.GetAsync($"/api/trainer/intakes/{otherIntake.Id}/learning-materials/{attached.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await trainer.GetAsync($"/api/trainer/intakes/{intake.Id}/learning-materials/{newer.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await trainer.GetAsync($"/api/trainer/intakes/{intake.Id}/learning-materials/{pending.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await creator.GetAsync(path)).StatusCode);
    }
}
