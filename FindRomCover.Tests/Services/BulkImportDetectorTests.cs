using FindRomCover.Services;
using FluentAssertions;
using Xunit;

namespace FindRomCover.Tests.Services;

public class BulkImportDetectorTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SingleEventShouldNotActivateBulkImport()
    {
        var detector = CreateDetector();

        detector.Record(Start).Should().BeFalse();
        detector.IsActive(Start).Should().BeFalse();
    }

    [Fact]
    public void EventsBelowThresholdWithinWindowShouldNotActivateBulkImport()
    {
        var detector = CreateDetector();

        detector.Record(Start).Should().BeFalse();
        detector.Record(Start.AddSeconds(1)).Should().BeFalse();
        detector.IsActive(Start.AddSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void ThresholdEventsWithinWindowShouldActivateBulkImport()
    {
        var detector = CreateDetector();

        detector.Record(Start).Should().BeFalse();
        detector.Record(Start.AddSeconds(1)).Should().BeFalse();
        detector.Record(Start.AddSeconds(2)).Should().BeTrue();
        detector.IsActive(Start.AddSeconds(2)).Should().BeTrue();
    }

    [Fact]
    public void EventsOutsideWindowShouldNotAccumulate()
    {
        var detector = CreateDetector();

        detector.Record(Start).Should().BeFalse();
        detector.Record(Start.AddSeconds(10)).Should().BeFalse();
        detector.Record(Start.AddSeconds(20)).Should().BeFalse();
        detector.IsActive(Start.AddSeconds(20)).Should().BeFalse();
    }

    [Fact]
    public void ActiveBulkImportShouldStayActiveWhileEventsKeepArriving()
    {
        var detector = CreateDetector();
        detector.Record(Start);
        detector.Record(Start.AddSeconds(1));
        detector.Record(Start.AddSeconds(2)).Should().BeTrue();

        // Extend past the original cooldown with one more event.
        detector.Record(Start.AddSeconds(16)).Should().BeTrue();
        detector.IsActive(Start.AddSeconds(25)).Should().BeTrue();
    }

    [Fact]
    public void BulkImportShouldExpireAfterCooldown()
    {
        var detector = CreateDetector();
        detector.Record(Start);
        detector.Record(Start.AddSeconds(1));
        detector.Record(Start.AddSeconds(2)).Should().BeTrue();

        detector.IsActive(Start.AddSeconds(16)).Should().BeTrue();
        detector.IsActive(Start.AddSeconds(17)).Should().BeFalse();
        detector.Record(Start.AddSeconds(17)).Should().BeFalse();
    }

    private static BulkImportDetector CreateDetector()
    {
        return new BulkImportDetector(3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
    }
}
