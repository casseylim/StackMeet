namespace StackMeet.Api.Models;

/// <summary>
/// Stores server-side operational settings managed from the admin screen.
/// </summary>
/// <remarks>
/// Secret values must be protected before storage; this table is used for SMTP setup and similar runtime settings.
/// </remarks>
public sealed class AppSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsProtected { get; set; }
    public DateTime UpdatedAt { get; set; }
}
