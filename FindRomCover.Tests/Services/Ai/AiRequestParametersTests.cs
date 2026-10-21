using System.Text.Json;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class AiRequestParametersTests
{
    private const string BaseOpenAiBody =
        "{\"model\":\"test-model\",\"messages\":[],\"temperature\":0.1,\"max_tokens\":4096}";

    [Fact]
    public void TryParseShouldAcceptEmptyValue()
    {
        var valid = AiRequestParameters.TryParse("  ", out var parameters, out var error);

        valid.Should().BeTrue();
        parameters.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void TryParseShouldAcceptJsonObject()
    {
        var valid = AiRequestParameters.TryParse("{\"temperature\":0.2}", out var parameters, out var error);

        valid.Should().BeTrue();
        parameters.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("{ invalid }")]
    public void TryParseShouldRejectNonObjectOrInvalidJson(string json)
    {
        var valid = AiRequestParameters.TryParse(json, out var parameters, out var error);

        valid.Should().BeFalse();
        parameters.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("model")]
    [InlineData("messages")]
    [InlineData("contents")]
    [InlineData("system")]
    [InlineData("systemInstruction")]
    [InlineData("stream")]
    [InlineData("Model")]
    public void TryParseShouldRejectReservedKeys(string key)
    {
        var valid = AiRequestParameters.TryParse($"{{\"{key}\": null}}", out _, out var error);

        valid.Should().BeFalse();
        error.Should().Contain(key);
    }

    [Fact]
    public void ApplyToJsonShouldReturnBaseBodyWithoutOverrides()
    {
        var result = AiRequestParameters.ApplyToJson(BaseOpenAiBody, string.Empty);

        result.Should().Be(BaseOpenAiBody);
    }

    [Fact]
    public void ApplyToJsonShouldMergeTopLevelValues()
    {
        var result = AiRequestParameters.ApplyToJson(
            BaseOpenAiBody,
            "{\"temperature\":0.3,\"top_p\":0.9,\"max_tokens\":8192}");

        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;
        root.GetProperty("temperature").GetDouble().Should().Be(0.3);
        root.GetProperty("top_p").GetDouble().Should().Be(0.9);
        root.GetProperty("max_tokens").GetInt32().Should().Be(8192);
        root.GetProperty("model").GetString().Should().Be("test-model");
    }

    [Fact]
    public void ApplyToJsonShouldDeepMergeNestedObjects()
    {
        const string baseBody =
            "{\"generationConfig\":{\"temperature\":0.1,\"maxOutputTokens\":4096},\"contents\":[]}";

        var result = AiRequestParameters.ApplyToJson(
            baseBody,
            "{\"generationConfig\":{\"temperature\":0.2,\"topP\":0.95}}");

        using var document = JsonDocument.Parse(result);
        var generationConfig = document.RootElement.GetProperty("generationConfig");
        generationConfig.GetProperty("temperature").GetDouble().Should().Be(0.2);
        generationConfig.GetProperty("topP").GetDouble().Should().Be(0.95);
        generationConfig.GetProperty("maxOutputTokens").GetInt32().Should().Be(4096);
    }

    [Fact]
    public void ApplyToJsonShouldRemoveKeysSetToNull()
    {
        var result = AiRequestParameters.ApplyToJson(BaseOpenAiBody, "{\"temperature\":null}");

        using var document = JsonDocument.Parse(result);
        document.RootElement.TryGetProperty("temperature", out _).Should().BeFalse();
        document.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(4096);
    }

    [Fact]
    public void ApplyToJsonShouldIgnoreReservedKeys()
    {
        var result = AiRequestParameters.ApplyToJson(
            BaseOpenAiBody,
            "{\"model\":\"other\",\"messages\":[{\"role\":\"user\"}],\"temperature\":0.4}");

        using var document = JsonDocument.Parse(result);
        document.RootElement.GetProperty("model").GetString().Should().Be("test-model");
        document.RootElement.GetProperty("messages").GetArrayLength().Should().Be(0);
        document.RootElement.GetProperty("temperature").GetDouble().Should().Be(0.4);
    }

    [Fact]
    public void ApplyToJsonShouldReplaceArrays()
    {
        var result = AiRequestParameters.ApplyToJson(
            "{\"stop\":[\"one\",\"two\"]}",
            "{\"stop\":[\"END\"]}");

        using var document = JsonDocument.Parse(result);
        document.RootElement.GetProperty("stop").EnumerateArray().Select(static value => value.GetString())
            .Should().Equal("END");
    }

    [Fact]
    public void ApplyToJsonShouldIgnoreInvalidJson()
    {
        var result = AiRequestParameters.ApplyToJson(BaseOpenAiBody, "{ invalid }");

        result.Should().Be(BaseOpenAiBody);
    }

    [Fact]
    public void ApplyToJsonShouldRejectOversizedJson()
    {
        var oversized = "{\"value\":\"" + new string('x', AiRequestParameters.MaxJsonLength) + "\"}";

        var result = AiRequestParameters.ApplyToJson(BaseOpenAiBody, oversized);

        result.Should().Be(BaseOpenAiBody);
    }
}
