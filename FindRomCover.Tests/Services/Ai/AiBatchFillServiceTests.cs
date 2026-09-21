using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FindRomCover.Managers;
using FindRomCover.Models;
using FindRomCover.Services;
using FindRomCover.Services.Ai;
using FluentAssertions;
using ImageMagick;
using Xunit;

namespace FindRomCover.Tests.Services.Ai;

public class AiBatchFillServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly List<IDisposable> _disposables = new();

    public AiBatchFillServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"AiBatchFillTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            try
            {
                disposable.Dispose();
            }
            catch
            {
                /* best effort */
            }

        try
        {
            Directory.Delete(_testDir, true);
        }
        catch
        {
            /* best effort */
        }
    }

    [Fact]
    public async Task RunAsyncShouldFillFromLocalCandidate()
    {
        var settings = CreateSettings();
        CreateImage("super mario bros cover.png");
        using var ai = CreateAiService(settings, PickResponse(0, 0.95));
        var service = new AiBatchFillService(settings, ai);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].Outcome.Should().Be(AiBatchOutcome.FilledFromLocal);
        File.Exists(Path.Combine(_testDir, "Super Mario Bros.png")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsyncShouldSkipWhenConfidenceIsBelowThreshold()
    {
        var settings = CreateSettings();
        CreateImage("super mario bros cover.png");
        using var ai = CreateAiService(settings, PickResponse(0, 0.5));
        var service = new AiBatchFillService(settings, ai);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results[0].Outcome.Should().Be(AiBatchOutcome.SkippedLowConfidence);
        File.Exists(Path.Combine(_testDir, "Super Mario Bros.png")).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsyncShouldReportNoCandidatesWhenFolderHasNoMatches()
    {
        var settings = CreateSettings();
        settings.SimilarityThreshold = 90;
        settings.AiCandidateThreshold = 90;
        CreateImage("completely different game.png");
        using var ai = CreateAiService(settings, PickResponse(0, 0.95));
        var service = new AiBatchFillService(settings, ai);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results[0].Outcome.Should().Be(AiBatchOutcome.NoCandidates);
    }

    [Fact]
    public async Task RunAsyncShouldSkipWhenTargetAlreadyExists()
    {
        var settings = CreateSettings();
        CreateImage("Super Mario Bros.png");
        using var ai = CreateAiService(settings, PickResponse(0, 0.95));
        var service = new AiBatchFillService(settings, ai);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results[0].Outcome.Should().Be(AiBatchOutcome.SkippedAlreadyExists);
    }

    [Fact]
    public async Task RunAsyncShouldSkipWhenCoverExistsWithDifferentExtension()
    {
        var settings = CreateSettings();
        using (var image = new MagickImage(MagickColors.Red, 64, 64))
        {
            image.Format = MagickFormat.Jpeg;
            image.Write(Path.Combine(_testDir, "Super Mario Bros.jpg"));
        }

        using var ai = CreateAiService(settings, PickResponse(0, 0.95));
        var service = new AiBatchFillService(settings, ai);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results[0].Outcome.Should().Be(AiBatchOutcome.SkippedAlreadyExists);
    }

    [Fact]
    public async Task RunAsyncShouldSkipWhenCoverExistsUnderSanitizedName()
    {
        var settings = CreateSettings();
        CreateImage("Dragon Quest III - Soshite Densetsu e. (Japan).png");

        using var ai = CreateAiService(settings, PickResponse(0, 0.95));
        var service = new AiBatchFillService(settings, ai);

        var results = await service.RunAsync(
            [new MissingImageItem("Dragon Quest III - Soshite Densetsu e... (Japan)",
                "Dragon Quest III - Soshite Densetsu e... (Japan)")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results[0].Outcome.Should().Be(AiBatchOutcome.SkippedAlreadyExists);
    }

    [Fact]
    public async Task RunAsyncShouldSkipItemsAlreadyQueried()
    {
        var settings = CreateSettings();
        CreateImage("super mario bros cover.png");

        var history = new AiQueryHistory(Path.Combine(_testDir, "history.json"));
        history.MarkQueried(Path.Combine(_testDir, "Super Mario Bros.png"), "local-no-match");

        var requests = 0;
        using var ai = CreateAiService(settings, PickResponse(0, 0.95), () => requests++);
        var service = new AiBatchFillService(settings, ai, null, history);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results[0].Outcome.Should().Be(AiBatchOutcome.SkippedAlreadyQueried);
        requests.Should().Be(0);
    }

    [Fact]
    public async Task RunAsyncShouldQueryWhenSkipPreviouslyQueriedIsDisabled()
    {
        var settings = CreateSettings();
        CreateImage("super mario bros cover.png");

        var history = new AiQueryHistory(Path.Combine(_testDir, "history.json"));
        var targetPath = Path.Combine(_testDir, "Super Mario Bros.png");
        history.MarkQueried(targetPath, "local-no-match");

        using var ai = CreateAiService(settings, PickResponse(0, 0.95));
        var service = new AiBatchFillService(settings, ai, null, history);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None,
            false);

        results[0].Outcome.Should().Be(AiBatchOutcome.FilledFromLocal);
        history.WasQueried(targetPath).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsyncShouldRememberItemWhenAiFoundNoConfidentMatch()
    {
        var settings = CreateSettings();
        CreateImage("super mario bros cover.png");

        var history = new AiQueryHistory(Path.Combine(_testDir, "history.json"));
        using var ai = CreateAiService(settings, PickResponse(0, 0.5));
        var service = new AiBatchFillService(settings, ai, null, history);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results[0].Outcome.Should().Be(AiBatchOutcome.SkippedLowConfidence);
        history.WasQueried(Path.Combine(_testDir, "Super Mario Bros.png")).Should().BeTrue();
    }

    [Fact]
    public async Task FindTopCandidatesAsyncShouldRankAndFilterCandidates()
    {
        CreateImage("super mario bros cover.png");
        CreateImage("totally unrelated.png");

        var candidates = await SimilarityCalculator.FindTopCandidatesAsync(
            "Super Mario Bros",
            _testDir,
            60,
            AppConstants.Algorithms.JaroWinkler,
            CancellationToken.None,
            5);

        candidates.Should().HaveCount(1);
        candidates[0].ImageName.Should().Be("super mario bros cover");
    }

    [Fact]
    public async Task RunAsyncShouldNotFailWhenModelDeclinesLocalPick()
    {
        var settings = CreateSettings();
        CreateImage("super mario bros cover.png");
        using var ai = CreateAiService(settings, PickResponse(-1, 0.0));
        var service = new AiBatchFillService(settings, ai);

        var results = await service.RunAsync(
            [new MissingImageItem("Super Mario Bros", "Super Mario Bros")],
            _testDir,
            false,
            null,
            null,
            CancellationToken.None);

        results[0].Outcome.Should().Be(AiBatchOutcome.SkippedLowConfidence);
        File.Exists(Path.Combine(_testDir, "Super Mario Bros.png")).Should().BeFalse();
    }

    [Fact]
    public void IsUsablePickShouldAcceptConfidentInRangePick()
    {
        var service = CreateService();
        var pick = new AiPickResult { BestIndex = 1, Confidence = 0.95 };

        service.IsUsablePick(pick, 3).Should().BeTrue();
    }

    [Fact]
    public void IsUsablePickShouldRejectDeclinedPick()
    {
        var service = CreateService();
        var pick = new AiPickResult { BestIndex = -1, Confidence = 0.0 };

        service.IsUsablePick(pick, 3).Should().BeFalse();
    }

    [Fact]
    public void IsUsablePickShouldRejectOutOfRangeIndex()
    {
        var service = CreateService();
        var pick = new AiPickResult { BestIndex = 5, Confidence = 0.95 };

        service.IsUsablePick(pick, 3).Should().BeFalse();
    }

    [Fact]
    public void IsUsablePickShouldRejectNullPick()
    {
        var service = CreateService();

        service.IsUsablePick(null, 3).Should().BeFalse();
    }

    [Fact]
    public void IsUsablePickShouldRejectLowConfidencePick()
    {
        var service = CreateService();
        var pick = new AiPickResult { BestIndex = 0, Confidence = 0.5 };

        service.IsUsablePick(pick, 3).Should().BeFalse();
    }

    private AiBatchFillService CreateService()
    {
        var settings = CreateSettings();
        var ai = CreateAiService(settings, PickResponse(0, 0.95));
        _disposables.Add(ai);
        return new AiBatchFillService(settings, ai);
    }

    private static SettingsManager CreateSettings()
    {
        return new SettingsManager
        {
            AiAssistEnabled = true,
            AiAutoSaveThreshold = 80,
            AiMaxCandidates = 6,
            AiBaseUrl = "https://example.test/v1",
            AiApiKey = "test-key",
            AiModel = "test-model",
            AiCandidateThreshold = 0,
            SimilarityThreshold = 0,
            SelectedSimilarityAlgorithm = AppConstants.Algorithms.JaroWinkler
        };
    }

    private static AiAssistService CreateAiService(
        SettingsManager settings,
        string responseJson,
        Action? onRequest = null)
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            onRequest?.Invoke();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler);
        var client = new OpenAiCompatibleVisionClient(httpClient);

        return new AiAssistService(
            settings,
            client,
            new AiVerdictCache(Path.Combine(Path.GetTempPath(), $"ai_cache_{Guid.NewGuid():N}.json")),
            httpClient);
    }

    private static string PickResponse(int bestIndex, double confidence)
    {
        var inner =
            $"{{\"bestIndex\":{bestIndex},\"confidence\":{confidence.ToString(CultureInfo.InvariantCulture)},\"reason\":\"test\"}}";
        var escaped = JsonSerializer.Serialize(inner);
        return $"{{\"choices\":[{{\"message\":{{\"content\":{escaped}}}}}]}}";
    }

    private void CreateImage(string fileName)
    {
        using var image = new MagickImage(MagickColors.Red, 64, 64);
        image.Format = MagickFormat.Png;
        image.Write(Path.Combine(_testDir, fileName));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
