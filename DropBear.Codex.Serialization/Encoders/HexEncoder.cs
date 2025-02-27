#region

using System.Buffers;
using System.Diagnostics;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Serialization.Encoders;

/// <summary>
///     Provides methods to encode and decode data using hexadecimal encoding.
/// </summary>
public sealed class HexEncoder : IEncoder
{
    /// <summary>
    ///     Lookup tables for fast hex character conversion.
    /// </summary>
    private static readonly char[] HexUpperCase =
    {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
    };

    private static readonly char[] HexLowerCase =
    {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'
    };

    private readonly ILogger _logger;
    private readonly bool _upperCase;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HexEncoder" /> class.
    /// </summary>
    /// <param name="upperCase">Whether to use uppercase hex characters.</param>
    public HexEncoder(bool upperCase = true)
    {
        _logger = LoggerFactory.Logger.ForContext<HexEncoder>();
        _upperCase = upperCase;
        _logger.Information("HexEncoder initialized with UpperCase: {UpperCase}", upperCase);
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> EncodeAsync(byte[] data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(data, nameof(data));

            if (data.Length == 0)
            {
                return Result<byte[], SerializationError>.Success(Array.Empty<byte>());
            }

            _logger.Information("Starting hexadecimal encoding of data with length {DataLength} bytes.", data.Length);

            // Make this CPU-bound operation awaitable with minimal overhead
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Allocate a stack-based buffer for small inputs
                    var charCount = data.Length * 2;
                    char[]? pooledArray = null;

                    // Use the stack for small inputs (up to 512 chars = 256 bytes),
                    // or rent from array pool for larger inputs
                    var hexChars = charCount <= 512
                        ? stackalloc char[charCount]
                        : (pooledArray = ArrayPool<char>.Shared.Rent(charCount)).AsSpan(0, charCount);

                    try
                    {
                        if (_upperCase)
                        {
                            // Faster than using BitConverter and string operations
                            for (int i = 0, j = 0; i < data.Length; i++)
                            {
                                var b = data[i];
                                hexChars[j++] = HexUpperCase[b >> 4];
                                hexChars[j++] = HexUpperCase[b & 0xF];
                            }
                        }
                        else
                        {
                            for (int i = 0, j = 0; i < data.Length; i++)
                            {
                                var b = data[i];
                                hexChars[j++] = HexLowerCase[b >> 4];
                                hexChars[j++] = HexLowerCase[b & 0xF];
                            }
                        }

                        // Convert the hex characters to bytes using UTF-8 encoding
                        var byteCount = Encoding.UTF8.GetByteCount(hexChars);
                        var encodedBytes = new byte[byteCount];
                        var written = Encoding.UTF8.GetBytes(hexChars, encodedBytes);
                        Debug.Assert(written == byteCount);

                        // Log, then return the success result
                        _logger.Information(
                            "Hexadecimal encoding completed. Data encoded from {OriginalSize} to {EncodedSize} bytes.",
                            data.Length, encodedBytes.Length);

                        return Result<byte[], SerializationError>.Success(encodedBytes);
                    }
                    finally
                    {
                        // Return the rented array to the pool if one was allocated
                        if (pooledArray != null)
                        {
                            ArrayPool<char>.Shared.Return(pooledArray);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during hexadecimal encoding: {Message}", ex.Message);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError($"Hexadecimal encoding failed: {ex.Message}") { Operation = "Encode" },
                        ex);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during hexadecimal encoding task creation: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Hexadecimal encoding task creation failed: {ex.Message}")
                {
                    Operation = "Encode"
                }, ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<byte[], SerializationError>> DecodeAsync(byte[] encodedData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(encodedData, nameof(encodedData));

            if (encodedData.Length == 0)
            {
                return Result<byte[], SerializationError>.Success(Array.Empty<byte>());
            }

            _logger.Information("Starting hexadecimal decoding of data with length {EncodedDataLength} bytes.",
                encodedData.Length);

            // Make this CPU-bound operation awaitable with minimal overhead
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Convert the byte array to a string
                    var hexString = Encoding.UTF8.GetString(encodedData);

                    // Validate the hex string
                    if (hexString.Length % 2 != 0)
                    {
                        throw new FormatException("Hexadecimal string must have an even length");
                    }

                    // Decode using an optimized approach
                    var decodedBytes = ConvertHexStringToByteArray(hexString);

                    _logger.Information(
                        "Hexadecimal decoding completed. Data decoded from {EncodedSize} to {DecodedSize} bytes.",
                        encodedData.Length, decodedBytes.Length);

                    return Result<byte[], SerializationError>.Success(decodedBytes);
                }
                catch (FormatException ex)
                {
                    _logger.Error(ex, "Invalid hexadecimal format during decoding: {Message}", ex.Message);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError($"Invalid hexadecimal format: {ex.Message}") { Operation = "Decode" },
                        ex);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during hexadecimal decoding: {Message}", ex.Message);
                    return Result<byte[], SerializationError>.Failure(
                        new SerializationError($"Hexadecimal decoding failed: {ex.Message}") { Operation = "Decode" },
                        ex);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during hexadecimal decoding task creation: {Message}", ex.Message);
            return Result<byte[], SerializationError>.Failure(
                new SerializationError($"Hexadecimal decoding task creation failed: {ex.Message}")
                {
                    Operation = "Decode"
                }, ex);
        }
    }

    /// <summary>
    ///     Gets information about the encoder.
    /// </summary>
    /// <returns>A dictionary containing encoder information.</returns>
    public IDictionary<string, object> GetEncoderInfo()
    {
        return new Dictionary<string, object>
        {
            ["EncodingType"] = "Hexadecimal", ["UpperCase"] = _upperCase, ["IsThreadSafe"] = true
        };
    }

    /// <summary>
    ///     Optimized conversion from hex string to byte array using Span.
    /// </summary>
    /// <param name="hexString">The hexadecimal string to convert.</param>
    /// <returns>A byte array containing the decoded data.</returns>
    /// <exception cref="FormatException">Thrown if the hex string is invalid.</exception>
    private static byte[] ConvertHexStringToByteArray(string hexString)
    {
        if (string.IsNullOrEmpty(hexString))
        {
            return Array.Empty<byte>();
        }

        if (hexString.Length % 2 != 0)
        {
            throw new FormatException("Hexadecimal string must have an even length");
        }

        var bytes = new byte[hexString.Length / 2];
        var hexSpan = hexString.AsSpan();

        for (var i = 0; i < bytes.Length; i++)
        {
            // Parse each hex byte directly without string allocation
            var highNibble = GetHexValue(hexSpan[i * 2]);
            var lowNibble = GetHexValue(hexSpan[(i * 2) + 1]);

            if (highNibble == -1 || lowNibble == -1)
            {
                throw new FormatException($"Invalid hexadecimal character in string at position {i * 2}");
            }

            bytes[i] = (byte)((highNibble << 4) | lowNibble);
        }

        return bytes;
    }

    /// <summary>
    ///     Gets the numeric value of a hex character.
    /// </summary>
    /// <param name="hex">The hex character to convert.</param>
    /// <returns>The numeric value (0-15) or -1 if invalid.</returns>
    private static int GetHexValue(char hex)
    {
        return hex switch
        {
            >= '0' and <= '9' => hex - '0',
            >= 'a' and <= 'f' => hex - 'a' + 10,
            >= 'A' and <= 'F' => hex - 'A' + 10,
            _ => -1
        };
    }
}
