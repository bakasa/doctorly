using Doctorly.Domain.Exceptions;
using Doctorly.Domain.ValueObjects;

namespace Doctorly.Domain.Tests.ValueObjects;

public class TimeRangeTests
{
    [Fact]
    public void Constructor_EndBeforeStart_Throws()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMinutes(-1);

        Assert.Throws<DomainException>(() => new TimeRange(start, end));
    }

    [Fact]
    public void Constructor_EndEqualsStart_Throws()
    {
        var start = DateTimeOffset.UtcNow;

        Assert.Throws<DomainException>(() => new TimeRange(start, start));
    }

    [Fact]
    public void Constructor_ValidRange_Succeeds()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMinutes(30);

        var range = new TimeRange(start, end);

        Assert.Equal(start, range.Start);
        Assert.Equal(end, range.End);
    }

    [Fact]
    public void Overlaps_OverlappingRanges_ReturnsTrue()
    {
        var start = DateTimeOffset.UtcNow;
        var a = new TimeRange(start, start.AddMinutes(30));
        var b = new TimeRange(start.AddMinutes(15), start.AddMinutes(45));

        Assert.True(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_DisjointRanges_ReturnsFalse()
    {
        var start = DateTimeOffset.UtcNow;
        var a = new TimeRange(start, start.AddMinutes(30));
        var b = new TimeRange(start.AddMinutes(30), start.AddMinutes(60));

        Assert.False(a.Overlaps(b));
    }
}
