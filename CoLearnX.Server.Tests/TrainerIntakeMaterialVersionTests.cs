using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class TrainerIntakeMaterialVersionTests
{
    [Fact]
    public async Task Attached_material_list_and_attach_response_include_business_version_number()
    {
        using var factory = new CoLearnXApiFactory();
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        int intakeId;
        int versionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(item => item.Course).FirstAsync(item => item.Trainer.Email == SeedData.TrainerEmail);
            intakeId = intake.Id;
            var material = new LearningMaterial
            {
                CreatorId = intake.Course.CreatorId,
                Title = "Versioned trainer material",
                FilePath = "materials/versioned-trainer.pdf",
                Format = "PDF",
            };
            db.LearningMaterials.Add(material);
            await db.SaveChangesAsync();
            db.CourseMaterials.Add(new CourseMaterial { CourseId = intake.CourseId, LearningMaterialId = material.Id });
            var version = new CourseMaterialVersion
            {
                LearningMaterialId = material.Id,
                VersionNumber = 7,
                FilePath = material.FilePath,
                Format = material.Format,
                Status = MaterialVersionStatus.Approved,
            };
            db.CourseMaterialVersions.Add(version);
            await db.SaveChangesAsync();
            versionId = version.Id;
        }

        using var attach = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{intakeId}/learning-materials",
            new { materialVersionId = versionId });
        Assert.Equal(HttpStatusCode.OK, attach.StatusCode);
        var attached = await attach.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(7, attached!["versionNumber"]!.GetValue<int>());

        using var list = await trainer.GetAsync($"/api/trainer/intakes/{intakeId}/learning-materials");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = (await list.Content.ReadFromJsonAsync<JsonArray>())!;
        Assert.Equal(7, items.Single(item => item!["materialVersionId"]!.GetValue<int>() == versionId)!["versionNumber"]!.GetValue<int>());
    }
}
