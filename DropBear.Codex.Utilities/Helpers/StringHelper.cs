#region

using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides extension and utility methods for string manipulations.
/// </summary>
public static partial class StringHelper
{
    /// <summary>
    ///     Formats the string according to the provided arguments.
    /// </summary>
    /// <param name="template">The string template containing placeholders.</param>
    /// <param name="args">The arguments to format into the template.</param>
    /// <returns>A formatted string with arguments interpolated.</returns>
    public static string FormatWith(this string template, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(template);
        return string.Format(template, args);
    }

    /// <summary>
    ///     Converts the first character of a string to uppercase.
    /// </summary>
    /// <param name="input">The string to modify.</param>
    /// <returns>The input string with its first character in uppercase.</returns>
    public static string FirstCharToUpper(this string input)
    {
        return string.IsNullOrEmpty(input) ? input : char.ToUpper(input[0], CultureInfo.CurrentCulture) + input[1..];
    }

    /// <summary>
    ///     Capitalizes the first letter of each sentence in a string.
    /// </summary>
    /// <param name="input">The input string to capitalize.</param>
    /// <returns>The string with each sentence capitalized.</returns>
    public static string CapitalizeSentence(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var result = new StringBuilder(input.Length);
        var shouldCapitalize = true;

        foreach (var c in input)
        {
            if (shouldCapitalize && char.IsLetter(c))
            {
                result.Append(char.ToUpper(c, CultureInfo.InvariantCulture));
                shouldCapitalize = false;
            }
            else
            {
                result.Append(c);
                if (c is '.' or '!' or '?')
                {
                    shouldCapitalize = true;
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    ///     Parses a date string and extracts its components.
    /// </summary>
    /// <param name="dateString">The date string in various formats (MM/dd/yy, M/d/yy, dd/MM/yyyy).</param>
    /// <returns>Tuple containing month, day, and year; or null if parsing fails.</returns>
    public static (int Month, int Day, int Year)? ExtractDateParts(this string dateString)
    {
        if (DateTime.TryParseExact(dateString, new[] { "MM/dd/yy", "M/d/yy", "dd/MM/yyyy" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return (parsedDate.Month, parsedDate.Day, parsedDate.Year);
        }

        return null;
    }

    /// <summary>
    ///     Ensures the string ends with a specified character.
    /// </summary>
    /// <param name="str">The string to process.</param>
    /// <param name="c">The character to append if not present.</param>
    /// <param name="comparisonType">Specifies the culture, case, and sort rules to be used.</param>
    /// <returns>The original string with the specified character appended if necessary.</returns>
    public static string EnsureEndsWith(this string str, char c,
        StringComparison comparisonType = StringComparison.Ordinal)
    {
        ArgumentNullException.ThrowIfNull(str);
        if (!str.EndsWith(c.ToString(CultureInfo.InvariantCulture), comparisonType))
        {
            return str + c;
        }

        return str;
    }

    /// <summary>
    ///     Converts a string to a SHA256 hash.
    /// </summary>
    /// <param name="str">The string to convert.</param>
    /// <returns>The SHA256 hash of the string.</returns>
    public static string ToSha256(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        var inputBytes = Encoding.UTF8.GetBytes(str);
        var hashBytes = SHA256.HashData(inputBytes);

        var sb = new StringBuilder();
        foreach (var hashByte in hashBytes)
        {
            sb.Append(hashByte.ToString("X2"));
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Converts a string to a byte array using UTF8 encoding.
    /// </summary>
    /// <param name="str">The string to convert.</param>
    /// <returns>The byte array representation of the string.</returns>
    public static byte[] GetBytes(this string str)
    {
        return GetBytes(str, Encoding.UTF8);
    }

    /// <summary>
    ///     Converts a string to a byte array using a specified encoding.
    /// </summary>
    /// <param name="str">The string to convert.</param>
    /// <param name="encoding">The encoding to use.</param>
    /// <returns>The byte array representation of the string.</returns>
    private static byte[] GetBytes(this string str, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(str);
        ArgumentNullException.ThrowIfNull(encoding);

        return encoding.GetBytes(str);
    }

    /// <summary>
    ///     Limits the length of a string to a specified maximum.
    /// </summary>
    /// <param name="data">The string to limit.</param>
    /// <param name="length">The maximum length.</param>
    /// <returns>A string truncated to the maximum length if necessary.</returns>
    public static string LimitTo(this string data, int length)
    {
        return string.IsNullOrEmpty(data) || data.Length <= length ? data : data[..length];
    }

    /// <summary>
    ///     Converts a string to PascalCase with a regex evaluation timeout.
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>The input string in PascalCase.</returns>
    public static string ToPascalCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        var formattedString = Regex.Replace(input, "(?<=[a-z])([A-Z])", " $1", RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1)).Trim();
        return textInfo.ToTitleCase(formattedString.ToUpperInvariant())
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Inserts spaces before capital letters in a string, effectively separating words.
    /// </summary>
    /// <param name="input">The string to modify.</param>
    /// <returns>The modified string with spaces inserted.</returns>
    public static string Wordify(this string input)
    {
        return WordifyRegex().Replace(input, " $1").Trim();
    }

    /// <summary>
    ///     Determines whether a string contains a specified substring using specified comparison rules.
    /// </summary>
    /// <param name="source">The source string.</param>
    /// <param name="toCheck">The string to check for.</param>
    /// <param name="comp">The comparison rules to apply.</param>
    /// <returns>true if the source string contains the specified substring; otherwise, false.</returns>
    public static bool Contains(this string source, string toCheck, StringComparison comp)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(toCheck);
        return source.Contains(toCheck, comp);
    }

    /// <summary>
    ///     Converts a <see cref="SecureString" /> to a regular string.
    /// </summary>
    /// <param name="value">The <see cref="SecureString" /> to convert.</param>
    /// <returns>A regular string containing the same value as the <see cref="SecureString" />.</returns>
    public static string SecureStringToString(SecureString value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var ptr = Marshal.SecureStringToGlobalAllocUnicode(value);
        try
        {
            return Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }

    /// <summary>
    ///     Converts a regular string to a <see cref="SecureString" />.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A <see cref="SecureString" /> containing the same value as the input string.</returns>
    public static SecureString ToSecureString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var secureString = new SecureString();
        foreach (var c in value)
        {
            secureString.AppendChar(c);
        }

        secureString.MakeReadOnly();
        return secureString;
    }

    [GeneratedRegex("(?<=[a-z])([A-Z])", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex WordifyRegex();
}
