using SerialHidWrapper.Infrastructure;

namespace SerialHidWrapper;

internal static class Program
{
    private const string MutexName = "Local\\SerialHidWrapper.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "Serial HID Wrapper wird bereits ausgeführt.",
                "Serial HID Wrapper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        var settingsStore = new JsonSettingsStore();
        var settings = settingsStore.Load();

        Application.Run(new TrayApplicationContext(settings, settingsStore));
    }
}

