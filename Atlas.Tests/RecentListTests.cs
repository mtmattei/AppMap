using Atlas.Core;

namespace Atlas.Tests;

public class RecentListTests
{
    [Fact]
    public void Promote_puts_the_new_path_first()
    {
        var result = RecentList.Promote(new[] { "a.json", "b.json" }, "c.json");

        Assert.Equal(new[] { "c.json", "a.json", "b.json" }, result);
    }

    [Fact]
    public void Promote_moves_an_existing_path_to_front_without_duplicating()
    {
        var result = RecentList.Promote(new[] { "a.json", "b.json", "c.json" }, "b.json");

        Assert.Equal(new[] { "b.json", "a.json", "c.json" }, result);
    }

    [Fact]
    public void Promote_dedupes_case_insensitively()
    {
        var result = RecentList.Promote(new[] { @"C:\Models\App.json" }, @"c:\models\app.json");

        Assert.Equal(new[] { @"c:\models\app.json" }, result);
    }

    [Fact]
    public void Promote_caps_at_the_capacity_keeping_the_newest()
    {
        var current = Enumerable.Range(1, 8).Select(i => $"{i}.json").ToList();

        var result = RecentList.Promote(current, "new.json");

        Assert.Equal(8, result.Count);
        Assert.Equal("new.json", result[0]);
        Assert.DoesNotContain("8.json", result); // the oldest fell off the end
    }

    [Fact]
    public void Promote_respects_a_custom_capacity()
    {
        var result = RecentList.Promote(new[] { "a.json", "b.json", "c.json" }, "d.json", capacity: 2);

        Assert.Equal(new[] { "d.json", "a.json" }, result);
    }
}
