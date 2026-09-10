using System.Security.Cryptography;

namespace StackMeet.Api.Activities.SportStacking.Identity;

public interface INadiTrackIdGenerator
{
    string Generate();
}

/// <summary>
/// Cryptographically strong generator for non-sequential public NADITrack athlete identifiers.
/// Database uniqueness remains the final collision authority.
/// </summary>
public sealed class CryptographicNadiTrackIdGenerator : INadiTrackIdGenerator
{
    public string Generate()
    {
        Span<char> body = stackalloc char[NadiTrackIdRules.BodyLength];
        for (var index = 0; index < body.Length; index++)
        {
            body[index] = NadiTrackIdRules.Alphabet[RandomNumberGenerator.GetInt32(NadiTrackIdRules.Alphabet.Length)];
        }

        return NadiTrackIdRules.Prefix + new string(body);
    }
}
