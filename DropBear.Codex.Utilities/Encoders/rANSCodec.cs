#region

using System.Numerics;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Encoders;

/// <summary>
///     A class that provides methods to encode and decode sequences of symbols using the rANS (Range Asymmetric Numeral
///     Systems) codec.
/// </summary>
public sealed class rANSCodec
{
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="rANSCodec" /> class.
    /// </summary>
    public rANSCodec()
    {
        _logger = LoggerFactory.Logger.ForContext<rANSCodec>();
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
    /// <returns>A <see cref="BigInteger" /> representing the encoded state.</returns>
    /// <exception cref="ArgumentException">Thrown when input symbols or symbol counts are invalid.</exception>
    public BigInteger RansEncode(IList<int> symbols, int[] symbolCounts)
    {
        try
        {
            ValidateInputs(symbols, symbolCounts);
            BigInteger totalCounts = symbolCounts.Sum();
            var cumulCounts = GetCumulativeCounts(symbolCounts);
            BigInteger state = 1;

            foreach (var symbol in symbols)
            {
                BigInteger sCount = symbolCounts[symbol];
                state = (state / sCount * totalCounts) + cumulCounts[symbol] + (state % sCount);
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during encoding");
            throw;
        }
    }

    /// <summary>
    ///     Decodes an encoded state back to the original sequence of symbols using the rANS codec.
    /// </summary>
    /// <param name="state">The encoded state as a <see cref="BigInteger" />.</param>
    /// <param name="numSymbols">The number of symbols in the original sequence.</param>
    /// <param name="symbolCounts">The frequency of each symbol in the sequence.</param>
    /// <returns>The decoded sequence of symbols as an <see cref="IList{T}" /> of integers.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the state becomes negative during decoding.</exception>
    public IList<int> RansDecode(BigInteger state, int numSymbols, int[] symbolCounts)
    {
        try
        {
            BigInteger totalCounts = symbolCounts.Sum();
            var cumulCounts = GetCumulativeCounts(symbolCounts);
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
                    _logger.Error("State became negative during decoding");
                    throw new InvalidOperationException("State became negative during decoding, indicating an error.");
                }
            }

            decodedSymbols.Reverse();
            return decodedSymbols;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during decoding");
            throw;
        }
    }

    /// <summary>
    ///     Calculates the cumulative counts based on the symbol frequencies.
    /// </summary>
    /// <param name="symbolCounts">The frequency of each symbol in the sequence.</param>
    /// <returns>An array of cumulative counts as <see cref="BigInteger" />.</returns>
    private BigInteger[] GetCumulativeCounts(int[] symbolCounts)
    {
        var cumulCounts = new BigInteger[symbolCounts.Length + 1];
        for (var i = 1; i <= symbolCounts.Length; i++)
        {
            cumulCounts[i] = cumulCounts[i - 1] + symbolCounts[i - 1];
        }

        return cumulCounts;
    }

    /// <summary>
    ///     Finds the symbol corresponding to the given slot in the cumulative counts using a binary search.
    /// </summary>
    /// <param name="slot">The slot value to be decoded.</param>
    /// <param name="cumulCounts">The cumulative counts array.</param>
    /// <returns>The index of the symbol corresponding to the given slot.</returns>
    private int CumulativeInverse(BigInteger slot, BigInteger[] cumulCounts)
    {
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
    ///     Validates the inputs for the encoding and decoding methods.
    /// </summary>
    /// <param name="symbols">The sequence of symbols to encode or decode.</param>
    /// <param name="symbolCounts">The frequency of each symbol in the sequence.</param>
    /// <exception cref="ArgumentException">Thrown when input symbols or symbol counts are invalid.</exception>
    private void ValidateInputs(IList<int> symbols, int[] symbolCounts)
    {
        if (symbols == null || !symbols.Any())
        {
            _logger.Error("Input symbols cannot be null or empty.");
            throw new ArgumentException("Input symbols cannot be null or empty.", nameof(symbols));
        }

        if (symbolCounts == null || symbolCounts.Length == 0)
        {
            _logger.Error("Symbol counts cannot be null or empty.");
            throw new ArgumentException("Symbol counts cannot be null or empty.", nameof(symbolCounts));
        }

        if (symbolCounts.Any(count => count <= 0))
        {
            _logger.Error("Symbol counts must be positive integers.");
            throw new ArgumentException("Symbol counts must be positive integers.", nameof(symbolCounts));
        }
    }
}
