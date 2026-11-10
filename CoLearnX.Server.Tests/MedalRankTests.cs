using CoLearnX.Server.Services;

namespace CoLearnX.Server.Tests;

public class MedalRankTests
{
    [Fact]
    public void Empty_area_states_the_full_rule()
    {
        var standing = MedalRanks.Describe(null);
        Assert.Equal("Locked", standing.Material);
        Assert.Contains("Brass", standing.Requirement);
        Assert.Contains("Silver", standing.Requirement);
        Assert.Contains("Gold", standing.Requirement);
        Assert.Contains("Diamond", standing.Requirement);
        Assert.Equal("0/1 courses.", standing.Progress);
    }

    [Fact]
    public void Any_single_course_earns_brass()
    {
        var beginner = MedalRanks.Describe(["Beginner"]);
        Assert.Equal("Brass", beginner.Material);
        Assert.Equal("1/3 courses. Still need an Intermediate or Advanced course.", beginner.Progress);

        var advanced = MedalRanks.Describe(["Advanced"]);
        Assert.Equal("Brass", advanced.Material);
        Assert.Equal("1/3 courses.", advanced.Progress);
        Assert.Contains("3", advanced.Requirement);
    }

    [Fact]
    public void Three_advanced_courses_reach_silver()
    {
        var standing = MedalRanks.Describe(["Advanced", "Advanced", "Advanced"]);
        Assert.Equal("Silver", standing.Material);
        Assert.Equal("3/5 courses.", standing.Progress);
        Assert.Contains("5", standing.Requirement);
        Assert.Contains("Gold", standing.Requirement);
    }

    [Fact]
    public void Three_courses_without_a_higher_level_stay_brass()
    {
        var standing = MedalRanks.Describe(["Beginner", "Beginner", "Beginner"]);
        Assert.Equal("Brass", standing.Material);
        Assert.Contains("Intermediate", standing.Requirement);
        Assert.Contains("Advanced", standing.Requirement);
    }

    [Fact]
    public void Three_courses_including_an_intermediate_reach_silver()
    {
        var standing = MedalRanks.Describe(["Beginner", "Beginner", "Intermediate"]);
        Assert.Equal("Silver", standing.Material);
        Assert.Equal("3/5 courses. Still need an Advanced course.", standing.Progress);
    }

    [Fact]
    public void Five_advanced_courses_reach_gold_and_name_the_missing_levels()
    {
        var standing = MedalRanks.Describe(["Advanced", "Advanced", "Advanced", "Advanced", "Advanced"]);
        Assert.Equal("Gold", standing.Material);
        Assert.Contains("Beginner", standing.Requirement);
        Assert.Contains("Intermediate", standing.Requirement);
        Assert.Contains("Diamond", standing.Requirement);
    }

    [Fact]
    public void Three_courses_across_every_level_still_need_five_for_diamond()
    {
        var standing = MedalRanks.Describe(["advanced", "BEGINNER", "Intermediate"]);
        Assert.Equal("Silver", standing.Material);
        Assert.Contains("5", standing.Requirement);
        Assert.Contains("Diamond", standing.Requirement);
    }

    [Fact]
    public void Five_courses_across_every_level_reach_diamond()
    {
        var standing = MedalRanks.Describe(["Beginner", "Intermediate", "Advanced", "Beginner", "Advanced"]);
        Assert.Equal("Diamond", standing.Material);
        Assert.Contains("5", standing.Requirement);
    }
}
