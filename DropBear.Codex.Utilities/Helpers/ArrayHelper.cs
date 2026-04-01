#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;
using DropBear.Codex.Utilities.Extensions;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides helper methods for printing and comparing byte arrays, optimized for .NET 8 features.
/// </summary>
public static class ArrayHelper
{
    /// <summary>
    ///     Prints the contents of a byte array in a formatted, human-readable form.
    ///     Uses <see cref="Span{T}" /> for optimized processing.
    /// </summary>
    public static Result<Unit, ByteArrayError> PrintByteArray(ReadOnlySpan<byte> bytes, TextWriter? output = null)
    {
        if (bytes.IsEmpty)
        {
            return Result<Unit, ByteArrayError>.Failure(new ByteArrayError("Byte array cannot be empty."));
        }

        const int bytesPerLine = 16;
        output ??= Console.Out;

        try
        {
            for (var i = 0; i < bytes.Length; i += bytesPerLine)
            {
                var lineBytes = bytes.Slice(i, Math.Min(bytesPerLine, bytes.Length - i));
                output.Write($"{i:X8}: ");

                // Print hexadecimal values
                foreach (var b in lineBytes)
                {
                    output.Write($"{b:X2} ");
                }

                output.Write(" ");

                // Print ASCII characters
                foreach (var b in lineBytes)
                {
                    output.Write(b < 32 || b > 126 ? '.' : (char)b);
                }

                output.WriteLine();
            }

            return Result<Unit, ByteArrayError>.Success(Unit.Value);
        }
        catch (IOException ex)
        {
            return Result<Unit, ByteArrayError>.Failure(new ByteArrayError("Failed to print byte array.", ex));
        }
        catch (ObjectDisposedException ex)
        {
            return Result<Unit, ByteArrayError>.Failure(new ByteArrayError("Failed to print byte array.", ex));
        }
    }

    /// <summary>
    ///     Compares two byte arrays and prints the differences in a side-by-side hexadecimal format.
    /// </summary>
    public static Result<Unit, ByteArrayError> CompareByteArrays(ReadOnlySpan<byte> array1, ReadOnlySpan<byte> array2,
        TextWriter? output = null)
    {
        if (array1.IsEmpty || array2.IsEmpty)
        {
            return Result<Unit, ByteArrayError>.Failure(new ByteArrayError("Both byte arrays must be provided."));
        }

        output ??= Console.Out;
        const int bytesPerLine = 16;

        try
        {
            output.WriteLine("Comparing byte arrays...");

            if (array1.Length != array2.Length)
            {
                output.WriteLine($"The arrays have different lengths: {array1.Length} and {array2.Length}.");
            }

            var diffCount = 0;
            var maxLength = Math.Max(array1.Length, array2.Length);

            for (var i = 0; i < maxLength; i += bytesPerLine)
            {
                var chunk1Length = i < array1.Length ? Math.Min(bytesPerLine, array1.Length - i) : 0;
                var chunk2Length = i < array2.Length ? Math.Min(bytesPerLine, array2.Length - i) : 0;
                var chunk1 = array1.Slice(i, chunk1Length);
                var chunk2 = array2.Slice(i, chunk2Length);

                output.Write($"{i:X8}: ");

                // Print bytes for array1
                foreach (var b in chunk1)
                {
                    output.Write($"{b:X2} ");
                }

                output.Write(" ");

                // Print bytes for array2 and differences
                var sharedLength = Math.Min(chunk1.Length, chunk2.Length);
                for (var j = 0; j < sharedLength; j++)
                {
                    var b1 = chunk1[j];
                    var b2 = chunk2[j];
                    output.Write($"{b2:X2} {(b1 != b2 ? "* " : "  ")}");
                    if (b1 != b2)
                    {
                        diffCount++;
                    }
                }

                for (var j = sharedLength; j < chunk2.Length; j++)
                {
                    output.Write($"{chunk2[j]:X2} * ");
                }

                diffCount += Math.Abs(chunk1.Length - chunk2.Length);
                output.WriteLine();
            }

            output.WriteLine($"Total differences: {diffCount}");
            return Result<Unit, ByteArrayError>.Success(Unit.Value);
        }
        catch (IOException ex)
        {
            return Result<Unit, ByteArrayError>.Failure(new ByteArrayError("Failed to compare byte arrays.", ex));
        }
        catch (ObjectDisposedException ex)
        {
            return Result<Unit, ByteArrayError>.Failure(new ByteArrayError("Failed to compare byte arrays.", ex));
        }
    }
}
