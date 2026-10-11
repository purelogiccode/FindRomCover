using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class UpdateCheckServiceNullPropertyTests
{
    [Fact]
    public void ParseReleaseResponseWithNullFieldsShouldUseFallbacks()
    {
        const string json = """
                            {
                              "tag_name": "v9.9.9",
                              "html_url": null,
                              "body": null,
                              "published_at": null
                            }
                            """;

        var info = UpdateCheckService.ParseReleaseResponse(json, new Version(1, 0, 0));

        info.IsUpdateAvailable.Should().BeTrue();
        info.ReleaseUrl.Should().Be("https://github.com/purelogiccode/FindRomCover/releases");
        info.ReleaseNotes.Should().BeEmpty();
        info.PublishedAt.Should().BeEmpty();
    }

    [Fact]
    public void ParseReleaseResponseWithNonStringFieldsShouldUseFallbacks()
    {
        const string json = """
                            {
                              "tag_name": "v9.9.9",
                              "html_url": 42,
                              "body": {"nested": true},
                              "published_at": []
                            }
                            """;

        var info = UpdateCheckService.ParseReleaseResponse(json, new Version(1, 0, 0));

        info.ReleaseUrl.Should().Be("https://github.com/purelogiccode/FindRomCover/releases");
        info.ReleaseNotes.Should().BeEmpty();
        info.PublishedAt.Should().BeEmpty();
    }
}
