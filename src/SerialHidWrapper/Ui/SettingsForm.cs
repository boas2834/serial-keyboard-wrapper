using System.IO.Ports;
using SerialHidWrapper.Core;

namespace SerialHidWrapper.Ui;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _port = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown _baudRate = new() { Minimum = 75, Maximum = 921600, Increment = 100, Dock = DockStyle.Fill };
    private readonly NumericUpDown _dataBits = new() { Minimum = 5, Maximum = 8, Dock = DockStyle.Fill };
    private readonly ComboBox _parity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _stopBits = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown _timeout = new() { Minimum = 50, Maximum = 5000, Increment = 50, Dock = DockStyle.Fill };
    private readonly ComboBox _suffix = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckBox _autoConnect = new() { Text = "Beim Start automatisch verbinden", AutoSize = true };
    private readonly CheckBox _autoStart = new() { Text = "Mit Windows starten", AutoSize = true };

    public SettingsForm(AppSettings settings)
    {
        Settings = settings.Copy();
        Text = "Serial HID Wrapper – Einstellungen";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(470, 390);

        _parity.Items.AddRange(AppSettings.AllowedParity);
        _stopBits.Items.AddRange(AppSettings.AllowedStopBits);
        _suffix.Items.AddRange(["Kein Suffix", "Enter", "Tab"]);

        var refreshButton = new Button { Text = "Aktualisieren", AutoSize = true };
        refreshButton.Click += (_, _) => RefreshPorts(_port.Text);

        var portPanel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, AutoSize = true };
        portPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        portPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        portPanel.Controls.Add(_port, 0, 0);
        portPanel.Controls.Add(refreshButton, 1, 0);

        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 10,
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            AutoSize = true
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(grid, 0, "COM-Port:", portPanel);
        AddRow(grid, 1, "Baudrate:", _baudRate);
        AddRow(grid, 2, "Datenbits:", _dataBits);
        AddRow(grid, 3, "Parität:", _parity);
        AddRow(grid, 4, "Stoppbits:", _stopBits);
        AddRow(grid, 5, "Scan-Timeout (ms):", _timeout);
        AddRow(grid, 6, "Ausgabe-Suffix:", _suffix);

        var optionsPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        optionsPanel.Controls.Add(_autoConnect);
        optionsPanel.Controls.Add(_autoStart);
        grid.Controls.Add(optionsPanel, 0, 7);
        grid.SetColumnSpan(optionsPanel, 2);

        var okButton = new Button { Text = "Speichern", AutoSize = true };
        var cancelButton = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
        okButton.Click += SaveAndClose;
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        grid.Controls.Add(buttons, 0, 9);
        grid.SetColumnSpan(buttons, 2);

        Controls.Add(grid);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        RefreshPorts(settings.PortName);
        _baudRate.Value = Math.Clamp(settings.BaudRate, (int)_baudRate.Minimum, (int)_baudRate.Maximum);
        _dataBits.Value = Math.Clamp(settings.DataBits, (int)_dataBits.Minimum, (int)_dataBits.Maximum);
        _parity.SelectedItem = settings.Parity;
        _stopBits.SelectedItem = settings.StopBits;
        _timeout.Value = Math.Clamp(settings.InactivityTimeoutMs, (int)_timeout.Minimum, (int)_timeout.Maximum);
        _suffix.SelectedIndex = (int)settings.Suffix;
        _autoConnect.Checked = settings.AutoConnect;
        _autoStart.Checked = settings.AutoStart;
    }

    public AppSettings Settings { get; private set; }

    private static void AddRow(TableLayoutPanel grid, int row, string label, Control control)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label
        {
            Text = label,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private void RefreshPorts(string? selectedPort)
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!string.IsNullOrWhiteSpace(selectedPort) && !ports.Contains(selectedPort, StringComparer.OrdinalIgnoreCase))
            ports.Add(selectedPort);

        _port.BeginUpdate();
        _port.Items.Clear();
        _port.Items.AddRange(ports.ToArray());
        _port.EndUpdate();

        if (!string.IsNullOrWhiteSpace(selectedPort))
            _port.SelectedItem = ports.FirstOrDefault(port => port.Equals(selectedPort, StringComparison.OrdinalIgnoreCase));
        if (_port.SelectedIndex < 0 && _port.Items.Count > 0)
            _port.SelectedIndex = 0;
    }

    private void SaveAndClose(object? sender, EventArgs args)
    {
        var candidate = new AppSettings
        {
            PortName = _port.Text,
            BaudRate = Decimal.ToInt32(_baudRate.Value),
            DataBits = Decimal.ToInt32(_dataBits.Value),
            Parity = _parity.SelectedItem?.ToString() ?? "None",
            StopBits = _stopBits.SelectedItem?.ToString() ?? "One",
            InactivityTimeoutMs = Decimal.ToInt32(_timeout.Value),
            Suffix = (OutputSuffix)Math.Max(0, _suffix.SelectedIndex),
            AutoConnect = _autoConnect.Checked,
            AutoStart = _autoStart.Checked
        };

        var errors = candidate.GetValidationErrors();
        if (errors.Count > 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Settings = candidate;
        DialogResult = DialogResult.OK;
        Close();
    }
}
