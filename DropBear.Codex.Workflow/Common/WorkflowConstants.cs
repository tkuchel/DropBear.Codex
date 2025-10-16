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
        /// <summary>
        ///     Step node type identifier.
        /// </summary>
        public const string Step = "Step";

        /// <summary>
        ///     Conditional node type identifier.
        /// </summary>
        public const string Conditional = "Conditional";

        /// <summary>
        ///     Parallel node type identifier.
        /// </summary>
        public const string Parallel = "Parallel";

        /// <summary>
        ///     Delay node type identifier.
        /// </summary>
        public const string Delay = "Delay";

        /// <summary>
        ///     Sequence node type identifier.
        /// </summary>
        public const string Sequence = "Sequence";
    }

    /// <summary>
    ///     Signal names for workflow suspension and resumption.
    /// </summary>
    public static class Signals
    {
        /// <summary>
        ///     Prefix for suspension signal messages.
        /// </summary>
        public const string SuspensionPrefix = "__SUSPEND__:";

        /// <summary>
        ///     Signal name for approval required.
        /// </summary>
        public const string ApprovalRequired = "__SUSPEND__:APPROVAL_REQUIRED";

        /// <summary>
        ///     Signal name for external signal required.
        /// </summary>
        public const string ExternalSignalRequired = "__SUSPEND__:EXTERNAL_SIGNAL";

        /// <summary>
        ///     Checks if an error message represents a suspension signal.
        /// </summary>
        /// <param name="message">The message to check</param>
        /// <returns>True if the message is a suspension signal</returns>
        public static bool IsSuspensionSignal(string? message) =>
            !string.IsNullOrEmpty(message) && message.StartsWith(SuspensionPrefix, StringComparison.Ordinal);

        /// <summary>
        ///     Extracts the signal name from a suspension message.
        /// </summary>
        /// <param name="message">The suspension message</param>
        /// <returns>The signal name, or null if not a valid suspension message</returns>
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
        /// <param name="signalName">The signal name</param>
        /// <returns>The formatted suspension message</returns>
        public static string CreateSuspensionMessage(string signalName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
            return $"{SuspensionPrefix}{signalName}";
        }
    }

    /// <summary>
    ///     Metadata keys used in workflow execution.
    /// </summary>
    public static class MetadataKeys
    {
        /// <summary>
        ///     Metadata key for suspension flag.
        /// </summary>
        public const string Suspension = "IsSuspension";

        /// <summary>
        ///     Metadata key for signal name.
        /// </summary>
        public const string SignalName = "SignalName";

        /// <summary>
        ///     Metadata key for step execution time.
        /// </summary>
        public const string ExecutionTime = "ExecutionTime";

        /// <summary>
        ///     Metadata key for retry attempt count.
        /// </summary>
        public const string RetryAttempt = "RetryAttempt";

        /// <summary>
        ///     Metadata key for condition result.
        /// </summary>
        public const string ConditionResult = "ConditionResult";

        /// <summary>
        ///     Metadata key for branch taken.
        /// </summary>
        public const string BranchTaken = "BranchTaken";

        /// <summary>
        ///     Metadata key for delay duration.
        /// </summary>
        public const string DelayDuration = "DelayDuration";

        /// <summary>
        ///     Metadata key for parallel branch count.
        /// </summary>
        public const string ParallelBranchCount = "ParallelBranchCount";

        /// <summary>
        ///     Metadata key for sequence completion flag.
        /// </summary>
        public const string SequenceCompleted = "SequenceCompleted";

        /// <summary>
        ///     Metadata key for nodes executed count.
        /// </summary>
        public const string NodesExecuted = "NodesExecuted";

        /// <summary>
        ///     Metadata key for total nodes count.
        /// </summary>
        public const string TotalNodes = "TotalNodes";
    }

    /// <summary>
    ///     Monitoring and telemetry constants.
    /// </summary>
    public static class Monitoring
    {
        /// <summary>
        ///     Activity name prefix for distributed tracing.
        /// </summary>
        public const string ActivityNamePrefix = "DropBear.Codex.Workflow";

        /// <summary>
        ///     Meter name for metrics.
        /// </summary>
        public const string MeterName = "DropBear.Codex.Workflow";

        /// <summary>
        ///     Instrumentation version.
        /// </summary>
        public const string InstrumentationVersion = "2025.1.0";

        /// <summary>
        ///     Tag name for workflow ID.
        /// </summary>
        public const string TagWorkflowId = "workflow.id";

        /// <summary>
        ///     Tag name for workflow instance ID.
        /// </summary>
        public const string TagWorkflowInstanceId = "workflow.instance_id";

        /// <summary>
        ///     Tag name for step name.
        /// </summary>
        public const string TagStepName = "workflow.step_name";

        /// <summary>
        ///     Tag name for workflow status.
        /// </summary>
        public const string TagWorkflowStatus = "workflow.status";

        /// <summary>
        ///     Tag name for correlation ID.
        /// </summary>
        public const string TagCorrelationId = "correlation.id";

        /// <summary>
        ///     Metric name for workflow execution duration.
        /// </summary>
        public const string MetricWorkflowDuration = "workflow.execution.duration";

        /// <summary>
        ///     Metric name for step execution duration.
        /// </summary>
        public const string MetricStepDuration = "workflow.step.duration";

        /// <summary>
        ///     Metric name for workflow failures.
        /// </summary>
        public const string MetricWorkflowFailures = "workflow.failures";

        /// <summary>
        ///     Metric name for step retries.
        /// </summary>
        public const string MetricStepRetries = "workflow.step.retries";
    }
#pragma warning restore CA1034
}
