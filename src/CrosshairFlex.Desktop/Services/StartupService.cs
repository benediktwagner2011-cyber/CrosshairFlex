using Microsoft.Win32;

namespace CrosshairFlex.Desktop.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CrosshairFlex";

    public void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);

        if (enabled)
        {
            var executable = Environment.ProcessPath ?? string.Empty;
            key?.SetValue(AppName, $"\"{executable}\"");
            return;
        }

        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
