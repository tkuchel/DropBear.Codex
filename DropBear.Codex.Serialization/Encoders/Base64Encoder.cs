#region

using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Encoders;

/// <summary>
///     Provides methods to encode and decode data using Base64 encoding.
/// </summary>
public sealed class Base64Encoder : IEncoder
{
    private readonly ILogger _logger;
    private readonly bool _useUrlSafeEncoding;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Base64Encoder" /> class.
    /// </summary>
    /// <param name="useUrlSafeEncoding">Whether to use URL-safe Base64 encoding.</param>
    public Base64Encoder(bool useUrlSafeEncoding = false)
    {
        _logger = LoggerFactory.Logger.ForContext<Base64Encoder>();
        _useUrlSafeEncoding = useUrlSafeEncoding;
        _logger.Information("Base64Encoder initialized with UseUrlSafeEncoding: {UseUrlSafeEncoding}",
            useUrlSafeEncoding);
    }

    /// <summary>
    ///     Encodes the specified data using Base64 encoding.
    /// </summary>
    /// <param name="data">The data to encode.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A result containing the Base64 encoded data on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    public async Task<Result<byte[], SerializationError>> EncodeAsync(byte[] data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(data, nameof(data));

            if (data.Length == 0)
            {
                return Result<byte[], SerializationError>.Success([]);
            }

            _logger.Information("Starting Base64 encoding of data with length {DataLength} bytes.", data.Length);

            // Make this CPU-bound operation awaitable with minimal overhead
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    string base64String;

                    if (_useUrlSafeEncoding)
                    {
                        // URL-safe Base64: Replace '+' with '-' and '/' with '_', remove padding '='
                        base64String = Convert.ToBase64String(data)
                            .Replace('+', '-')
                            .Replace('/', '_')
                            .TrimEnd('=');
                    }
                    else
                    {
                        base64String = Convert.ToBase64String(data);
                    }

                    var encodedBytes = Encoding.UTF8.GetBytes(base64String);

                    _logger.Information(
                        "Base64 encoding completed. Data encoded from {OriginalSize} to {EncodedSize} bytes.",
                        data.Length, encodedBytes.Length);

                    return Result<byte[], SerializationError>.Success(encodedBytes);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during Base64 encoding: {Message}", ex.Message);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError($"Base64 encoding failed: {ex.Message}") { Operation = "Encode" }, ex);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during Base64 encoding task creation: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Base64 encoding task creation failed: {ex.Message}") { Operation = "Encode" },
                ex);
        }
    }

    /// <summary>
    ///     Decodes the specified Base64 encoded data.
    /// </summary>
    /// <param name="encodedData">The encoded data to decode.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A result containing the decoded data on success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when encodedData is null.</exception>
    /// <exception cref="FormatException">Thrown when the encoded data has invalid Base64 format.</exception>
    public async Task<Result<byte[], SerializationError>> DecodeAsync(byte[] encodedData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(encodedData, nameof(encodedData));

            if (encodedData.Length == 0)
            {
                return Result<byte[], SerializationError>.Success([]);
            }

            _logger.Information("Starting Base64 decoding of data with length {EncodedDataLength} bytes.",
                encodedData.Length);

            // Make this CPU-bound operation awaitable with minimal overhead
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Convert the byte array to a string
                    var base64EncodedString = Encoding.UTF8.GetString(encodedData);

                    // Handle URL-safe Base64 encoding if necessary
                    if (_useUrlSafeEncoding)
                    {
                        // Restore standard Base64 format: Replace '-' with '+' and '_' with '/'
                        base64EncodedString = base64EncodedString
                            .Replace('-', '+')
                            .Replace('_', '/');

                        // Add padding if needed
                        switch (base64EncodedString.Length % 4)
                        {
                            case 2: base64EncodedString += "=="; break;
                            case 3: base64EncodedString += "="; break;
                        }
                    }

                    var decodedBytes = Convert.FromBase64String(base64EncodedString);

                    _logger.Information(
                        "Base64 decoding completed. Data decoded from {EncodedSize} to {DecodedSize} bytes.",
                        encodedData.Length, decodedBytes.Length);

                    return Result<byte[], SerializationError>.Success(decodedBytes);
                }
                catch (FormatException ex)
                {
                    _logger.Error(ex, "Invalid Base64 format during decoding: {Message}", ex.Message);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError($"Invalid Base64 format: {ex.Message}") { Operation = "Decode" }, ex);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during Base64 decoding: {Message}", ex.Message);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError($"Base64 decoding failed: {ex.Message}") { Operation = "Decode" }, ex);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during Base64 decoding task creation: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Base64 decoding task creation failed: {ex.Message}") { Operation = "Decode" },
                ex);
        }
    }

    /// <summary>
    ///     Gets information about the encoder.
    /// </summary>
    /// <returns>A dictionary containing encoder information.</returns>
    public IDictionary<string, object> GetEncoderInfo()
    {
        return new Dictionary<string, object>
(StringComparer.Ordinal)
        {
            ["EncodingType"] = "Base64", ["IsUrlSafe"] = _useUrlSafeEncoding, ["IsThreadSafe"] = true
        };
    }
}
