using SerialHidWrapper.Core;

namespace SerialHidWrapper.Core.Tests;

public sealed class ScanFramerTests
{
    [Theory]
    [InlineData("ABC\r")]
    [InlineData("ABC\n")]
    [InlineData("ABC\r\n")]
    public void TerminatorsProduceExactlyOneScan(string input)
    {
        var framer = new ScanFramer();

        var results = framer.Push(input);

        Assert.Single(results);
        Assert.Equal("ABC", results[0].Scan);
    }

    [Fact]
    public void MultipleScansInOneChunkAreSeparated()
    {
        var results = new ScanFramer().Push("ONE\rTWO\nTHREE\r\n");

        Assert.Equal(["ONE", "TWO", "THREE"], results.Select(result => result.Scan));
    }

    [Fact]
    public void TimeoutFlushesIncompleteScan()
    {
        var framer = new ScanFramer();
        Assert.Empty(framer.Push("12345"));

        var result = framer.FlushOnTimeout();

        Assert.Equal("12345", result.Scan);
        Assert.False(result.Overflowed);
        Assert.False(framer.FlushOnTimeout().HasScan);
    }

    [Fact]
    public void EmptyInputAndRepeatedTerminatorsAreIgnored()
    {
        var framer = new ScanFramer();

        Assert.Empty(framer.Push("\r\n\r\n"));
        Assert.False(framer.FlushOnTimeout().HasScan);
    }

    [Fact]
    public void CharactersArePreserved()
    {
        var results = new ScanFramer().Push("A-12/äöü\r");

        Assert.Equal("A-12/äöü", Assert.Single(results).Scan);
    }

    [Fact]
    public void OverflowIsReportedAndFrameIsDiscarded()
    {
        var framer = new ScanFramer(4);

        var results = framer.Push("ABCDE-rest\rOK\r");

        Assert.Equal(2, results.Count);
        Assert.True(results[0].Overflowed);
        Assert.Equal("OK", results[1].Scan);
    }
}

