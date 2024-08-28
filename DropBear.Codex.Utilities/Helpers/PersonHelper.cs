namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for formatting person-related information.
/// </summary>
public static class PersonHelper
{
    /// <summary>
    ///     Formats a person's name by combining the family name and given names.
    /// </summary>
    /// <param name="familyName">The family name.</param>
    /// <param name="givenNames">The given names.</param>
    /// <param name="givenNameFirst">Whether to place the given names first. Defaults to false.</param>
    /// <returns>The formatted name, combining family name and given names.</returns>
    public static string FormatName(string familyName, string givenNames, bool givenNameFirst = false)
    {
        if (string.IsNullOrWhiteSpace(familyName) && string.IsNullOrWhiteSpace(givenNames))
        {
            throw new ArgumentException("Both family name and given names cannot be empty.");
        }

        familyName = familyName?.Trim() ?? string.Empty;
        givenNames = givenNames?.Trim() ?? string.Empty;

        return givenNameFirst
            ? string.IsNullOrWhiteSpace(givenNames) ? familyName : $"{givenNames} {familyName}".TrimEnd()
            : string.IsNullOrWhiteSpace(familyName)
                ? givenNames
                : $"{familyName}, {givenNames}".TrimEnd(',');
    }

    /// <summary>
    ///     Formats an address from its individual components.
    /// </summary>
    /// <param name="line1">The first line of the address.</param>
    /// <param name="line2">The second line of the address (optional).</param>
    /// <param name="city">The city.</param>
    /// <param name="state">The state or region.</param>
    /// <param name="postCode">The postal or ZIP code.</param>
    /// <param name="separator">The separator to use between address components. Defaults to a single space.</param>
    /// <returns>The formatted address as a single string.</returns>
    public static string FormatAddress(string line1, string line2, string city, string state, string postCode,
        string separator = " ")
    {
        if (string.IsNullOrWhiteSpace(line1) && string.IsNullOrWhiteSpace(line2) &&
            string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(state) && string.IsNullOrWhiteSpace(postCode))
        {
            throw new ArgumentException("At least one address component must be provided.");
        }

        var addressParts = new[] { line1, line2, city, state, postCode }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim());

        return string.Join(separator, addressParts);
    }
}
