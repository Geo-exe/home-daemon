using System.Security.Cryptography;

namespace HomeDaemon.Api.Auth;

/// <summary>
/// Generates one-time passwords handed to accounts that must change them at first sign-in. The
/// alphabet avoids characters that are easy to confuse when the password is read aloud or
/// typed by hand, and the result always contains an upper-case letter, a lower-case letter and
/// a digit so it satisfies the default password policy.
/// </summary>
public static class TemporaryPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const int Length = 16;

    public static string Create()
    {
        Span<char> characters = stackalloc char[Length];
        characters[0] = Pick(Upper);
        characters[1] = Pick(Lower);
        characters[2] = Pick(Digits);

        const string all = Upper + Lower + Digits;
        for (var index = 3; index < Length; index++)
        {
            characters[index] = Pick(all);
        }

        // Shuffle so the guaranteed classes do not always sit in the same positions.
        RandomNumberGenerator.Shuffle(characters);
        return new string(characters);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
