#region

using System.Numerics;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.Encoders;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Extensions;

/// <summary>
///     Provides extension methods for encoding and decoding strings and byte arrays using the rANS codec.
/// </summary>
public static class RANSCodecExtensions
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<RANSCodec>();

    /// <summary>
    ///     Encodes a string using the rANS codec.
    /// </summary>
    /// <param name="codec">The rANS codec instance.</param>
    /// <param name="input">The string to encode.</param>
    /// <returns>A <see cref="BigInteger" /> representing the encoded state.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="input" /> is null.</exception>
    public static BigInteger EncodeString(this RANSCodec codec, string input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        try
        {
            var (symbols, symbolCounts, encodeMap) = GetSymbolsAndCounts(input);
            codec.LastUsedSymbolCounts = symbolCounts;
            codec.LastUsedDecodeMap = encodeMap.ToDictionary(kvp => kvp.Value, kvp => (byte)kvp.Key);

            var result = codec.RansEncode(symbols, symbolCounts);
            if (result.IsSuccess)
            {
                return result.Value;
            }

            Logger.Error("Error during string encoding: {Error}", result.Error);

            throw new InvalidOperationException("Error during string encoding");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during string encoding: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Decodes a previously encoded state back to its original string form.
    /// </summary>
    /// <param name="codec">The rANS codec instance.</param>
    /// <param name="encodedState">The encoded state as a <see cref="BigInteger" />.</param>
    /// <param name="originalLength">The length of the original string.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if decoding is attempted before encoding.</exception>
    public static string DecodeToString(this RANSCodec codec, BigInteger encodedState, int originalLength)
    {
        if (codec.LastUsedSymbolCounts == null || codec.LastUsedDecodeMap == null)
        {
            throw new InvalidOperationException("Decoding attempted before encoding. Please encode a string first.");
        }

        try
        {
            var decodedSymbols = codec.RansDecode(encodedState, originalLength, codec.LastUsedSymbolCounts);

            if (decodedSymbols.IsSuccess)
            {
                return new string(decodedSymbols.Value.Select(s => (char)codec.LastUsedDecodeMap[s]).ToArray());
            }

            Logger.Error("Error during string decoding: {Error}", decodedSymbols.Error);
            throw new InvalidOperationException("Error during string decoding");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during string decoding: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Encodes a byte array using the rANS codec.
    /// </summary>
    /// <param name="codec">The rANS codec instance.</param>
    /// <param name="input">The byte array to encode.</param>
    /// <returns>A <see cref="BigInteger" /> representing the encoded state.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="input" /> is null.</exception>
    public static BigInteger EncodeByteArray(this RANSCodec codec, byte[] input)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        try
        {
            var (symbols, symbolCounts, encodeMap) = GetSymbolsAndCounts(input);
            codec.LastUsedSymbolCounts = symbolCounts;
            codec.LastUsedDecodeMap = encodeMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            var result = codec.RansEncode(symbols, symbolCounts);
            if (result.IsSuccess)
            {
                return result.Value;
            }

            Logger.Error("Error during byte array encoding: {Error}", result.Error);

            throw new InvalidOperationException("Error during byte array encoding");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during byte array encoding: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Decodes a previously encoded state back to its original byte array form.
    /// </summary>
    /// <param name="codec">The rANS codec instance.</param>
    /// <param name="encodedState">The encoded state as a <see cref="BigInteger" />.</param>
    /// <param name="originalLength">The length of the original byte array.</param>
    /// <returns>The decoded byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown if decoding is attempted before encoding.</exception>
    public static byte[] DecodeToByteArray(this RANSCodec codec, BigInteger encodedState, int originalLength)
    {
        if (codec.LastUsedSymbolCounts == null || codec.LastUsedDecodeMap == null)
        {
            throw new InvalidOperationException(
                "Decoding attempted before encoding. Please encode a byte array first.");
        }

        try
        {
            var decodedSymbols = codec.RansDecode(encodedState, originalLength, codec.LastUsedSymbolCounts);

            if (decodedSymbols.IsSuccess)
            {
                return decodedSymbols.Value.Select(s => codec.LastUsedDecodeMap[s]).ToArray();
            }

            Logger.Error("Error during byte array decoding: {Error}", decodedSymbols.Error);
            throw new InvalidOperationException("Error during byte array decoding");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during byte array decoding: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Extracts symbols, their counts, and an encoding map from the input data.
    /// </summary>
    /// <typeparam name="T">The type of elements in the input sequence.</typeparam>
    /// <param name="input">The input sequence of elements.</param>
    /// <returns>
    ///     A tuple containing the list of symbols, an array of their counts, and a dictionary mapping elements to their symbol
    ///     indices.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="input" /> is null.</exception>
    private static (List<int> symbols, int[] symbolCounts, Dictionary<T, int> encodeMap)
        GetSymbolsAndCounts<T>(IEnumerable<T> input) where T : notnull
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var encodeMap = new Dictionary<T, int>();
        var symbolCounts = new Dictionary<int, int>();
        var symbols = new List<int>();

        try
        {
            foreach (var item in input)
            {
                if (!encodeMap.TryGetValue(item, out var symbol))
                {
                    symbol = encodeMap.Count;
                    encodeMap[item] = symbol;
                }

                symbols.Add(symbol);
                symbolCounts[symbol] = symbolCounts.TryGetValue(symbol, out var count) ? count + 1 : 1;
            }

            var maxSymbol = encodeMap.Values.Max();
            var symbolCountArray = new int[maxSymbol + 1];
            foreach (var kvp in symbolCounts)
            {
                symbolCountArray[kvp.Key] = kvp.Value;
            }

            return (symbols, symbolCountArray, encodeMap);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during symbol extraction: {ex.Message}");
            throw;
        }
    }
}
