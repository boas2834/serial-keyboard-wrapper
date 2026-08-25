namespace SerialHidWrapper.Core;

public enum OutputSuffix
{
    None,
    Enter,
    Tab
}

public sealed class AppSettings
{
    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public int InactivityTimeoutMs { get; set; } = 200;
    public OutputSuffix Suffix { get; set; } = OutputSuffix.Enter;
    public bool AutoConnect { get; set; } = true;
    public bool AutoStart { get; set; }

    public IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(PortName))
            errors.Add("Bitte einen COM-Port auswählen.");
        if (BaudRate is < 75 or > 921600)
            errors.Add("Die Baudrate muss zwischen 75 und 921600 liegen.");
        if (DataBits is < 5 or > 8)
            errors.Add("Die Anzahl der Datenbits muss zwischen 5 und 8 liegen.");
        if (!AllowedParity.Contains(Parity, StringComparer.OrdinalIgnoreCase))
            errors.Add("Die ausgewählte Parität ist ungültig.");
        if (!AllowedStopBits.Contains(StopBits, StringComparer.OrdinalIgnoreCase))
            errors.Add("Die ausgewählte Anzahl der Stoppbits ist ungültig.");
        if (InactivityTimeoutMs is < 50 or > 5000)
            errors.Add("Der Timeout muss zwischen 50 und 5000 ms liegen.");

        return errors;
    }

    public AppSettings Copy() => new()
    {
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        Parity = Parity,
        StopBits = StopBits,
        InactivityTimeoutMs = InactivityTimeoutMs,
        Suffix = Suffix,
        AutoConnect = AutoConnect,
        AutoStart = AutoStart
    };

    public static readonly string[] AllowedParity = ["None", "Odd", "Even", "Mark", "Space"];
    public static readonly string[] AllowedStopBits = ["One", "OnePointFive", "Two"];
}

