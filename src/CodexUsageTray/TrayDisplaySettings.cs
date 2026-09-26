using Microsoft.Win32;

namespace CodexUsageTray;

internal static class TrayDisplaySettings
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
    }

    public static int IconSize => Math.Clamp(SystemInformation.SmallIconSize.Width, 16, 64);
}
