namespace HomeDaemon.Application.Common;

/// <summary>
/// Validation and normalisation for the ISO 4217 currency code stored on the household. The
/// budget module keeps a single currency per household and only checks the shape of the code.
/// </summary>
public static class CurrencyCodes
{
    public static bool IsValid(string? value) =>
        value is { Length: 3 } code
        && code.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim().ToUpperInvariant();
    }
}
