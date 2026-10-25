using BS2.Application.Classification;
using BS2.Domain;
using BS2.Domain.Entities;
using Xunit;

namespace BS2.Tests.Classification;

public class ClassificationResponseParserTests
{
    private static readonly IReadOnlyList<Category> Categories = CategorySeed.Categories;
    private static readonly IReadOnlyList<GroupCandidate> NoGroups = [];

    [Fact]
    public void Parse_reads_category_confidence_and_alternatives()
    {
        var parsed = ClassificationResponseParser.Parse(
            """{"category":"Groceries","confidence":0.85,"alternatives":[{"category":"FoodAndDrink","confidence":0.1},{"category":"Banana","confidence":0}]}""",
            Categories, NoGroups);

        Assert.Equal("Groceries", parsed.Category?.Code);
        Assert.Equal("Groceries", parsed.PredictedCode);
        Assert.Equal(0.85, parsed.Confidence);
        Assert.Equal(["FoodAndDrink", "Banana"], parsed.Alternatives.Select(a => a.Code));
        Assert.Equal(0.1, parsed.Alternatives[0].Probability, 6);
        Assert.Equal(Math.Log(0.1), parsed.Alternatives[0].LogProb, 6);
        Assert.True(double.IsFinite(parsed.Alternatives[1].LogProb));
        Assert.Null(parsed.SuggestedGroupId);
    }

    [Fact]
    public void Parse_keeps_unresolvable_code_without_category_and_clamps_confidence()
    {
        var parsed = ClassificationResponseParser.Parse("""{"category":"Banana","confidence":1.4,"alternatives":[]}""", Categories, NoGroups);

        Assert.Null(parsed.Category);
        Assert.Equal("Banana", parsed.PredictedCode);
        Assert.Equal(1.0, parsed.Confidence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Groceries")]
    [InlineData("[1,2]")]
    public void Parse_returns_nothing_for_non_json_answers(string? raw)
    {
        var parsed = ClassificationResponseParser.Parse(raw, Categories, NoGroups);

        Assert.Null(parsed.Category);
        Assert.Null(parsed.Confidence);
        Assert.Empty(parsed.Alternatives);
    }

    [Theory]
    [InlineData("2", 1)]
    [InlineData("1", 0)]
    [InlineData("0", null)]
    [InlineData("3", null)]
    public void Parse_maps_group_number_to_candidate(string group, int? expectedIndex)
    {
        GroupCandidate[] groups =
        [
            new(Guid.NewGuid(), "Italy", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 14), []),
            new(Guid.NewGuid(), "Renovation", new DateOnly(2026, 5, 1), new DateOnly(2026, 9, 1), [])
        ];

        var parsed = ClassificationResponseParser.Parse(
            $$"""{"category":"Groceries","confidence":0.9,"alternatives":[],"group":"{{group}}"}""", Categories, groups);

        Assert.Equal(expectedIndex is { } i ? groups[i].Id : null, parsed.SuggestedGroupId);
    }

    [Fact]
    public void Decide_accepts_at_exactly_threshold()
    {
        var groceries = CategorySeed.Resolve("Groceries")!;

        Assert.True(ClassificationResponseParser.Decide(0.6, 0.6, groceries));
        Assert.False(ClassificationResponseParser.Decide(0.5999, 0.6, groceries));
    }

    [Fact]
    public void Decide_rejects_without_category_or_confidence()
    {
        Assert.False(ClassificationResponseParser.Decide(0.99, 0.6, null));
        Assert.False(ClassificationResponseParser.Decide(null, 0.6, CategorySeed.Resolve("Groceries")));
    }
}
