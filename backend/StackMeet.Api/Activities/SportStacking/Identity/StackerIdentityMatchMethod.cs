namespace StackMeet.Api.Activities.SportStacking.Identity;

/// <summary>
/// Stable provenance values for permanent-identity to competition-entry associations.
/// These values record evidence or operator choice; they do not authorize automatic person merges.
/// </summary>
public static class StackerIdentityMatchMethod
{
    public const string CreatedNew = "CREATED_NEW";
    public const string NadiTrackId = "NADITRACK_ID";
    public const string WssaId = "WSSA_ID";
    public const string NameAndBirthDate = "NAME_AND_BIRTH_DATE";
    public const string Email = "EMAIL";
    public const string Phone = "PHONE";
    public const string Manual = "MANUAL";
}
