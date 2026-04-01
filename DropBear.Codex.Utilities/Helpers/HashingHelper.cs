#region

using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for cryptographic operations, optimized for .NET 8.
/// </summary>
public static class HashingHelper
{
    /// <summary>
    ///     Generates a cryptographically secure random salt of the specified size.
    /// </summary>
    public static Result<byte[], HashingError> GenerateRandomSalt(int saltSize)
    {
        if (saltSize <= 0)
        {
            return Result<byte[], HashingError>.Failure(new HashingError("Salt size must be a positive integer."));
        }

        Span<byte> buffer = stackalloc byte[saltSize];
        RandomNumberGenerator.Fill(buffer);
        return Result<byte[], HashingError>.Success(buffer.ToArray());
    }

    /// <summary>
    ///     Converts a byte array to its Base64 string representation.
    ///     Uses <see cref="Span{T}" /> for optimized conversion.
    /// </summary>
    public static Result<string, HashingError> ConvertByteArrayToBase64String(ReadOnlySpan<byte> byteArray)
    {
        if (byteArray.IsEmpty)
        {
            return Result<string, HashingError>.Failure(new HashingError("Byte array cannot be empty."));
        }

        return Result<string, HashingError>.Success(Convert.ToBase64String(byteArray));
    }

    /// <summary>
    ///     Converts a Base64 string back into a byte array.
    /// </summary>
    public static Result<byte[], HashingError> ConvertBase64StringToByteArray(ReadOnlySpan<char> base64String)
    {
        if (base64String.IsEmpty)
        {
            return Result<byte[], HashingError>.Failure(new HashingError("Base64 string cannot be empty."));
        }

        try
        {
            return Result<byte[], HashingError>.Success(Convert.FromBase64String(base64String.ToString()));
        }
        catch (FormatException ex)
        {
            return Result<byte[], HashingError>.Failure(new HashingError("Invalid Base64 format.", ex));
        }
    }

    /// <summary>
    ///     Computes the SHA256 hash of a string.
    ///     Uses <see cref="Span{T}" /> for optimized hashing.
    /// </summary>
    public static Result<string, HashingError> ToSha256(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
        {
            return Result<string, HashingError>.Failure(new HashingError("Input string cannot be empty."));
        }

        Span<byte> inputBytes = stackalloc byte[Encoding.UTF8.GetByteCount(input)];
        Encoding.UTF8.GetBytes(input, inputBytes);
        Span<byte> hashBytes = stackalloc byte[32];
        SHA256.HashData(inputBytes, hashBytes);
        return Result<string, HashingError>.Success(Convert.ToHexString(hashBytes));
    }
}
