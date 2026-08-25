using System.IO.Ports;
using System.Text;
using SerialHidWrapper.Core;

namespace SerialHidWrapper.Infrastructure;

internal enum ScannerConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    WaitingForPort
}

internal readonly record struct ScannerStatus(ScannerConnectionState State, string Message);

internal interface ISerialScannerService : IDisposable
{
    event Action<string>? ScanReceived;
    event Action<ScannerStatus>? StatusChanged;
    event Action<string>? NonFatalError;

    bool IsConnectionRequested { get; }
    void Start(AppSettings settings);
    void Stop();
}

internal sealed class SerialScannerService : ISerialScannerService
{
    private static readonly TimeSpan ConnectionCheckInterval = TimeSpan.FromSeconds(2);

    private readonly object _sync = new();
    private readonly ScanFramer _framer = new();
    private readonly System.Threading.Timer _connectionTimer;
    private readonly System.Threading.Timer _inactivityTimer;

    private SerialPort? _port;
    private AppSettings? _settings;
    private bool _connectionRequested;
    private bool _openingPort;
    private bool _disposed;

    public SerialScannerService()
    {
        _connectionTimer = new System.Threading.Timer(CheckConnection, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _inactivityTimer = new System.Threading.Timer(FlushAfterInactivity, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event Action<string>? ScanReceived;
    public event Action<ScannerStatus>? StatusChanged;
    public event Action<string>? NonFatalError;

    public bool IsConnectionRequested
    {
        get
        {
            lock (_sync)
                return _connectionRequested;
        }
    }

    public void Start(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = settings.GetValidationErrors();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(settings));

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _settings = settings.Copy();
            _connectionRequested = true;
            ClosePortLocked();
            _framer.Reset();
            _connectionTimer.Change(TimeSpan.Zero, ConnectionCheckInterval);
        }

        PublishStatus(ScannerConnectionState.Connecting, $"Verbinde mit {settings.PortName} …");
        TryOpenPort();
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _connectionRequested = false;
            _connectionTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _inactivityTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            ClosePortLocked();
            _framer.Reset();
        }

        PublishStatus(ScannerConnectionState.Disconnected, "Nicht verbunden");
    }

    private void CheckConnection(object? state)
    {
        string? portName;
        bool isOpen;

        lock (_sync)
        {
            if (_disposed || !_connectionRequested || _settings is null)
                return;

            portName = _settings.PortName;
            isOpen = _port?.IsOpen == true;
        }

        var portExists = PortExists(portName);
        if (isOpen && portExists)
            return;

        if (isOpen)
            DisconnectForRetry($"{portName} wurde getrennt – warte auf den Port");

        TryOpenPort();
    }

    private void TryOpenPort()
    {
        AppSettings settings;

        lock (_sync)
        {
            if (_disposed || !_connectionRequested || _settings is null || _port?.IsOpen == true || _openingPort)
                return;
            settings = _settings.Copy();
            _openingPort = true;
        }

        try
        {
            if (!PortExists(settings.PortName))
            {
                PublishStatus(ScannerConnectionState.WaitingForPort, $"Warte auf {settings.PortName}");
                return;
            }

            PublishStatus(ScannerConnectionState.Connecting, $"Verbinde mit {settings.PortName} …");

            SerialPort? candidate = null;
            try
            {
                candidate = new SerialPort(
                    settings.PortName,
                    settings.BaudRate,
                    Enum.Parse<Parity>(settings.Parity, ignoreCase: true),
                    settings.DataBits,
                    Enum.Parse<StopBits>(settings.StopBits, ignoreCase: true))
                {
                    Encoding = Encoding.ASCII,
                    Handshake = Handshake.None,
                    ReadTimeout = settings.InactivityTimeoutMs,
                    WriteTimeout = 1000
                };
                candidate.DataReceived += HandleDataReceived;
                candidate.ErrorReceived += HandleErrorReceived;
                candidate.Open();

                lock (_sync)
                {
                    if (_disposed || !_connectionRequested)
                    {
                        candidate.DataReceived -= HandleDataReceived;
                        candidate.ErrorReceived -= HandleErrorReceived;
                        candidate.Close();
                        candidate.Dispose();
                        return;
                    }

                    ClosePortLocked();
                    _port = candidate;
                    candidate = null;
                }

                PublishStatus(ScannerConnectionState.Connected, $"Verbunden: {settings.PortName}");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                if (candidate is not null)
                {
                    candidate.DataReceived -= HandleDataReceived;
                    candidate.ErrorReceived -= HandleErrorReceived;
                    candidate.Dispose();
                }
                PublishStatus(ScannerConnectionState.WaitingForPort, $"{settings.PortName} nicht verfügbar – neuer Versuch folgt");
            }
        }
        finally
        {
            lock (_sync)
                _openingPort = false;
        }
    }

    private static bool PortExists(string portName)
    {
        try
        {
            return SerialPort.GetPortNames().Contains(portName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void HandleDataReceived(object sender, SerialDataReceivedEventArgs args)
    {
        try
        {
            string chunk;
            IReadOnlyList<ScanFrameResult> results;
            int timeout;

            lock (_sync)
            {
                if (_disposed || !_connectionRequested || !ReferenceEquals(sender, _port) || _settings is null)
                    return;

                chunk = _port.ReadExisting();
                if (chunk.Length == 0)
                    return;

                results = _framer.Push(chunk);
                timeout = _settings.InactivityTimeoutMs;
                _inactivityTimer.Change(TimeSpan.FromMilliseconds(timeout), Timeout.InfiniteTimeSpan);
            }

            PublishFrameResults(results);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            DisconnectForRetry("Serielle Verbindung unterbrochen – neuer Versuch folgt");
        }
    }

    private void HandleErrorReceived(object sender, SerialErrorReceivedEventArgs args) =>
        DisconnectForRetry("Fehler am seriellen Port – neuer Versuch folgt");

    private void FlushAfterInactivity(object? state)
    {
        ScanFrameResult result;
        lock (_sync)
        {
            if (_disposed || !_connectionRequested || _port?.IsOpen != true)
                return;
            result = _framer.FlushOnTimeout();
        }

        if (result.HasScan)
            ScanReceived?.Invoke(result.Scan!);
    }

    private void PublishFrameResults(IReadOnlyList<ScanFrameResult> results)
    {
        foreach (var result in results)
        {
            if (result.Overflowed)
                NonFatalError?.Invoke($"Scan verworfen: mehr als {ScanFramer.DefaultMaximumLength} Zeichen");
            if (result.HasScan)
                ScanReceived?.Invoke(result.Scan!);
        }
    }

    private void DisconnectForRetry(string message)
    {
        lock (_sync)
        {
            if (_disposed || !_connectionRequested)
                return;
            _inactivityTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            ClosePortLocked();
            _framer.Reset();
        }

        PublishStatus(ScannerConnectionState.WaitingForPort, message);
    }

    private void ClosePortLocked()
    {
        if (_port is null)
            return;

        _port.DataReceived -= HandleDataReceived;
        _port.ErrorReceived -= HandleErrorReceived;
        try
        {
            if (_port.IsOpen)
                _port.Close();
        }
        catch (IOException)
        {
            // The device may already have disappeared.
        }
        finally
        {
            _port.Dispose();
            _port = null;
        }
    }

    private void PublishStatus(ScannerConnectionState state, string message) =>
        StatusChanged?.Invoke(new ScannerStatus(state, message));

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _connectionRequested = false;
            ClosePortLocked();
            _framer.Reset();
        }

        _connectionTimer.Dispose();
        _inactivityTimer.Dispose();
    }
}
