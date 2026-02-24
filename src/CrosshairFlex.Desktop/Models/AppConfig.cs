namespace CrosshairFlex.Desktop.Models;

public sealed class AppConfig
{
    public string Version { get; set; } = "1.0.0";
    public bool FirstLaunchCompleted { get; set; }
    public bool StartWithWindows { get; set; }
    public bool SafeMode { get; set; }
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "en";
    public string LastProfileId { get; set; } = string.Empty;
    public bool EnableTemporaryOnRightMouse { get; set; } = true;
    public bool EnableTemporaryOnLeftMouse { get; set; }
    public List<CrosshairProfile> Profiles { get; set; } = [CrosshairProfile.CreateDefault()];
}
