using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class VisionModelClientParsingTests
{
    [Fact]
    public void ExtractJsonObjectShouldPreferTheVerdictObjectOverAnExample()
    {
        const string content = """
                               Example: {"note": "here is the format", "index": 0}
                               Answer: {"bestIndex": 2, "confidence": 87, "reason": "real"}
                               """;

        var json = VisionModelClientBase.ExtractJsonObject(content);

        json.Should().Contain("\"bestIndex\": 2");
    }

    [Fact]
    public void ExtractJsonObjectShouldFallBackToTheFirstValidObjectWithoutVerdictFields()
    {
        const string content = "prefix {\"note\":\"only example\"} suffix {\"other\":1}";

        var json = VisionModelClientBase.ExtractJsonObject(content);

        json.Should().Be("{\"note\":\"only example\"}");
    }

    [Fact]
    public void ExtractJsonObjectShouldThrowWhenNoJsonIsPresent()
    {
        var act = () => VisionModelClientBase.ExtractJsonObject("no braces here");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParsePickResponseShouldAcceptIntegralRealIndex()
    {
        var result = VisionModelClientBase.ParsePickResponse("{\"bestIndex\": 1.0, \"confidence\": 80}");

        result.BestIndex.Should().Be(1);
        result.Confidence.Should().BeApproximately(0.8, 0.0001);
    }

    [Theory]
    [InlineData("{\"match\": 0, \"confidence\": 0.9}", false)]
    [InlineData("{\"match\": 1, \"confidence\": 0.9}", true)]
    [InlineData("{\"match\": \"0\", \"confidence\": 0.9}", false)]
    [InlineData("{\"match\": \"1\", \"confidence\": 0.9}", true)]
    public void ParseVerificationResponseShouldUnderstandNumericBooleans(string json, bool expected)
    {
        VisionModelClientBase.ParseVerificationResponse(json).IsMatch.Should().Be(expected);
    }

    [Fact]
    public void ExtractJsonObjectShouldRecognizeTheMatchVerdictField()
    {
        const string content = """
                               Example: {"note": "here is the format"}
                               Answer: {"match": false}
                               """;

        VisionModelClientBase.ExtractJsonObject(content).Should().Be("{\"match\": false}");
    }

    [Fact]
    public void ParsePickResponseShouldNotCollapseConfidenceJustAboveOne()
    {
        var result = VisionModelClientBase.ParsePickResponse("{\"bestIndex\": 0, \"confidence\": 1.02}");

        result.Confidence.Should().BeApproximately(1.0, 0.0001);
    }
}