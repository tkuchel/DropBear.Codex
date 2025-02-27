#region

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides extension and utility methods for string manipulations, optimized for .NET 8.
/// </summary>
public static class StringHelper
{
    /// <summary>
    ///     Converts the first character of a string to uppercase.
    ///     Uses <see cref="Span{T}" /> for optimized string operations.
    /// </summary>
    public static Result<string, StringError> FirstCharToUpper(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
        {
            return Result<string, StringError>.Failure(new StringError("Input string cannot be empty."));
        }

        try
        {
            Span<char> result = stackalloc char[input.Length];
            input.CopyTo(result);
            result[0] = char.ToUpper(result[0], CultureInfo.CurrentCulture);
            return Result<string, StringError>.Success(result.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, StringError>.Failure(new StringError("Failed to capitalize first character.", ex));
        }
    }

    /// <summary>
    ///     Converts a string to a SHA256 hash.
    ///     Optimized with <see cref="Span{T}" /> and <see cref="stackalloc" />.
    /// </summary>
    public static Result<string, StringError> ToSha256(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
        {
            return Result<string, StringError>.Failure(new StringError("Input string cannot be empty."));
        }

        try
        {
            Span<byte> inputBytes = stackalloc byte[Encoding.UTF8.GetByteCount(input)];
            Encoding.UTF8.GetBytes(input, inputBytes);
            Span<byte> hashBytes = stackalloc byte[32];
            SHA256.HashData(inputBytes, hashBytes);
            return Result<string, StringError>.Success(Convert.ToHexString(hashBytes));
        }
        catch (Exception ex)
        {
            return Result<string, StringError>.Failure(new StringError("Failed to generate SHA256 hash.", ex));
        }
    }

    /// <summary>
    ///     Limits the length of a string to a specified maximum.
    ///     Uses <see cref="Span{T}" /> for optimized slicing.
    /// </summary>
    public static Result<string, StringError> LimitTo(ReadOnlySpan<char> data, int length)
    {
        if (data.IsEmpty)
        {
            return Result<string, StringError>.Failure(new StringError("Input string cannot be empty."));
        }

        try
        {
            return Result<string, StringError>.Success(data[..Math.Min(length, data.Length)].ToString());
        }
        catch (Exception ex)
        {
            return Result<string, StringError>.Failure(new StringError("Failed to limit string length.", ex));
        }
    }
}
