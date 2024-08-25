#region

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core;

/// <summary>
///     Represents the outcome of an operation that includes a payload, with support for compression, hashing, and
///     validation.
/// </summary>
/// <typeparam name="T">The type of the payload data.</typeparam>
#pragma warning disable MA0048
public class ResultWithPayload<T> : IEquatable<ResultWithPayload<T>>
#pragma warning restore MA0048
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ResultWithPayload{T}" /> class.
    /// </summary>
    /// <param name="payload">The compressed payload data.</param>
    /// <param name="hash">The hash of the payload data.</param>
    /// <param name="state">The state of the result.</param>
    /// <param name="errorMessage">The error message, if any.</param>
    internal ResultWithPayload(byte[]? payload, string? hash, ResultState state, string? errorMessage)
    {
        Payload = payload ?? Array.Empty<byte>();
        Hash = hash ?? string.Empty;
        State = state;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    /// <summary>
    ///     Gets the compressed payload data.
    /// </summary>
    public byte[] Payload { get; }

    /// <summary>
    ///     Gets the hash of the payload data.
    /// </summary>
    public string Hash { get; }

    /// <summary>
    ///     Gets the state of the result.
    /// </summary>
    public ResultState State { get; }

    /// <summary>
    ///     Gets the error message associated with the result, if any.
    /// </summary>
    public string ErrorMessage { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the result is valid by checking the state and validating the hash.
    /// </summary>
    public bool IsValid => State == ResultState.Success && ValidateHash(Payload, Hash);

    /// <inheritdoc />
    public bool Equals(ResultWithPayload<T>? other)
    {
        if (other is null)
        {
            return false;
        }

        return State == other.State && string.Equals(Hash, other.Hash, StringComparison.Ordinal) &&
               Payload.SequenceEqual(other.Payload);
    }

    /// <summary>
    ///     Updates the error message associated with the result.
    /// </summary>
    /// <param name="errorMessage">The new error message.</param>
    public void UpdateErrorMessage(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    /// <summary>
    ///     Decompresses the payload and deserializes it into an object of type <typeparamref name="T" />.
    /// </summary>
    /// <returns>A <see cref="Result{T}" /> containing the deserialized object if successful, or a failure result otherwise.</returns>
    public Result<T?> DecompressAndDeserialize()
    {
        if (State is not ResultState.Success)
        {
            return Result<T?>.Failure("Operation failed, cannot decompress.");
        }

        try
        {
            using var input = new MemoryStream(Payload);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            var decompressedJson = reader.ReadToEnd();

            if (!ValidateHash(Payload, Hash))
            {
                throw new InvalidOperationException("Data corruption detected during decompression.");
            }

            var deserializedData = JsonSerializer.Deserialize<T>(decompressedJson);
            return deserializedData is not null
                ? Result<T?>.Success(deserializedData)
                : Result<T?>.Failure("Deserialization returned null.");
        }
        catch (Exception ex)
        {
            return Result<T?>.Failure(ex.Message);
        }
    }

    /// <summary>
    ///     Validates the hash of the data against the expected hash.
    /// </summary>
    /// <param name="data">The data to validate.</param>
    /// <param name="expectedHash">The expected hash value.</param>
    /// <returns>True if the hash matches the expected value; otherwise, false.</returns>
    private static bool ValidateHash(byte[] data, string expectedHash)
    {
        var actualHash = ComputeHash(data);
        return string.Equals(actualHash, expectedHash, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Computes the SHA-256 hash of the provided data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The base64-encoded string representation of the hash.</returns>
    private static string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    ///     Compresses the provided data asynchronously.
    /// </summary>
    /// <param name="data">The data to compress.</param>
    /// <returns>A task representing the asynchronous operation, containing the compressed data.</returns>
    private static async Task<byte[]> CompressAsync(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionMode.Compress))
        {
            await zip.WriteAsync(data).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Compresses the provided data.
    /// </summary>
    /// <param name="data">The data to compress.</param>
    /// <returns>The compressed data.</returns>
    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionMode.Compress))
        {
            zip.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as ResultWithPayload<T>);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(State, Hash, Payload);
    }

    /// <summary>
    ///     Creates a successful result with the specified payload.
    /// </summary>
    /// <param name="data">The payload data.</param>
    /// <returns>A new <see cref="ResultWithPayload{T}" /> representing a successful result with the payload.</returns>
    public static ResultWithPayload<T> SuccessWithPayload(T data)
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(data);
            var compressedData = Compress(Encoding.UTF8.GetBytes(jsonData));
            var hash = ComputeHash(compressedData);
            return new ResultWithPayload<T>(compressedData, hash, ResultState.Success, string.Empty);
        }
        catch (JsonException)
        {
            return new ResultWithPayload<T>(Array.Empty<byte>(), string.Empty, ResultState.Failure,
                "Serialization failed.");
        }
        catch (Exception ex)
        {
            return new ResultWithPayload<T>(Array.Empty<byte>(), string.Empty, ResultState.Failure, ex.Message);
        }
    }

    /// <summary>
    ///     Creates a failed result with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A new <see cref="ResultWithPayload{T}" /> representing a failed result.</returns>
    public static ResultWithPayload<T> FailureWithPayload(string error)
    {
        return new ResultWithPayload<T>(Array.Empty<byte>(), string.Empty, ResultState.Failure, error);
    }

    /// <summary>
    ///     Creates a successful result with the specified payload asynchronously.
    /// </summary>
    /// <param name="data">The payload data.</param>
    /// <returns>A task representing the asynchronous operation, containing the successful result with the payload.</returns>
    public static async Task<ResultWithPayload<T>> SuccessWithPayloadAsync(T data)
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(data);
            var compressedData = await CompressAsync(Encoding.UTF8.GetBytes(jsonData)).ConfigureAwait(false);
            var hash = ComputeHash(compressedData);
            return new ResultWithPayload<T>(compressedData, hash, ResultState.Success, string.Empty);
        }
        catch (JsonException)
        {
            return new ResultWithPayload<T>(Array.Empty<byte>(), string.Empty, ResultState.PartialSuccess,
                "Serialization partially failed.");
        }
        catch (Exception ex)
        {
            return new ResultWithPayload<T>(Array.Empty<byte>(), string.Empty, ResultState.Failure, ex.Message);
        }
    }
}
