#region

using System.Numerics;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Encoders;

/// <summary>
///     A class that provides methods to encode and decode sequences of symbols using the rANS (Range Asymmetric Numeral
///     Systems) codec.
/// </summary>
/// <remarks>
///     rANS (range Asymmetric Numeral Systems) is an entropy coding technique that combines the compression efficiency
///     of arithmetic coding with the execution speed of Huffman coding.
/// </remarks>
public sealed class RANSCodec
{
    private readonly Dictionary<int, BigInteger[]> _cumulativeCountsCache = new();
    private readonly ILogger _logger;
    private readonly Dictionary<int, int[]> _symbolCountsCache = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RANSCodec" /> class.
    /// </summary>
    public RANSCodec()
    {
        _logger = LoggerFactory.Logger.ForContext<RANSCodec>();
    }

    /// <summary>
    ///     Gets or sets the last used symbol counts during the encoding process.
    /// </summary>
    public int[]? LastUsedSymbolCounts { get; set; }

    /// <summary>
    ///     Gets or sets the last used decode map during the encoding process.
    /// </summary>
    public IDictionary<int, byte>? LastUsedDecodeMap { get; set; }

    /// <summary>
    ///     Encodes a sequence of symbols into a compressed state using the rANS codec.
    /// </summary>
    /// <param name="symbols">The sequence of symbols to encode.</param>
    /// <param name="symbolCounts">The frequency of each symbol in the sequence.</param>
    /// <returns>A Result containing the encoded BigInteger state or an error if encoding fails.</returns>
    public Result<BigInteger, RANSCodecError> RansEncode(IList<int> symbols, int[] symbolCounts)
    {
        if (symbols == null || !symbols.Any())
        {
            return Result<BigInteger, RANSCodecError>.Failure(
                new RANSCodecError("Input symbols cannot be null or empty."));
        }

        if (symbolCounts == null || symbolCounts.Length == 0)
        {
            return Result<BigInteger, RANSCodecError>.Failure(
                new RANSCodecError("Symbol counts cannot be null or empty."));
        }

        if (symbolCounts.Any(count => count <= 0))
        {
            return Result<BigInteger, RANSCodecError>.Failure(
                new RANSCodecError("Symbol counts must be positive integers."));
        }

        try
        {
            // Cache total counts and cumulative counts for performance
            var totalCounts = CalculateTotalCounts(symbolCounts);
            var cumulCounts = GetCumulativeCounts(symbolCounts);

            BigInteger state = 1;

            foreach (var symbol in symbols)
            {
                if (symbol < 0 || symbol >= symbolCounts.Length)
                {
                    return Result<BigInteger, RANSCodecError>.Failure(
                        new RANSCodecError($"Symbol {symbol} is out of range for the given symbol counts."));
                }

                BigInteger sCount = symbolCounts[symbol];
                state = (state / sCount * totalCounts) + cumulCounts[symbol] + (state % sCount);
            }

            // Cache the symbol counts for reuse
            LastUsedSymbolCounts = symbolCounts;

            _logger.Debug("Successfully encoded {SymbolCount} symbols", symbols.Count);
            return Result<BigInteger, RANSCodecError>.Success(state);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during encoding");
            return Result<BigInteger, RANSCodecError>.Failure(
                new RANSCodecError($"Error during encoding: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Decodes an encoded state back to the original sequence of symbols using the rANS codec.
    /// </summary>
    /// <param name="state">The encoded state as a <see cref="BigInteger" />.</param>
    /// <param name="numSymbols">The number of symbols in the original sequence.</param>
    /// <param name="symbolCounts">The frequency of each symbol in the sequence.</param>
    /// <returns>A Result containing the decoded sequence of symbols or an error if decoding fails.</returns>
    public Result<IList<int>, RANSCodecError> RansDecode(BigInteger state, int numSymbols, int[] symbolCounts)
    {
        if (state <= 0)
        {
            return Result<IList<int>, RANSCodecError>.Failure(
                new RANSCodecError("Encoded state must be a positive number."));
        }

        if (numSymbols <= 0)
        {
            return Result<IList<int>, RANSCodecError>.Failure(
                new RANSCodecError("Number of symbols must be positive."));
        }

        if (symbolCounts == null || symbolCounts.Length == 0)
        {
            return Result<IList<int>, RANSCodecError>.Failure(
                new RANSCodecError("Symbol counts cannot be null or empty."));
        }

        if (symbolCounts.Any(count => count <= 0))
        {
            return Result<IList<int>, RANSCodecError>.Failure(
                new RANSCodecError("Symbol counts must be positive integers."));
        }

        try
        {
            // Cache total counts and cumulative counts for performance
            var totalCounts = CalculateTotalCounts(symbolCounts);
            var cumulCounts = GetCumulativeCounts(symbolCounts);

            // Pre-allocate the result list for better performance
            var decodedSymbols = new List<int>(numSymbols);

            for (var i = 0; i < numSymbols; i++)
            {
                var slot = state % totalCounts;
                var symbol = CumulativeInverse(slot, cumulCounts);

                decodedSymbols.Add(symbol);

                BigInteger sCount = symbolCounts[symbol];
                state = (state / totalCounts * sCount) + slot - cumulCounts[symbol];

                if (state < 0)
                {
                    _logger.Error("State became negative during decoding at position {Position}", i);
                    return Result<IList<int>, RANSCodecError>.Failure(
                        new RANSCodecError("State became negative during decoding, indicating an error."));
                }
            }

            // Reverse the symbols to get the original order
            decodedSymbols.Reverse();

            _logger.Debug("Successfully decoded {SymbolCount} symbols", decodedSymbols.Count);
            return Result<IList<int>, RANSCodecError>.Success(decodedSymbols);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during decoding");
            return Result<IList<int>, RANSCodecError>.Failure(
                new RANSCodecError($"Error during decoding: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Calculates the total counts for all symbols.
    /// </summary>
    /// <param name="symbolCounts">The frequency of each symbol in the sequence.</param>
    /// <returns>The sum of all symbol counts.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BigInteger CalculateTotalCounts(int[] symbolCounts)
    {
        var hash = symbolCounts.GetHashCode();

        // Check if we have a cached value first
        if (_symbolCountsCache.TryGetValue(hash, out var cachedCounts) &&
            cachedCounts.SequenceEqual(symbolCounts))
        {
            return symbolCounts.Sum();
        }

        // Cache the symbol counts
        _symbolCountsCache[hash] = (int[])symbolCounts.Clone();
        return symbolCounts.Sum();
    }

    /// <summary>
    ///     Calculates the cumulative counts based on the symbol frequencies.
    /// </summary>
    /// <param name="symbolCounts">The frequency of each symbol in the sequence.</param>
    /// <returns>An array of cumulative counts as <see cref="BigInteger" />.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BigInteger[] GetCumulativeCounts(int[] symbolCounts)
    {
        var hash = symbolCounts.GetHashCode();

        // Check if we have a cached value first
        if (_cumulativeCountsCache.TryGetValue(hash, out var cachedCumuls) &&
            _symbolCountsCache.TryGetValue(hash, out var cachedCounts) &&
            cachedCounts.SequenceEqual(symbolCounts))
        {
            return cachedCumuls;
        }

        var cumulCounts = new BigInteger[symbolCounts.Length + 1];
        for (var i = 1; i <= symbolCounts.Length; i++)
        {
            cumulCounts[i] = cumulCounts[i - 1] + symbolCounts[i - 1];
        }

        // Cache the cumulative counts
        _cumulativeCountsCache[hash] = cumulCounts;
        return cumulCounts;
    }

    /// <summary>
    ///     Finds the symbol corresponding to the given slot in the cumulative counts using a binary search.
    /// </summary>
    /// <param name="slot">The slot value to be decoded.</param>
    /// <param name="cumulCounts">The cumulative counts array.</param>
    /// <returns>The index of the symbol corresponding to the given slot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CumulativeInverse(BigInteger slot, BigInteger[] cumulCounts)
    {
        // Binary search for the symbol
        int left = 1, right = cumulCounts.Length - 1;
        while (left < right)
        {
            var mid = (left + right) / 2;
            if (slot < cumulCounts[mid])
            {
                right = mid;
            }
            else
            {
                left = mid + 1;
            }
        }

        return left - 1;
    }

    /// <summary>
    ///     Clears the internal caches to free memory.
    /// </summary>
    public void ClearCaches()
    {
        _symbolCountsCache.Clear();
        _cumulativeCountsCache.Clear();
        _logger.Information("Cleared internal caches");
    }
}
