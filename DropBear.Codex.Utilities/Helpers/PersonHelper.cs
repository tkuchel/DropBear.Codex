#region

using System.Collections.Frozen;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for formatting person-related information, optimized for .NET 8.
/// </summary>
public static class PersonHelper
{
    private static readonly FrozenDictionary<bool, string> NameFormats = new Dictionary<bool, string>
    {
        { true, "{0} {1}" }, // Given name first
        { false, "{0}, {1}" } // Family name first
    }.ToFrozenDictionary();

    /// <summary>
    ///     Formats a person's name efficiently using <see cref="Span{T}" />.
    /// </summary>
    public static Result<string, PersonError> FormatName(ReadOnlySpan<char> familyName, ReadOnlySpan<char> givenNames,
        bool givenNameFirst = false)
    {
        if (familyName.IsEmpty && givenNames.IsEmpty)
        {
            return Result<string, PersonError>.Failure(
                new PersonError("Both family name and given names cannot be empty."));
        }

        try
        {
            var format = NameFormats[givenNameFirst];
            return Result<string, PersonError>.Success(string
                .Format(format, givenNames.Trim().ToString(), familyName.Trim().ToString()).TrimEnd(','));
        }
        catch (Exception ex)
        {
            return Result<string, PersonError>.Failure(new PersonError("Error formatting name.", ex));
        }
    }

    /// <summary>
    ///     Formats an address from its individual components.
    /// </summary>
    public static Result<string, PersonError> FormatAddress(string? line1, string? line2, string? city, string? state,
        string? postCode, char separator = ' ')
    {
        if (string.IsNullOrWhiteSpace(line1) && string.IsNullOrWhiteSpace(line2) && string.IsNullOrWhiteSpace(city) &&
            string.IsNullOrWhiteSpace(state) && string.IsNullOrWhiteSpace(postCode))
        {
            return Result<string, PersonError>.Failure(
                new PersonError("At least one address component must be provided."));
        }

        try
        {
            var addressParts = new[] { line1, line2, city, state, postCode }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim());

            var result = string.Join(separator, addressParts);
            return Result<string, PersonError>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<string, PersonError>.Failure(new PersonError("Error formatting address.", ex));
        }
    }
}
