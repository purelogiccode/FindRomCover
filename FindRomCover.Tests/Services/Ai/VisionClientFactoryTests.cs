using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services.Ai;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class VisionClientFactoryTests
{
    private static AiVisionOptions CreateOptions(string provider)
    {
        return new AiVisionOptions(
            true,
            provider,
            "https://example.test/v1",
            "test-key",
            "test-model",
            30,
            6,
            512,
            80,
            false,
            false,
            false);
    }

    [Theory]
    [InlineData(AppConstants.AiProviders.OpenRouter, typeof(OpenAiCompatibleVisionClient))]
    [InlineData(AppConstants.AiProviders.OpenAi, typeof(OpenAiCompatibleVisionClient))]
    [InlineData(AppConstants.AiProviders.Glm, typeof(OpenAiCompatibleVisionClient))]
    [InlineData(AppConstants.AiProviders.Local, typeof(OpenAiCompatibleVisionClient))]
    [InlineData(AppConstants.AiProviders.CustomOpenAi, typeof(OpenAiCompatibleVisionClient))]
    [InlineData(AppConstants.AiProviders.Anthropic, typeof(AnthropicVisionClient))]
    [InlineData(AppConstants.AiProviders.CustomAnthropic, typeof(AnthropicVisionClient))]
    [InlineData(AppConstants.AiProviders.Gemini, typeof(GeminiVisionClient))]
    public void CreateShouldResolveAdapterForProvider(string provider, Type expectedType)
    {
        using var client = VisionClientFactory.Create(CreateOptions(provider));

        client.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData(AppConstants.AiProviders.OpenRouter, "https://openrouter.ai/api/v1",
        "google/gemini-2.5-flash")]
    [InlineData(AppConstants.AiProviders.OpenAi, "https://api.openai.com/v1", "gpt-4o-mini")]
    [InlineData(AppConstants.AiProviders.Anthropic, "https://api.anthropic.com/v1", "claude-sonnet-4-5")]
    [InlineData(AppConstants.AiProviders.Gemini, "https://generativelanguage.googleapis.com/v1beta",
        "gemini-2.5-flash")]
    [InlineData(AppConstants.AiProviders.Glm, "https://api.z.ai/api/paas/v4", "glm-4.5v")]
    [InlineData(AppConstants.AiProviders.Local, "http://localhost:11434/v1", "qwen2.5vl:7b")]
    [InlineData(AppConstants.AiProviders.CustomOpenAi, "", "")]
    [InlineData(AppConstants.AiProviders.CustomAnthropic, "https://api.anthropic.com/v1", "")]
    public void EffectiveDefaultsShouldCoverEveryProvider(string provider, string expectedBaseUrl, string expectedModel)
    {
        var settings = new SettingsManager
        {
            AiProvider = provider,
            AiBaseUrl = string.Empty,
            AiModel = string.Empty
        };

        settings.GetEffectiveAiBaseUrl().Should().Be(expectedBaseUrl);
        settings.GetEffectiveAiModel().Should().Be(expectedModel);
    }
}
