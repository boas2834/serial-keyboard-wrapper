namespace SerialHidWrapper.Core;

public interface IKeyboardOutput
{
    void Send(string text, OutputSuffix suffix);
}

public sealed class ScanOutputDispatcher(IKeyboardOutput keyboardOutput)
{
    private int _paused;

    public bool IsPaused
    {
        get => Volatile.Read(ref _paused) != 0;
        set => Volatile.Write(ref _paused, value ? 1 : 0);
    }

    public bool Dispatch(string scan, OutputSuffix suffix)
    {
        if (IsPaused || string.IsNullOrEmpty(scan))
            return false;

        keyboardOutput.Send(scan, suffix);
        return true;
    }
}

