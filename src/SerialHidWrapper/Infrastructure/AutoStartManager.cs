using Microsoft.Win32;

namespace SerialHidWrapper.Infrastructure;

internal static class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SerialHidWrapper";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Der Windows-Autostartschlüssel konnte nicht geöffnet werden.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Der Pfad der Anwendung konnte nicht ermittelt werden.");
        key.SetValue(ValueName, $"\"{executablePath}\"");
    }
}

