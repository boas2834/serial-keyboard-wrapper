using System.Text;

namespace SerialHidWrapper.Core;

public readonly record struct ScanFrameResult(string? Scan, bool Overflowed)
{
    public bool HasScan => !string.IsNullOrEmpty(Scan);
}

public sealed class ScanFramer
{
    public const int DefaultMaximumLength = 4096;

    private readonly StringBuilder _buffer = new();
    private readonly int _maximumLength;
    private bool _discardUntilBoundary;

    public ScanFramer(int maximumLength = DefaultMaximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        _maximumLength = maximumLength;
    }

    public IReadOnlyList<ScanFrameResult> Push(ReadOnlySpan<char> characters)
    {
        var results = new List<ScanFrameResult>();

        foreach (var character in characters)
        {
            if (character is '\r' or '\n')
            {
                var result = CompleteBoundary();
                if (result.HasScan || result.Overflowed)
                    results.Add(result);
                continue;
            }

            if (_discardUntilBoundary)
                continue;

            if (_buffer.Length >= _maximumLength)
            {
                _buffer.Clear();
                _discardUntilBoundary = true;
                results.Add(new ScanFrameResult(null, true));
                continue;
            }

            _buffer.Append(character);
        }

        return results;
    }

    public ScanFrameResult FlushOnTimeout()
    {
        if (_discardUntilBoundary)
        {
            Reset();
            return default;
        }

        if (_buffer.Length == 0)
            return default;

        var scan = _buffer.ToString();
        _buffer.Clear();
        return new ScanFrameResult(scan, false);
    }

    public void Reset()
    {
        _buffer.Clear();
        _discardUntilBoundary = false;
    }

    private ScanFrameResult CompleteBoundary()
    {
        if (_discardUntilBoundary)
        {
            Reset();
            return default;
        }

        if (_buffer.Length == 0)
            return default;

        var scan = _buffer.ToString();
        _buffer.Clear();
        return new ScanFrameResult(scan, false);
    }
}

