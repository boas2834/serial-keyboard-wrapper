using SerialHidWrapper.Core;
using SerialHidWrapper.Infrastructure;
using SerialHidWrapper.Ui;

namespace SerialHidWrapper;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ISettingsStore _settingsStore;
    private readonly ISerialScannerService _scanner;
    private readonly ScanOutputDispatcher _outputDispatcher;
    private readonly Control _uiDispatcher;
    private readonly Icon _applicationIcon;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _lastScanItem;
    private readonly ToolStripMenuItem _connectionItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _autoStartItem;

    private AppSettings _settings;
    private ScannerStatus _scannerStatus = new(ScannerConnectionState.Disconnected, "Nicht verbunden");
    private SettingsForm? _settingsForm;
    private string? _lastRawData;
    private DateTime? _lastRawDataAt;
    private string? _lastReceivedScan;
    private DateTime? _lastReceivedAt;
    private bool _explicitlyPaused;
    private bool _menuIsOpen;
    private bool _settingsAreOpen;
    private bool _exiting;

    public TrayApplicationContext(AppSettings settings, ISettingsStore settingsStore)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _scanner = new SerialScannerService();
        _outputDispatcher = new ScanOutputDispatcher(new WindowsKeyboardOutput());
        _applicationIcon = BarcodeIconFactory.Create();

        _uiDispatcher = new Control();
        _uiDispatcher.CreateControl();

        _statusItem = new ToolStripMenuItem("Nicht verbunden") { Enabled = false };
        _lastScanItem = new ToolStripMenuItem("Letzter Scan: –") { Enabled = false };
        _connectionItem = new ToolStripMenuItem("Verbinden", null, ToggleConnection);
        _pauseItem = new ToolStripMenuItem("Ausgabe pausieren", null, TogglePause);
        _autoStartItem = new ToolStripMenuItem("Mit Windows starten", null, ToggleAutoStart)
        {
            Checked = settings.AutoStart,
            CheckOnClick = false
        };

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange([
            _statusItem,
            _lastScanItem,
            new ToolStripSeparator(),
            _connectionItem,
            _pauseItem,
            new ToolStripMenuItem("Einstellungen …", null, (_, _) => ShowSettings()),
            _autoStartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Beenden", null, (_, _) => ExitThread())
        ]);
        _menu.Opening += (_, _) =>
        {
            _menuIsOpen = true;
            RefreshOutputPauseState();
        };
        _menu.Closed += (_, _) =>
        {
            _menuIsOpen = false;
            RefreshOutputPauseState();
        };

        _notifyIcon = new NotifyIcon
        {
            Icon = _applicationIcon,
            Text = "Serial HID Wrapper – Nicht verbunden",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        _scanner.ScanReceived += HandleScanReceived;
        _scanner.RawDataReceived += HandleRawDataReceived;
        _scanner.StatusChanged += status => RunOnUiThread(() => ApplyStatus(status));
        _scanner.NonFatalError += message => RunOnUiThread(() => ShowNonFatalError(message));

        ReconcileAutoStartAtLaunch();

        if (_settings.AutoConnect && _settings.GetValidationErrors().Count == 0)
            _scanner.Start(_settings);
        else if (string.IsNullOrWhiteSpace(_settings.PortName))
            ApplyStatus(new ScannerStatus(ScannerConnectionState.Disconnected, "Bitte COM-Port konfigurieren"));
    }

    private void ToggleConnection(object? sender, EventArgs args)
    {
        if (_scanner.IsConnectionRequested)
        {
            _scanner.Stop();
            return;
        }

        if (_settings.GetValidationErrors().Count > 0)
        {
            ShowSettings();
            if (_settings.GetValidationErrors().Count > 0)
                return;
        }

        _scanner.Start(_settings);
    }

    private void TogglePause(object? sender, EventArgs args)
    {
        _explicitlyPaused = !_explicitlyPaused;
        RefreshOutputPauseState();
        RefreshStatusDisplay();
    }

    private void ToggleAutoStart(object? sender, EventArgs args)
    {
        var desired = !_settings.AutoStart;
        if (!TryApplyAutoStart(desired))
            return;

        _settings.AutoStart = desired;
        PersistSettings();
        _autoStartItem.Checked = desired;
    }

    private void ShowSettings()
    {
        if (_settingsAreOpen)
            return;

        _settingsAreOpen = true;
        RefreshOutputPauseState();
        var oldSettings = _settings;

        try
        {
            using var dialog = new SettingsForm(_settings);
            _settingsForm = dialog;
            dialog.ShowConnectionStatus(_scannerStatus.Message);
            if (_lastRawData is not null && _lastRawDataAt is not null)
                dialog.ShowRawData(_lastRawData, _lastRawDataAt.Value);
            if (_lastReceivedScan is not null && _lastReceivedAt is not null)
                dialog.ShowLastScan(_lastReceivedScan, _lastReceivedAt.Value);

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            var updated = dialog.Settings;
            if (updated.AutoStart != oldSettings.AutoStart && !TryApplyAutoStart(updated.AutoStart))
                updated.AutoStart = oldSettings.AutoStart;

            _settings = updated;
            PersistSettings();
            _autoStartItem.Checked = _settings.AutoStart;
            _scanner.Start(_settings);
        }
        finally
        {
            _settingsForm = null;
            _settingsAreOpen = false;
            RefreshOutputPauseState();
        }
    }

    private void HandleScanReceived(string scan)
    {
        var timestamp = DateTime.Now;
        RunOnUiThread(() =>
        {
            _lastReceivedScan = scan;
            _lastReceivedAt = timestamp;
            _settingsForm?.ShowLastScan(scan, timestamp);
        });

        try
        {
            if (!_outputDispatcher.Dispatch(scan, _settings.Suffix))
                return;

            RunOnUiThread(() =>
            {
                _lastScanItem.Text = $"Letzter Scan: {timestamp:HH:mm:ss} ({scan.Length} Zeichen)";
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            RunOnUiThread(() => ShowNonFatalError("Tastatureingabe fehlgeschlagen. Läuft das Zielprogramm als Administrator?"));
        }
    }

    private void HandleRawDataReceived(string data)
    {
        var timestamp = DateTime.Now;
        RunOnUiThread(() =>
        {
            _lastRawData = data;
            _lastRawDataAt = timestamp;
            _settingsForm?.ShowRawData(data, timestamp);
        });
    }

    private void ApplyStatus(ScannerStatus status)
    {
        _scannerStatus = status;
        _settingsForm?.ShowConnectionStatus(status.Message);
        _connectionItem.Text = _scanner.IsConnectionRequested ? "Trennen" : "Verbinden";
        RefreshStatusDisplay();
    }

    private void RefreshStatusDisplay()
    {
        var prefix = _explicitlyPaused ? "Pausiert – " : string.Empty;
        var text = prefix + _scannerStatus.Message;
        _statusItem.Text = text;
        _pauseItem.Text = _explicitlyPaused ? "Ausgabe fortsetzen" : "Ausgabe pausieren";
        _pauseItem.Checked = _explicitlyPaused;
        _notifyIcon.Text = TruncateNotifyText("Serial HID Wrapper – " + text);
    }

    private void RefreshOutputPauseState() =>
        _outputDispatcher.IsPaused = _explicitlyPaused || _menuIsOpen || _settingsAreOpen;

    private void ShowNonFatalError(string message)
    {
        _notifyIcon.BalloonTipTitle = "Serial HID Wrapper";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
        _notifyIcon.ShowBalloonTip(4000);
    }

    private void ReconcileAutoStartAtLaunch()
    {
        if (!_settings.AutoStart)
            return;

        if (!TryApplyAutoStart(true))
        {
            _settings.AutoStart = false;
            _autoStartItem.Checked = false;
            PersistSettings();
        }
    }

    private bool TryApplyAutoStart(bool enabled)
    {
        try
        {
            AutoStartManager.SetEnabled(enabled);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException or IOException)
        {
            MessageBox.Show(
                "Die Autostart-Einstellung konnte nicht geändert werden.\n\n" + exception.Message,
                "Serial HID Wrapper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    private void PersistSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Die Einstellungen konnten nicht gespeichert werden.\n\n" + exception.Message,
                "Serial HID Wrapper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RunOnUiThread(Action action)
    {
        if (_exiting || _uiDispatcher.IsDisposed)
            return;

        try
        {
            if (_uiDispatcher.InvokeRequired)
                _uiDispatcher.BeginInvoke(action);
            else
                action();
        }
        catch (InvalidOperationException) when (_exiting || _uiDispatcher.IsDisposed)
        {
            // The message loop is shutting down.
        }
    }

    private static string TruncateNotifyText(string text) =>
        text.Length <= 63 ? text : text[..60] + "…";

    protected override void ExitThreadCore()
    {
        if (_exiting)
            return;

        _exiting = true;
        _notifyIcon.Visible = false;
        _scanner.Dispose();
        _notifyIcon.Dispose();
        _applicationIcon.Dispose();
        _menu.Dispose();
        _uiDispatcher.Dispose();
        base.ExitThreadCore();
    }
}
