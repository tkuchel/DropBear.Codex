#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Errors;

#endregion

namespace DropBear.Codex.Serialization.Errors;

/// <summary>
///     Represents an error that occurred during compression or decompression operations.
///     Use this instead of throwing CompressionException.
/// </summary>
public sealed record CompressionError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CompressionError" /> class.
    /// </summary>
    /// <param name="message">The error message describing the compression failure.</param>
    public CompressionError(string message) : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets the compression algorithm being used.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    ///     Gets or sets the operation that failed (Compress or Decompress).
    /// </summary>
    public string? Operation { get; init; }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for a compression failure.
    /// </summary>
    /// <param name="algorithm">The compression algorithm.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new CompressionError instance.</returns>
    public static CompressionError CompressionFailed(string algorithm, string reason)
    {
        return new CompressionError($"Compression failed using '{algorithm}': {reason}")
        {
            Algorithm = algorithm,
            Operation = "Compress",
            Code = "COMP_COMPRESS_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for a decompression failure.
    /// </summary>
    /// <param name="algorithm">The compression algorithm.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <returns>A new CompressionError instance.</returns>
    public static CompressionError DecompressionFailed(string algorithm, string reason)
    {
        return new CompressionError($"Decompression failed using '{algorithm}': {reason}")
        {
            Algorithm = algorithm,
            Operation = "Decompress",
            Code = "COMP_DECOMPRESS_FAILED",
            Category = ErrorCategory.Technical,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for invalid compressed data.
    /// </summary>
    /// <param name="algorithm">The compression algorithm.</param>
    /// <returns>A new CompressionError instance.</returns>
    public static CompressionError InvalidCompressedData(string algorithm)
    {
        return new CompressionError($"Invalid or corrupted compressed data for algorithm '{algorithm}'")
        {
            Algorithm = algorithm,
            Operation = "Decompress",
            Code = "COMP_INVALID_DATA",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.High
        };
    }

    /// <summary>
    ///     Creates an error for an unsupported compression algorithm.
    /// </summary>
    /// <param name="algorithm">The unsupported algorithm.</param>
    /// <returns>A new CompressionError instance.</returns>
    public static CompressionError UnsupportedAlgorithm(string algorithm)
    {
        return new CompressionError($"Unsupported compression algorithm: {algorithm}")
        {
            Algorithm = algorithm,
            Code = "COMP_UNSUPPORTED_ALGORITHM",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.Medium
        };
    }

    /// <summary>
    ///     Creates an error for null or empty input data.
    /// </summary>
    /// <param name="operation">The operation being performed.</param>
    /// <returns>A new CompressionError instance.</returns>
    public static CompressionError NullOrEmptyData(string operation)
    {
        return new CompressionError($"Cannot {operation.ToLowerInvariant()} null or empty data")
        {
            Operation = operation,
            Code = "COMP_NULL_DATA",
            Category = ErrorCategory.Validation,
            Severity = ErrorSeverity.Medium
        };
    }

    #endregion
}
