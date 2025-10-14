namespace DropBear.Codex.Workflow.Common;

/// <summary>
///     Centralized constants and configuration values for the workflow engine.
/// </summary>
public static class WorkflowConstants
{
    /// <summary>
    ///     Workflow execution limits and constraints.
    /// </summary>
#pragma warning disable CA1034
    public static class Limits
    {
        /// <summary>
        ///     Maximum number of parallel branches allowed in a single parallel node (default: 10).
        /// </summary>
        public const int MaxParallelBranches = 10;

        /// <summary>
        ///     Maximum error message length (default: 1000 characters).
        /// </summary>
        public const int MaxErrorMessageLength = 1000;

        /// <summary>
        ///     Maximum signal name length (default: 256 characters).
        /// </summary>
        public const int MaxSignalNameLength = 256;

        /// <summary>
        ///     Maximum workflow nesting depth (default: 10 levels).
        /// </summary>
        public const int MaxWorkflowNestingDepth = 10;

        /// <summary>
        ///     Maximum workflow ID length (default: 128 characters).
        /// </summary>
        public const int MaxWorkflowIdLength = 128;

        /// <summary>
        ///     Maximum display name length (default: 256 characters).
        /// </summary>
        public const int MaxDisplayNameLength = 256;

        /// <summary>
        ///     Maximum metadata key length (default: 128 characters).
        /// </summary>
        public const int MaxMetadataKeyLength = 128;

        /// <summary>
        ///     Maximum metadata value size in bytes (default: 64KB).
        /// </summary>
        public const int MaxMetadataValueSize = 65536;

        /// <summary>
        ///     Maximum execution trace entries (default: 10000).
        /// </summary>
        public const int MaxExecutionTraceEntries = 10000;

        /// <summary>
        ///     Maximum workflow timeout duration (default: 30 days).
        /// </summary>
        public static readonly TimeSpan MaxWorkflowTimeout = TimeSpan.FromDays(30);

        /// <summary>
        ///     Minimum workflow timeout duration (default: 1 second).
        /// </summary>
        public static readonly TimeSpan MinWorkflowTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        ///     Maximum step timeout duration (default: 1 hour).
        /// </summary>
        public static readonly TimeSpan MaxStepTimeout = TimeSpan.FromHours(1);
    }

    /// <summary>
    ///     Default configuration values used when not explicitly specified.
    /// </summary>
    public static class Defaults
    {
        /// <summary>
        ///     Default number of retry attempts for failed steps (default: 3).
        /// </summary>
        public const int DefaultRetryAttempts = 3;

        /// <summary>
        ///     Default backoff multiplier for exponential retry (default: 2.0).
        /// </summary>
        public const double DefaultBackoffMultiplier = 2.0;

        /// <summary>
        ///     Default workflow version when not specified.
        /// </summary>
        public static readonly Version DefaultVersion = new(1, 0);

        /// <summary>
        ///     Default base delay for exponential backoff retry strategy (default: 100ms).
        /// </summary>
        public static readonly TimeSpan DefaultRetryBaseDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>
        ///     Default maximum delay between retry attempts (default: 1 minute).
        /// </summary>
        public static readonly TimeSpan DefaultMaxRetryDelay = TimeSpan.FromMinutes(1);

        /// <summary>
        ///     Default signal timeout for persistent workflows (default: 24 hours).
        /// </summary>
        public static readonly TimeSpan DefaultSignalTimeout = TimeSpan.FromHours(24);

        /// <summary>
        ///     Default interval for checking workflow timeouts (default: 5 minutes).
        /// </summary>
        public static readonly TimeSpan DefaultTimeoutCheckInterval = TimeSpan.FromMinutes(5);

        /// <summary>
        ///     Default maximum degree of parallelism for parallel execution.
        /// </summary>
        public static int DefaultMaxDegreeOfParallelism => Environment.ProcessorCount;
    }

    /// <summary>
    ///     Node type identifiers for workflow graph nodes.
    /// </summary>
    public static class NodeTypes
    {
        public const string Step = "Step";
        public const string Conditional = "Conditional";
        public const string Parallel = "Parallel";
        public const string Delay = "Delay";
        public const string Sequence = "Sequence";
    }

    /// <summary>
    ///     Signal names for workflow suspension and resumption.
    /// </summary>
    public static class Signals
    {
        public const string SuspensionPrefix = "__SUSPEND__:";
        public const string ApprovalRequired = "__SUSPEND__:APPROVAL_REQUIRED";
        public const string ExternalSignalRequired = "__SUSPEND__:EXTERNAL_SIGNAL";

        /// <summary>
        ///     Checks if an error message represents a suspension signal.
        /// </summary>
        public static bool IsSuspensionSignal(string? message) =>
            !string.IsNullOrEmpty(message) && message.StartsWith(SuspensionPrefix, StringComparison.Ordinal);

        /// <summary>
        ///     Extracts the signal name from a suspension message.
        /// </summary>
        public static string? ExtractSignalName(string? message)
        {
            if (string.IsNullOrEmpty(message) || !IsSuspensionSignal(message))
            {
                return null;
            }

            return message[SuspensionPrefix.Length..];
        }

        /// <summary>
        ///     Creates a suspension message for a given signal name.
        /// </summary>
        public static string CreateSuspensionMessage(string signalName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
            return $"{SuspensionPrefix}{signalName}";
        }
    }

    /// <summary>
    ///     Monitoring and telemetry constants.
    /// </summary>
    public static class Monitoring
    {
        public const string ActivityNamePrefix = "DropBear.Codex.Workflow";
        public const string MeterName = "DropBear.Codex.Workflow";
    }

    /// <summary>
    ///     Metadata keys for workflow execution context.
    /// </summary>
    public static class MetadataKeys
    {
        public const string WorkflowId = "WorkflowId";
        public const string WorkflowInstanceId = "WorkflowInstanceId";
        public const string StepName = "StepName";
        public const string RetryAttempt = "RetryAttempt";
        public const string Suspension = "__Suspension__";
        public const string SignalName = "__SignalName__";
    }

    /// <summary>
    ///     Validation error codes.
    /// </summary>
    public static class ErrorCodes
    {
        public const string InvalidConfiguration = "WORKFLOW_INVALID_CONFIG";
        public const string ExecutionTimeout = "WORKFLOW_TIMEOUT";
        public const string Cancelled = "WORKFLOW_CANCELLED";
        public const string StepFailed = "WORKFLOW_STEP_FAILED";
        public const string Suspended = "WORKFLOW_SUSPENDED";
        public const string InvalidSignal = "WORKFLOW_INVALID_SIGNAL";
    }
#pragma warning restore CA1034
}
