using SerialHidWrapper.Core;

namespace SerialHidWrapper.Core.Tests;

public sealed class ScanOutputDispatcherTests
{
    [Theory]
    [InlineData(OutputSuffix.None)]
    [InlineData(OutputSuffix.Enter)]
    [InlineData(OutputSuffix.Tab)]
    public void DispatchForwardsTextAndConfiguredSuffix(OutputSuffix suffix)
    {
        var keyboard = new RecordingKeyboardOutput();
        var dispatcher = new ScanOutputDispatcher(keyboard);

        Assert.True(dispatcher.Dispatch("ABC123", suffix));

        Assert.Equal(("ABC123", suffix), Assert.Single(keyboard.Calls));
    }

    [Fact]
    public void PausedDispatcherDropsScan()
    {
        var keyboard = new RecordingKeyboardOutput();
        var dispatcher = new ScanOutputDispatcher(keyboard) { IsPaused = true };

        Assert.False(dispatcher.Dispatch("SECRET", OutputSuffix.Enter));
        Assert.Empty(keyboard.Calls);
    }

    private sealed class RecordingKeyboardOutput : IKeyboardOutput
    {
        public List<(string Text, OutputSuffix Suffix)> Calls { get; } = [];

        public void Send(string text, OutputSuffix suffix) => Calls.Add((text, suffix));
    }
}
