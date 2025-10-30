#region

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Converters;

/// <summary>
///     Contains error details for binary and hex conversion operations.
/// </summary>
public sealed record BinaryConversionError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BinaryConversionError" /> record.
    /// </summary>
    /// <param name="message">The error message.</param>
    public BinaryConversionError(string message) : base(message) { }
}

/// <summary>
///     Provides methods to convert between string, binary, and hexadecimal representations
///     with optimized performance and memory usage.
/// </summary>
public static class BinaryAndHexConverter
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext(typeof(BinaryAndHexConverter));

    // Lookup tables for binary-hex conversions to avoid repeated calculations
    private static readonly Dictionary<string, string> BinaryToHexTable = new(StringComparer.Ordinal)
    {
        { "0000", "0" },
        { "0001", "1" },
        { "0010", "2" },
        { "0011", "3" },
        { "0100", "4" },
        { "0101", "5" },
        { "0110", "6" },
        { "0111", "7" },
        { "1000", "8" },
        { "1001", "9" },
        { "1010", "A" },
        { "1011", "B" },
        { "1100", "C" },
        { "1101", "D" },
        { "1110", "E" },
        { "1111", "F" }
    };

    private static readonly Dictionary<string, string> HexToBinaryTable = new(StringComparer.Ordinal)
    {
        { "0", "0000" },
        { "1", "0001" },
        { "2", "0010" },
        { "3", "0011" },
        { "4", "0100" },
        { "5", "0101" },
        { "6", "0110" },
        { "7", "0111" },
        { "8", "1000" },
        { "9", "1001" },
        { "A", "1010" },
        { "B", "1011" },
        { "C", "1100" },
        { "D", "1101" },
        { "E", "1110" },
        { "F", "1111" }
    };

    // Table for valid hex characters for quick lookup
    private static readonly HashSet<char> ValidHexChars = new("0123456789ABCDEFabcdef");

    /// <summary>
    ///     Converts a string to its binary representation.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A Result containing the binary representation of the input string or an error.</returns>
    public static Result<string, BinaryConversionError> StringToBinary(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        try
        {
            // Pre-calculate output size
            var charCount = value.Length;
            var outputSize = charCount * 8;

            // For small strings, use stack allocation; for larger ones use pooled array
            char[]? rentedArray = null;
            var result = outputSize <= 1024
                ? stackalloc char[outputSize]
                : (rentedArray = ArrayPool<char>.Shared.Rent(outputSize)).AsSpan(0, outputSize);

            try
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                var position = 0;

                foreach (var b in bytes)
                {
                    // Convert each byte to 8 bits
                    for (var bit = 7; bit >= 0; bit--)
                    {
                        result[position++] = ((b >> bit) & 1) == 1 ? '1' : '0';
                    }
                }

                return Result<string, BinaryConversionError>.Success(new string(result.Slice(0, position)));
            }
            finally
            {
                // Return the rented array to the pool if we used one
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting string to binary");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert string to binary: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a binary string to its original string representation.
    /// </summary>
    /// <param name="value">The binary string to convert.</param>
    /// <returns>A Result containing the original string representation or an error.</returns>
    public static Result<string, BinaryConversionError> BinaryToString(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        if (value.Length % 8 != 0)
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Binary string is not valid. Length must be a multiple of 8."));
        }

        try
        {
            // Calculate output size (1 byte per 8 binary digits)
            var byteCount = value.Length / 8;
            var bytes = new byte[byteCount];

            // Convert each 8-bit sequence to a byte
            for (var i = 0; i < byteCount; i++)
            {
                var byteChars = value.Slice(i * 8, 8);

                // Validate binary chars
                for (var j = 0; j < 8; j++)
                {
                    if (byteChars[j] != '0' && byteChars[j] != '1')
                    {
                        return Result<string, BinaryConversionError>.Failure(
                            new BinaryConversionError("Invalid binary character encountered."));
                    }
                }

                // Parse the 8-bit sequence
                bytes[i] = (byte)Convert.ToInt32(new string(byteChars), 2);
            }

            // Convert bytes back to string
            var result = Encoding.UTF8.GetString(bytes);
            return Result<string, BinaryConversionError>.Success(result);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting binary to string");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert binary to string: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a binary string to its hexadecimal representation using a lookup table.
    /// </summary>
    /// <param name="value">The binary string to convert.</param>
    /// <returns>A Result containing the hexadecimal representation of the binary string or an error.</returns>
    public static Result<string, BinaryConversionError> BinaryToHex(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        // Binary string length must be a multiple of 4 for hex conversion
        if (value.Length % 4 != 0)
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Binary string length must be a multiple of 4 for hex conversion."));
        }

        try
        {
            // Calculate output size (1 hex char per 4 binary digits)
            var hexLength = value.Length / 4;

            // Use stack allocation for small outputs, pooled array for larger ones
            char[]? rentedArray = null;
            var result = hexLength <= 1024
                ? stackalloc char[hexLength]
                : (rentedArray = ArrayPool<char>.Shared.Rent(hexLength)).AsSpan(0, hexLength);

            try
            {
                // Process 4 binary digits at a time
                for (var i = 0; i < value.Length; i += 4)
                {
                    var chunk = value.Slice(i, 4);

                    // Validate chunk contains only 0 and 1
                    for (var j = 0; j < 4; j++)
                    {
                        if (chunk[j] != '0' && chunk[j] != '1')
                        {
                            return Result<string, BinaryConversionError>.Failure(
                                new BinaryConversionError("Invalid binary character encountered."));
                        }
                    }

                    // Get the hex value from the lookup table
                    var hexValue = BinaryToHexTable[new string(chunk)];
                    result[i / 4] = hexValue[0];
                }

                return Result<string, BinaryConversionError>.Success(new string(result));
            }
            finally
            {
                // Return the rented array to the pool if we used one
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting binary to hex");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert binary to hex: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a hexadecimal string to its binary representation using a lookup table.
    /// </summary>
    /// <param name="value">The hexadecimal string to convert.</param>
    /// <returns>A Result containing the binary representation of the hexadecimal string or an error.</returns>
    public static Result<string, BinaryConversionError> HexToBinary(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        try
        {
            // Calculate output size (4 binary digits per hex char)
            var binaryLength = value.Length * 4;

            // Use stack allocation for small outputs, pooled array for larger ones
            char[]? rentedArray = null;
            var result = binaryLength <= 1024
                ? stackalloc char[binaryLength]
                : (rentedArray = ArrayPool<char>.Shared.Rent(binaryLength)).AsSpan(0, binaryLength);

            try
            {
                // Process each hex character
                for (var i = 0; i < value.Length; i++)
                {
                    var hexChar = char.ToUpperInvariant(value[i]);

                    // Validate hex char
                    if (!ValidHexChars.Contains(hexChar))
                    {
                        return Result<string, BinaryConversionError>.Failure(
                            new BinaryConversionError($"Invalid hexadecimal character: '{hexChar}'"));
                    }

                    // Get the binary value from the lookup table
                    var binaryValue = HexToBinaryTable[hexChar.ToString()];
                    binaryValue.AsSpan().CopyTo(result.Slice(i * 4, 4));
                }

                return Result<string, BinaryConversionError>.Success(new string(result));
            }
            finally
            {
                // Return the rented array to the pool if we used one
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting hex to binary");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert hex to binary: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a string to a binary byte array.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A Result containing a byte array representing the binary data of the string or an error.</returns>
    public static Result<byte[], BinaryConversionError> StringToBinaryBytes(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<byte[], BinaryConversionError>.Success([]);
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return Result<byte[], BinaryConversionError>.Success(bytes);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting string to binary bytes");
            return Result<byte[], BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert string to binary bytes: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a binary byte array back to its string representation.
    /// </summary>
    /// <param name="value">The binary byte array to convert.</param>
    /// <returns>A Result containing the string representation of the binary data or an error.</returns>
    public static Result<string, BinaryConversionError> BinaryBytesToString(byte[]? value)
    {
        if (value == null || value.Length == 0)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        try
        {
            var result = Encoding.UTF8.GetString(value);
            return Result<string, BinaryConversionError>.Success(result);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting binary bytes to string");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert binary bytes to string: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a string to a hexadecimal byte array.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A Result containing a byte array representing the hexadecimal data of the string or an error.</returns>
    public static Result<byte[], BinaryConversionError> StringToHexBytes(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<byte[], BinaryConversionError>.Success([]);
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var hex = BitConverter.ToString(bytes).Replace("-", "", StringComparison.OrdinalIgnoreCase);

            // Convert hex string to byte array (2 hex chars per byte)
            var result = new byte[hex.Length / 2];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber);
            }

            return Result<byte[], BinaryConversionError>.Success(result);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting string to hex bytes");
            return Result<byte[], BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert string to hex bytes: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a hexadecimal byte array back to its string representation.
    /// </summary>
    /// <param name="value">The hexadecimal byte array to convert.</param>
    /// <returns>A Result containing the string representation of the hexadecimal data or an error.</returns>
    public static Result<string, BinaryConversionError> HexBytesToString(byte[]? value)
    {
        if (value == null || value.Length == 0)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        try
        {
            var hex = BitConverter.ToString(value).Replace("-", "", StringComparison.OrdinalIgnoreCase);
            return Result<string, BinaryConversionError>.Success(hex);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting hex bytes to string");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert hex bytes to string: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a hexadecimal string to a byte array.
    /// </summary>
    /// <param name="hex">The hexadecimal string to convert.</param>
    /// <returns>A Result containing a byte array representing the bytes of the hexadecimal string or an error.</returns>
    public static Result<byte[], BinaryConversionError> HexToByteArray(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return Result<byte[], BinaryConversionError>.Failure(
                new BinaryConversionError("Hex string cannot be null or empty."));
        }

        if (hex.Length % 2 != 0)
        {
            return Result<byte[], BinaryConversionError>.Failure(
                new BinaryConversionError("Hex string must have an even length."));
        }

        try
        {
            // Use a pooled array for better memory efficiency
            var byteLength = hex.Length / 2;
            var result = new byte[byteLength];

            for (var i = 0; i < byteLength; i++)
            {
                var hexChars = hex.AsSpan(i * 2, 2);

                // Validate hex chars
                for (var j = 0; j < 2; j++)
                {
                    if (!ValidHexChars.Contains(hexChars[j]))
                    {
                        return Result<byte[], BinaryConversionError>.Failure(
                            new BinaryConversionError($"Invalid hexadecimal character: '{hexChars[j]}'"));
                    }
                }

                result[i] = byte.Parse(hexChars, NumberStyles.HexNumber);
            }

            return Result<byte[], BinaryConversionError>.Success(result);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting hex string to byte array");
            return Result<byte[], BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert hex string to byte array: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Converts a byte array to its hexadecimal string representation.
    /// </summary>
    /// <param name="bytes">The byte array to convert.</param>
    /// <returns>A Result containing a string representing the hexadecimal representation of the byte array or an error.</returns>
    public static Result<string, BinaryConversionError> ByteArrayToHex(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Byte array cannot be null or empty."));
        }

        try
        {
            // Pre-allocate StringBuilder for better performance
            var sb = new StringBuilder(bytes.Length * 2);

            foreach (var b in bytes)
            {
                sb.Append(b.ToString("X2"));
            }

            return Result<string, BinaryConversionError>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error converting byte array to hex string");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to convert byte array to hex string: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Validates if the given string is a valid binary representation.
    /// </summary>
    /// <param name="value">The binary string to validate.</param>
    /// <returns>True if the string is a valid binary representation; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidBinaryString(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '0' && value[i] != '1')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Converts a hexadecimal string to its original string representation.
    /// </summary>
    /// <param name="value">The hexadecimal string to convert.</param>
    /// <returns>A Result containing the original string representation or an error.</returns>
    public static Result<string, BinaryConversionError> HexToString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        // First convert hex to binary
        var binaryResult = HexToBinary(value);
        if (!binaryResult.IsSuccess)
        {
            return Result<string, BinaryConversionError>.Failure(binaryResult.Error!);
        }

        // Then convert binary to string
        return BinaryToString(binaryResult.Value!);
    }

    /// <summary>
    ///     Validates if the given string is a valid hexadecimal representation.
    /// </summary>
    /// <param name="value">The hexadecimal string to validate.</param>
    /// <returns>True if the string is a valid hexadecimal representation; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidHexString(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (!ValidHexChars.Contains(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Converts a string to its hexadecimal representation.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A Result containing the hexadecimal representation of the input string or an error.</returns>
    public static Result<string, BinaryConversionError> StringToHex(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        // First convert string to binary
        var binaryResult = StringToBinary(value);
        if (!binaryResult.IsSuccess)
        {
            return Result<string, BinaryConversionError>.Failure(binaryResult.Error!);
        }

        // Then convert binary to hex
        return BinaryToHex(binaryResult.Value!);
    }

    /// <summary>
    ///     Performs a bitwise AND operation on two binary strings.
    /// </summary>
    /// <param name="binary1">The first binary string.</param>
    /// <param name="binary2">The second binary string.</param>
    /// <returns>A Result containing the result of the bitwise AND operation as a binary string or an error.</returns>
    public static Result<string, BinaryConversionError> BitwiseAnd(ReadOnlySpan<char> binary1,
        ReadOnlySpan<char> binary2)
    {
        return PerformBitwiseOperation(binary1, binary2, (b1, b2) => b1 && b2);
    }

    /// <summary>
    ///     Performs a bitwise OR operation on two binary strings.
    /// </summary>
    /// <param name="binary1">The first binary string.</param>
    /// <param name="binary2">The second binary string.</param>
    /// <returns>A Result containing the result of the bitwise OR operation as a binary string or an error.</returns>
    public static Result<string, BinaryConversionError> BitwiseOr(ReadOnlySpan<char> binary1,
        ReadOnlySpan<char> binary2)
    {
        return PerformBitwiseOperation(binary1, binary2, (b1, b2) => b1 || b2);
    }

    /// <summary>
    ///     Performs a bitwise XOR operation on two binary strings.
    /// </summary>
    /// <param name="binary1">The first binary string.</param>
    /// <param name="binary2">The second binary string.</param>
    /// <returns>A Result containing the result of the bitwise XOR operation as a binary string or an error.</returns>
    public static Result<string, BinaryConversionError> BitwiseXor(ReadOnlySpan<char> binary1,
        ReadOnlySpan<char> binary2)
    {
        return PerformBitwiseOperation(binary1, binary2, (b1, b2) => b1 != b2);
    }

    /// <summary>
    ///     Performs a bitwise NOT operation on a binary string.
    /// </summary>
    /// <param name="binary">The binary string to negate.</param>
    /// <returns>A Result containing the result of the bitwise NOT operation as a binary string or an error.</returns>
    public static Result<string, BinaryConversionError> BitwiseNot(ReadOnlySpan<char> binary)
    {
        if (binary.IsEmpty)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        // Validate the binary string
        if (!IsValidBinaryString(binary))
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Invalid binary string."));
        }

        try
        {
            char[]? rentedArray = null;
            var result = binary.Length <= 1024
                ? stackalloc char[binary.Length]
                : (rentedArray = ArrayPool<char>.Shared.Rent(binary.Length)).AsSpan(0, binary.Length);

            try
            {
                for (var i = 0; i < binary.Length; i++)
                {
                    result[i] = binary[i] == '0' ? '1' : '0';
                }

                return Result<string, BinaryConversionError>.Success(new string(result));
            }
            finally
            {
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing bitwise NOT operation");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to perform bitwise NOT: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Shifts a binary string to the left by a specified number of bits.
    /// </summary>
    /// <param name="binary">The binary string to shift.</param>
    /// <param name="shift">The number of bits to shift.</param>
    /// <returns>A Result containing the shifted binary string or an error.</returns>
    public static Result<string, BinaryConversionError> ShiftLeft(ReadOnlySpan<char> binary, int shift)
    {
        if (binary.IsEmpty)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        if (shift < 0)
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Shift amount cannot be negative."));
        }

        // Validate the binary string
        if (!IsValidBinaryString(binary))
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Invalid binary string."));
        }

        if (shift == 0)
        {
            return Result<string, BinaryConversionError>.Success(new string(binary));
        }

        try
        {
            // Result will be same length, padded with zeros from the right
            char[]? rentedArray = null;
            var result = binary.Length <= 1024
                ? stackalloc char[binary.Length]
                : (rentedArray = ArrayPool<char>.Shared.Rent(binary.Length)).AsSpan(0, binary.Length);

            try
            {
                // Copy binary content shifted left
                if (shift < binary.Length)
                {
                    binary.Slice(shift).CopyTo(result);
                }

                // Fill the remaining positions with zeros
                result.Slice(binary.Length - shift).Fill('0');

                return Result<string, BinaryConversionError>.Success(new string(result));
            }
            finally
            {
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing shift left operation");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to perform shift left: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Shifts a binary string to the right by a specified number of bits.
    /// </summary>
    /// <param name="binary">The binary string to shift.</param>
    /// <param name="shift">The number of bits to shift.</param>
    /// <returns>A Result containing the shifted binary string or an error.</returns>
    public static Result<string, BinaryConversionError> ShiftRight(ReadOnlySpan<char> binary, int shift)
    {
        if (binary.IsEmpty)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        if (shift < 0)
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Shift amount cannot be negative."));
        }

        // Validate the binary string
        if (!IsValidBinaryString(binary))
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Invalid binary string."));
        }

        if (shift == 0)
        {
            return Result<string, BinaryConversionError>.Success(new string(binary));
        }

        try
        {
            // Result will be same length, padded with zeros from the left
            char[]? rentedArray = null;
            var result = binary.Length <= 1024
                ? stackalloc char[binary.Length]
                : (rentedArray = ArrayPool<char>.Shared.Rent(binary.Length)).AsSpan(0, binary.Length);

            try
            {
                // Fill the left part with zeros
                result.Slice(0, Math.Min(shift, binary.Length)).Fill('0');

                // Copy binary content shifted right if there's room
                if (shift < binary.Length)
                {
                    binary.Slice(0, binary.Length - shift).CopyTo(result.Slice(shift));
                }

                return Result<string, BinaryConversionError>.Success(new string(result));
            }
            finally
            {
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing shift right operation");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to perform shift right: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Performs a specified bitwise operation on two binary strings.
    /// </summary>
    /// <param name="binary1">The first binary string.</param>
    /// <param name="binary2">The second binary string.</param>
    /// <param name="operation">The bitwise operation to perform (function taking two boolean values).</param>
    /// <returns>A Result containing the result of performing the bitwise operation or an error.</returns>
    private static Result<string, BinaryConversionError> PerformBitwiseOperation(
        ReadOnlySpan<char> binary1,
        ReadOnlySpan<char> binary2,
        Func<bool, bool, bool> operation)
    {
        if (binary1.IsEmpty && binary2.IsEmpty)
        {
            return Result<string, BinaryConversionError>.Success(string.Empty);
        }

        // Validate the binary strings
        if (!IsValidBinaryString(binary1) || !IsValidBinaryString(binary2))
        {
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError("Invalid binary string."));
        }

        try
        {
            var maxLength = Math.Max(binary1.Length, binary2.Length);

            char[]? rentedArray = null;
            var result = maxLength <= 1024
                ? stackalloc char[maxLength]
                : (rentedArray = ArrayPool<char>.Shared.Rent(maxLength)).AsSpan(0, maxLength);

            try
            {
                for (var i = 0; i < maxLength; i++)
                {
                    var bit1 = i < binary1.Length && binary1[i] == '1';
                    var bit2 = i < binary2.Length && binary2[i] == '1';

                    result[i] = operation(bit1, bit2) ? '1' : '0';
                }

                return Result<string, BinaryConversionError>.Success(new string(result));
            }
            finally
            {
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing bitwise operation");
            return Result<string, BinaryConversionError>.Failure(
                new BinaryConversionError($"Failed to perform bitwise operation: {ex.Message}"), ex);
        }
    }
}
