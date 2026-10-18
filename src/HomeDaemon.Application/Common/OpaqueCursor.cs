using System.Text;

namespace HomeDaemon.Application.Common;

/// <summary>
/// Encodes and decodes the opaque cursors used by keyset pagination. The payload is a list of
/// formatted, invariant-culture components; base64url keeps it safe in a query string. Callers
/// own the meaning of the components and must validate them.
/// </summary>
internal static class OpaqueCursor
{
    private const char Separator = '|';

    public static string Encode(params string[] parts) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(Separator, parts)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static string[] Decode(string cursor, int expectedParts)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            throw new ApplicationValidationException("The cursor is empty.");
        }

        string decoded;
        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            var padded = normalized.PadRight(normalized.Length + ((4 - (normalized.Length % 4)) % 4), '=');
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch (FormatException ex)
        {
            throw new ApplicationValidationException("The cursor is not valid.", ex);
        }

        var parts = decoded.Split(Separator);
        if (parts.Length != expectedParts)
        {
            throw new ApplicationValidationException("The cursor is not valid.");
        }

        return parts;
    }
}
