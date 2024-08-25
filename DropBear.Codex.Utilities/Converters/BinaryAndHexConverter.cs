#region

using System.Globalization;
using System.Text;

#endregion

namespace DropBear.Codex.Utilities.Converters;

/// <summary>
///     Provides methods to convert between string, binary, and hexadecimal representations.
/// </summary>
public static class BinaryAndHexConverter
{
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

    /// <summary>
    ///     Converts a string to its binary representation.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The binary representation of the input string.</returns>
    public static string StringToBinary(string? value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : string.Concat(Encoding.UTF8.GetBytes(value).Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
    }

    /// <summary>
    ///     Converts a binary string to its original string representation.
    /// </summary>
    /// <param name="value">The binary string to convert.</param>
    /// <returns>The original string representation.</returns>
    /// <exception cref="ArgumentException">Thrown when the binary string is not valid.</exception>
    public static string BinaryToString(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length % 8 != 0)
        {
            throw new ArgumentException("Binary string is not valid.", nameof(value));
        }

        return Encoding.UTF8.GetString(Enumerable.Range(0, value.Length / 8)
            .Select(i => Convert.ToByte(value.Substring(i * 8, 8), 2))
            .ToArray());
    }

    /// <summary>
    ///     Converts a binary string to its hexadecimal representation using a lookup table.
    /// </summary>
    /// <param name="value">The binary string to convert.</param>
    /// <returns>The hexadecimal representation of the binary string.</returns>
    public static string BinaryToHex(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var hex = new StringBuilder(value.Length / 4);
        for (var i = 0; i < value.Length; i += 4)
        {
            hex.Append(BinaryToHexTable[value.Substring(i, 4)]);
        }

        return hex.ToString();
    }

    /// <summary>
    ///     Converts a hexadecimal string to its binary representation using a lookup table.
    /// </summary>
    /// <param name="value">The hexadecimal string to convert.</param>
    /// <returns>The binary representation of the hexadecimal string.</returns>
    public static string HexToBinary(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var binary = new StringBuilder(value.Length * 4);
        foreach (var c in value.ToUpperInvariant())
        {
            binary.Append(HexToBinaryTable[c.ToString()]);
        }

        return binary.ToString();
    }

    /// <summary>
    ///     Converts a string to a binary byte array.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A byte array representing the binary data of the string.</returns>
    public static byte[] StringToBinaryBytes(string? value)
    {
        return string.IsNullOrEmpty(value) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(value);
    }

    /// <summary>
    ///     Converts a binary byte array back to its string representation.
    /// </summary>
    /// <param name="value">The binary byte array to convert.</param>
    /// <returns>The string representation of the binary data.</returns>
    public static string BinaryBytesToString(byte[]? value)
    {
        return value is null || value.Length == 0 ? string.Empty : Encoding.UTF8.GetString(value);
    }

    /// <summary>
    ///     Converts a string to a hexadecimal byte array.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>A byte array representing the hexadecimal data of the string.</returns>
    public static byte[] StringToHexBytes(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<byte>();
        }

        var hex = BitConverter.ToString(Encoding.UTF8.GetBytes(value))
            .Replace("-", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();

        return Enumerable.Range(0, hex.Length / 2)
            .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
            .ToArray();
    }

    /// <summary>
    ///     Converts a hexadecimal byte array back to its string representation.
    /// </summary>
    /// <param name="value">The hexadecimal byte array to convert.</param>
    /// <returns>The string representation of the hexadecimal data.</returns>
    public static string HexBytesToString(byte[]? value)
    {
        if (value is null || value.Length == 0)
        {
            return string.Empty;
        }

        return BitConverter.ToString(value).Replace("-", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
    }

    /// <summary>
    ///     Converts a hexadecimal string to a byte array.
    /// </summary>
    /// <param name="hex">The hexadecimal string to convert.</param>
    /// <returns>A byte array representing the bytes of the hexadecimal string.</returns>
    /// <exception cref="ArgumentException">Thrown if the hex string is null, empty, or has an odd length.</exception>
    public static byte[] HexToByteArray(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            throw new ArgumentException("Hex string cannot be null or empty.", nameof(hex));
        }

        if (hex.Length % 2 != 0)
        {
            throw new ArgumentException("Hex string must have an even length.", nameof(hex));
        }

        return Enumerable.Range(0, hex.Length / 2)
            .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
            .ToArray();
    }

    /// <summary>
    ///     Converts a byte array to its hexadecimal string representation.
    /// </summary>
    /// <param name="bytes">The byte array to convert.</param>
    /// <returns>A string representing the hexadecimal representation of the byte array.</returns>
    /// <exception cref="ArgumentException">Thrown if the byte array is null or empty.</exception>
    public static string ByteArrayToHex(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            throw new ArgumentException("Byte array cannot be null or empty.", nameof(bytes));
        }

        return BitConverter.ToString(bytes).Replace("-", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
    }

    /// <summary>
    ///     Validates if the given string is a valid binary representation.
    /// </summary>
    /// <param name="value">The binary string to validate.</param>
    /// <returns>True if the string is a valid binary representation; otherwise, false.</returns>
    public static bool IsValidBinaryString(string? value)
    {
        return !string.IsNullOrEmpty(value) && value.All(c => c == '0' || c == '1');
    }

    /// <summary>
    ///     Converts a hexadecimal string to its original string representation.
    /// </summary>
    /// <param name="value">The hexadecimal string to convert.</param>
    /// <returns>The original string representation.</returns>
    public static string HexToString(string? value)
    {
        return BinaryToString(HexToBinary(value));
    }

    /// <summary>
    ///     Validates if the given string is a valid hexadecimal representation.
    /// </summary>
    /// <param name="value">The hexadecimal string to validate.</param>
    /// <returns>True if the string is a valid hexadecimal representation; otherwise, false.</returns>
    public static bool IsValidHexString(string? value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.All(c =>
                   "0123456789ABCDEFabcdef".Contains(c,
                       StringComparison.Ordinal));
    }

    /// <summary>
    ///     Converts a string to its hexadecimal representation.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The hexadecimal representation of the input string.</returns>
    public static string StringToHex(string? value)
    {
        return BinaryToHex(StringToBinary(value));
    }

    /// <summary>
    ///     Performs a bitwise AND operation on two binary strings.
    /// </summary>
    /// <param name="binary1">The first binary string.</param>
    /// <param name="binary2">The second binary string.</param>
    /// <returns>The result of the bitwise AND operation as a binary string.</returns>
    public static string BitwiseAnd(string? binary1, string? binary2)
    {
        return PerformBitwiseOperation(binary1, binary2, (b1, b2) => (b1 & b2).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     Performs a bitwise OR operation on two binary strings.
    /// </summary>
    /// <param name="binary1">The first binary string.</param>
    /// <param name="binary2">The second binary string.</param>
    /// <returns>The result of the bitwise OR operation as a binary string.</returns>
    public static string BitwiseOr(string? binary1, string? binary2)
    {
        return PerformBitwiseOperation(binary1, binary2, (b1, b2) => (b1 | b2).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     Performs a bitwise XOR operation on two binary strings.
    /// </summary>
    /// <param name="binary1">The first binary string.</param>
    /// <param name="binary2">The second binary string.</param>
    /// <returns>The result of the bitwise XOR operation as a binary string.</returns>
    public static string BitwiseXor(string? binary1, string? binary2)
    {
        return PerformBitwiseOperation(binary1, binary2, (b1, b2) => (b1 ^ b2).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     Performs a bitwise NOT operation on a binary string.
    /// </summary>
    /// <param name="binary">The binary string to negate.</param>
    /// <returns>The result of the bitwise NOT operation as a binary string.</returns>
    public static string BitwiseNot(string? binary)
    {
        return new string(binary?.Select(b => b == '0' ? '1' : '0').ToArray() ?? Array.Empty<char>());
    }

    /// <summary>
    ///     Shifts a binary string to the left by a specified number of bits.
    /// </summary>
    /// <param name="binary">The binary string to shift.</param>
    /// <param name="shift">The number of bits to shift.</param>
    /// <returns>The shifted binary string.</returns>
    public static string ShiftLeft(string? binary, int shift)
    {
        return binary is null
            ? string.Empty
            : binary.PadRight(binary.Length + shift, '0')[..binary.Length];
    }

    /// <summary>
    ///     Shifts a binary string to the right by a specified number of bits.
    /// </summary>
    /// <param name="binary">The binary string to shift.</param>
    /// <param name="shift">The number of bits to shift.</param>
    /// <returns>The shifted binary string.</returns>
    public static string ShiftRight(string? binary, int shift)
    {
        return binary is null
            ? string.Empty
            : binary.PadLeft(binary.Length + shift, '0')[shift..];
    }

    /// <summary>
    ///     Performs a specified bitwise operation on two binary strings.
    /// </summary>
    /// <param name="binary1">The first binary string.</param>
    /// <param name="binary2">The second binary string.</param>
    /// <param name="operation">The bitwise operation to perform, represented as a Func delegate.</param>
    /// <returns>The result of performing the bitwise operation on the binary strings.</returns>
    private static string PerformBitwiseOperation(string? binary1, string? binary2, Func<int, int, string> operation)
    {
        var maxLength = Math.Max(binary1?.Length ?? 0, binary2?.Length ?? 0);
        binary1 = binary1?.PadLeft(maxLength, '0') ?? new string('0', maxLength);
        binary2 = binary2?.PadLeft(maxLength, '0') ?? new string('0', maxLength);

        var result = new StringBuilder(maxLength);
        for (var i = 0; i < maxLength; i++)
        {
            var bit1 = binary1[i] - '0';
            var bit2 = binary2[i] - '0';
            result.Append(operation(bit1, bit2));
        }

        return result.ToString();
    }
}
