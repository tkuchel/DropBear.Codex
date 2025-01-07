namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides helper methods for printing and comparing byte arrays.
/// </summary>
public static class ArrayHelper
{
    /// <summary>
    ///     Prints the contents of a byte array in a formatted, human-readable form.
    /// </summary>
    /// <param name="bytes">The byte array to print.</param>
    /// <param name="output">The output stream to write to. Defaults to Console.Out.</param>
    public static void PrintByteArray(byte[] bytes, TextWriter? output = null)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        const int bytesPerLine = 16;
        output ??= Console.Out;

        for (var i = 0; i < bytes.Length; i += bytesPerLine)
        {
            output.Write($"{i:X8}: ");

            // Print hexadecimal values
            for (var j = 0; j < bytesPerLine; j++)
            {
                output.Write(i + j < bytes.Length ? $"{bytes[i + j]:X2} " : "   ");
            }

            output.Write(" ");

            // Print ASCII characters
            for (var j = 0; j < bytesPerLine; j++)
            {
                if (i + j < bytes.Length)
                {
                    var b = bytes[i + j];
                    var c = b < 32 || b > 126 ? '.' : (char)b;
                    output.Write(c);
                }
                else
                {
                    output.Write(" ");
                }
            }

            output.WriteLine();
        }
    }

    /// <summary>
    ///     Compares two byte arrays and prints the differences in a side-by-side hexadecimal format.
    /// </summary>
    /// <param name="array1">The first byte array.</param>
    /// <param name="array2">The second byte array.</param>
    /// <param name="output">The output stream to write to. Defaults to Console.Out.</param>
    public static void CompareBytesArrays(byte[] array1, byte[] array2, TextWriter? output = null)
    {
        if (array1 == null)
        {
            throw new ArgumentNullException(nameof(array1));
        }

        if (array2 == null)
        {
            throw new ArgumentNullException(nameof(array2));
        }

        output ??= Console.Out;
        output.WriteLine("Comparing byte arrays...");

        if (array1.Length != array2.Length)
        {
            output.WriteLine($"The arrays have different lengths: {array1.Length} and {array2.Length}.");
        }

        var diffCount = 0;
        const int bytesPerLine = 16;
        var maxLength = Math.Max(array1.Length, array2.Length);

        for (var i = 0; i < maxLength; i += bytesPerLine)
        {
            var bytesToPrint = Math.Min(bytesPerLine, maxLength - i);

            output.Write($"{i:X8}: ");

            // Print bytes for array1
            for (var j = 0; j < bytesToPrint; j++)
            {
                var index = i + j;
                output.Write(index < array1.Length ? $"{array1[index]:X2} " : "   ");
            }

            output.Write(" ");

            // Print bytes for array2
            for (var j = 0; j < bytesToPrint; j++)
            {
                var index = i + j;
                if (index < array2.Length)
                {
                    output.Write($"{array2[index]:X2} ");
                    if (index < array1.Length && array1[index] != array2[index])
                    {
                        output.Write("* ");
                        diffCount++;
                    }
                    else
                    {
                        output.Write("  ");
                    }
                }
                else
                {
                    output.Write("   ");
                }
            }

            output.WriteLine();
        }

        output.WriteLine($"Total differences: {diffCount}");
    }
}
