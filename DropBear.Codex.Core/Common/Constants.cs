namespace DropBear.Codex.Core.Common;

/// <summary>
///     Centralized constants for the DropBear.Codex.Core library.
///     Provides compile-time constants and static readonly values used throughout the library.
/// </summary>
public static class Constants
{
    /// <summary>
    ///     Telemetry-related constants.
    /// </summary>
#pragma warning disable CA1034
    public static class Telemetry

    {
        /// <summary>
        ///     Default capacity for the telemetry event channel.
        /// </summary>
        public const int DefaultChannelCapacity = 1000;

        /// <summary>
        ///     Maximum capacity for the telemetry event channel.
        /// </summary>
        public const int MaxChannelCapacity = 100_000;

        /// <summary>
        ///     Minimum capacity for the telemetry event channel.
        /// </summary>
        public const int MinChannelCapacity = 10;

        /// <summary>
        ///     Name of the ActivitySource for distributed tracing.
        /// </summary>
        public const string ActivitySourceName = "DropBear.Codex.Core.Results";

        /// <summary>
        ///     Name of the Meter for metrics.
        /// </summary>
        public const string MeterName = "DropBear.Codex.Core.Results";

        /// <summary>
        ///     Version of the instrumentation.
        /// </summary>
        public const string InstrumentationVersion = "2.0.0";

        /// <summary>
        ///     Default timeout for shutting down the telemetry processor.
        /// </summary>
        public static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        ///     Maximum timeout for shutting down the telemetry processor.
        /// </summary>
        public static readonly TimeSpan MaxShutdownTimeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    ///     Limits for various string and collection sizes.
    /// </summary>
    public static class Limits
    {
        /// <summary>
        ///     Maximum length for envelope header keys.
        /// </summary>
        public const int MaxHeaderKeyLength = 256;

        /// <summary>
        ///     Maximum length for telemetry tag keys.
        /// </summary>
        public const int MaxTagKeyLength = 128;

        /// <summary>
        ///     Maximum length for error messages.
        /// </summary>
        public const int MaxErrorMessageLength = 4096;

        /// <summary>
        ///     Maximum length for error codes.
        /// </summary>
        public const int MaxErrorCodeLength = 64;

        /// <summary>
        ///     Maximum number of metadata entries in an error.
        /// </summary>
        public const int MaxErrorMetadataCount = 50;

        /// <summary>
        ///     Maximum depth for nested result transformations before warning.
        /// </summary>
        public const int MaxTransformationDepth = 100;
    }

    /// <summary>
    ///     Envelope-related constants.
    /// </summary>
    public static class Envelope
    {
        /// <summary>
        ///     Default envelope ID format.
        /// </summary>
        public const string DefaultIdFormat = "N"; // 32 digits without hyphens

        /// <summary>
        ///     Maximum number of headers in an envelope.
        /// </summary>
        public const int MaxHeaderCount = 100;

        /// <summary>
        ///     Header key prefix for system-reserved headers.
        /// </summary>
        public const string SystemHeaderPrefix = "x-system-";

        /// <summary>
        ///     Header key for envelope version.
        /// </summary>
        public const string VersionHeaderKey = "x-system-version";

        /// <summary>
        ///     Header key for content type.
        /// </summary>
        public const string ContentTypeHeaderKey = "content-type";
    }

    /// <summary>
    ///     Serialization-related constants.
    /// </summary>
    public static class SerializationConstants
    {
        /// <summary>
        ///     Default buffer size for serialization operations.
        /// </summary>
        public const int DefaultBufferSize = 8192;

        /// <summary>
        ///     Maximum size for serialized payloads (10 MB).
        /// </summary>
        public const int MaxPayloadSize = 10 * 1024 * 1024;

        /// <summary>
        ///     Encoding name for UTF-8.
        /// </summary>
        public const string Utf8EncodingName = "utf-8";
    }

    /// <summary>
    ///     Caching and pooling constants.
    /// </summary>
    public static class Pooling
    {
        /// <summary>
        ///     Default object pool size.
        /// </summary>
        public const int DefaultPoolSize = 32;

        /// <summary>
        ///     Maximum object pool size.
        /// </summary>
        public const int MaxPoolSize = 1024;

        /// <summary>
        ///     Default time to keep pooled objects before disposal.
        /// </summary>
        public static readonly TimeSpan DefaultPoolRetentionTime = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    ///     Retry and timeout constants.
    /// </summary>
    public static class Retry
    {
        /// <summary>
        ///     Default number of retry attempts.
        /// </summary>
        public const int DefaultMaxAttempts = 3;

        /// <summary>
        ///     Default multiplier for exponential backoff.
        /// </summary>
        public const double DefaultBackoffMultiplier = 2.0;

        /// <summary>
        ///     Default delay between retry attempts.
        /// </summary>
        public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>
        ///     Default maximum delay between retry attempts.
        /// </summary>
        public static readonly TimeSpan DefaultMaxRetryDelay = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    ///     Validation constants.
    /// </summary>
    public static class Validation
    {
        /// <summary>
        ///     Characters allowed in header keys.
        /// </summary>
        public const string AllowedHeaderKeyCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

        /// <summary>
        ///     Characters allowed in error codes.
        /// </summary>
        public const string AllowedErrorCodeCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";

        /// <summary>
        ///     Minimum length for identifiers.
        /// </summary>
        public const int MinIdentifierLength = 1;

        /// <summary>
        ///     Maximum length for identifiers.
        /// </summary>
        public const int MaxIdentifierLength = 256;
    }

    /// <summary>
    ///     Error message templates.
    /// </summary>
    public static class ErrorMessages
    {
        /// <summary>
        ///     Template for argument null exception messages.
        /// </summary>
        public const string ArgumentNullTemplate = "Argument '{0}' cannot be null";

        /// <summary>
        ///     Template for argument validation exception messages.
        /// </summary>
        public const string ArgumentInvalidTemplate = "Argument '{0}' is invalid: {1}";

        /// <summary>
        ///     Template for operation failed messages.
        /// </summary>
        public const string OperationFailedTemplate = "Operation '{0}' failed: {1}";

        /// <summary>
        ///     Message for when a required operation is not supported.
        /// </summary>
        public const string OperationNotSupported = "This operation is not supported";

        /// <summary>
        ///     Message for when an object has been disposed.
        /// </summary>
        public const string ObjectDisposed = "Cannot access a disposed object";
    }

    /// <summary>
    ///     Diagnostic and logging constants.
    /// </summary>
    public static class DiagnosticsConstants
    {
        /// <summary>
        ///     Maximum number of stack trace frames to capture.
        /// </summary>
        public const int MaxStackTraceFrames = 50;

        /// <summary>
        ///     Maximum length of diagnostic messages.
        /// </summary>
        public const int MaxDiagnosticMessageLength = 2048;

        /// <summary>
        ///     Format for timestamps in diagnostic messages.
        /// </summary>
        public const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    }

    /// <summary>
    ///     Version information.
    /// </summary>
    public static class Version
    {
        /// <summary>
        ///     Current library version.
        /// </summary>
        public const string Current = "2025.10.0";

        /// <summary>
        ///     Minimum compatible version.
        /// </summary>
        public const string MinCompatible = "2025.9.0";

        /// <summary>
        ///     Semantic version format.
        /// </summary>
        public const string Format = "yyyy.M.patch";
    }
#pragma warning restore CA1034
}
