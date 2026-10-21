using BS2.Application.Classification;
using BS2.Domain;
using BS2.Domain.Entities;
using Xunit;

namespace BS2.Tests.Classification;

public class ClassificationResponseParserTests
{
    private static readonly IReadOnlyList<Category> Categories = CategorySeed.Categories;
    private static readonly IReadOnlyList<TokenLogProb> NoTokens = [];

    [Theory]
    [InlineData("Food and drink (other)", "FoodAndDrink")]
    [InlineData("FoodAndDrink", "FoodAndDrink")]
    [InlineData("[Groceries]", "Groceries")]
    [InlineData(" Fast_Food ", "FastFood")]
    public void Parse_resolves_cleaned_response_to_category(string raw, string expectedCode)
    {
        var parsed = ClassificationResponseParser.Parse(raw, Categories, NoTokens);

        Assert.Equal(expectedCode, parsed.Category?.Code);
        Assert.Equal(expectedCode, parsed.PredictedCode);
    }

    [Fact]
    public void Parse_keeps_unresolvable_text_as_code_without_category()
    {
        var parsed = ClassificationResponseParser.Parse("Banana", Categories, NoTokens);

        Assert.Null(parsed.Category);
        Assert.Equal("Banana", parsed.PredictedCode);
        Assert.Null(parsed.Confidence);
    }

    [Fact]
    public void Parse_confidence_is_exp_of_summed_logprobs()
    {
        var tokens = new[]
        {
            new TokenLogProb("Gro", Math.Log(0.5), []),
            new TokenLogProb("ceries", Math.Log(0.8), [])
        };

        var parsed = ClassificationResponseParser.Parse("Groceries", Categories, tokens);

        Assert.Equal(0.4, parsed.Confidence!.Value, 6);
    }

    [Fact]
    public void Parse_clamps_confidence_to_one()
    {
        var parsed = ClassificationResponseParser.Parse("Groceries", Categories, [new TokenLogProb("Groceries", 0.001, [])]);

        Assert.Equal(1.0, parsed.Confidence);
    }

    [Fact]
    public void Parse_maps_first_token_alternatives_and_keeps_unresolvable_tokens()
    {
        var tokens = new[]
        {
            new TokenLogProb("Gro", Math.Log(0.7), [("Gro", Math.Log(0.7)), ("Food", Math.Log(0.2)), ("Takeaway", Math.Log(0.1))]),
            new TokenLogProb("ceries", 0, [("ceries", 0)])
        };

        var parsed = ClassificationResponseParser.Parse("Groceries", Categories, tokens);

        Assert.Equal(3, parsed.Alternatives.Count);
        Assert.Equal("Gro", parsed.Alternatives[0].Code);
        Assert.Equal("Food", parsed.Alternatives[1].Code);
        Assert.Equal("Takeaway", parsed.Alternatives[2].Code);
        Assert.Equal(0.2, parsed.Alternatives[1].Probability, 6);
        Assert.Equal(Math.Log(0.2), parsed.Alternatives[1].LogProb, 6);
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
