using System.Diagnostics.CodeAnalysis;

// Suppress analyzer warnings for example/demonstration code
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Design", "CA1056:URI properties should not be strings", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Design", "CA2227:Collection properties should be read only", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Performance", "CA1848:Use LoggerMessage delegates", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Usage", "CA2007:Consider calling ConfigureAwait", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]

// Suppress Meziantou analyzer warnings for example code
[assembly: SuppressMessage("Design", "MA0002:IEqualityComparer<string> or IComparer<string> is missing", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Design", "MA0004:Use Task.ConfigureAwait(false)", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Design", "MA0015:Specify the parameter name", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Design", "MA0016:Prefer using collection abstraction", Justification = "Example code for demonstration purposes", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]
[assembly: SuppressMessage("Design", "MA0048:File name must match type name", Justification = "Example code with multiple related types", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Examples")]

// Suppress warnings for persistence models (demo/sample code)
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Sample model code", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Models")]
[assembly: SuppressMessage("Design", "CA1056:URI properties should not be strings", Justification = "Sample model code", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Models")]
[assembly: SuppressMessage("Design", "MA0002:IEqualityComparer<string> or IComparer<string> is missing", Justification = "Sample model code", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Models")]
[assembly: SuppressMessage("Design", "MA0016:Prefer using collection abstraction", Justification = "Sample model code", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Models")]
[assembly: SuppressMessage("Design", "MA0048:File name must match type name", Justification = "Sample model with multiple related types", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Models")]

// Suppress specific warnings for production code where appropriate
[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory methods for Result pattern", Scope = "type", Target = "~T:DropBear.Codex.Workflow.Results.WorkflowResult`1")]
[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Factory methods for Result pattern", Scope = "type", Target = "~T:DropBear.Codex.Workflow.Results.NodeExecutionResult`1")]
[assembly: SuppressMessage("Design", "CA1724:Type names should not match namespaces", Justification = "Workflow is the core domain type", Scope = "type", Target = "~T:DropBear.Codex.Workflow.Core.Workflow`1")]
[assembly: SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via dependency injection", Scope = "type", Target = "~T:DropBear.Codex.Workflow.Persistence.Services.NoOpWorkflowNotificationService")]
[assembly: SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated via reflection/DI", Scope = "type", Target = "~T:DropBear.Codex.Workflow.Core.WorkflowExecutionContext`1")]
[assembly: SuppressMessage("Design", "CA1859:Use concrete types when possible for improved performance", Justification = "Interface return type required for polymorphism", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Nodes")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Arguments validated by caller in library code", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Extensions")]

// Suppress style warnings where they conflict with design decisions
[assembly: SuppressMessage("Style", "IDE0011:Add braces", Justification = "Simple one-line if statements are more readable", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow")]
[assembly: SuppressMessage("Style", "IDE0019:Use pattern matching", Justification = "Explicit type checking is clearer in complex scenarios", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow")]

// Suppress globalization warnings for diagnostic/debugging output
[assembly: SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "Diagnostic output doesn't require localization", Scope = "member", Target = "~M:DropBear.Codex.Workflow.Results.WorkflowResult`1.ToExecutionReport~System.String")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Diagnostic output doesn't require localization", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Results")]
[assembly: SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Diagnostic output doesn't require localization", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Extensions")]
[assembly: SuppressMessage("Globalization", "CA1307:Specify StringComparison", Justification = "Default string comparison is appropriate for internal logic", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow")]
[assembly: SuppressMessage("Globalization", "CA1310:Specify StringComparison for correctness", Justification = "Default string comparison is appropriate for internal logic", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow")]
[assembly: SuppressMessage("Globalization", "MA0011:IFormatProvider is missing", Justification = "Diagnostic output doesn't require localization", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Extensions")]

// Suppress parameter default value warnings for backwards compatibility
[assembly: SuppressMessage("Design", "MA0015:Specify the parameter name in ArgumentException", Justification = "Backwards compatibility with existing API", Scope = "type", Target = "~T:DropBear.Codex.Workflow.Persistence.Configuration.PersistentWorkflowOptions")]

// Suppress overly strict logging performance warnings for non-hot-path code
[assembly: SuppressMessage("Performance", "CA1848:Use LoggerMessage delegates", Justification = "Performance impact is negligible in non-hot-path code", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence")]
[assembly: SuppressMessage("Performance", "CA1848:Use LoggerMessage delegates", Justification = "Performance impact is negligible in non-hot-path code", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Nodes")]

// Suppress method length warnings for complex but cohesive methods
[assembly: SuppressMessage("Design", "MA0051:Method is too long", Justification = "Complex orchestration logic is cohesive and well-structured", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Implementation")]
[assembly: SuppressMessage("Design", "MA0051:Method is too long", Justification = "Complex node execution logic is cohesive", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Nodes")]

// Suppress general exception catching in error handling methods
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error handling must catch all exceptions", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Implementation")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Step execution must catch all exceptions", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Nodes")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Extension methods must handle all exceptions gracefully", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Extensions")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Service must catch all exceptions", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Services")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Step compensation must catch all exceptions", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Steps")]

// Suppress string comparison warnings for internal helper methods
[assembly: SuppressMessage("Performance", "MA0074:Prefer char.IsDigit instead of string.Contains", Justification = "String contains is clearer for this logic", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow")]

// Suppress collection performance warnings where readability is preferred
[assembly: SuppressMessage("Performance", "MA0020:Use direct methods instead of LINQ methods", Justification = "Readability over micro-optimization", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow")]

// Suppress hash code warnings for internal types
[assembly: SuppressMessage("Performance", "MA0002:IEqualityComparer<string> or IComparer<string> is missing", Justification = "Default comparison is appropriate for internal logic", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow")]
[assembly: SuppressMessage("Performance", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Specific collection type required for performance", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence")]
[assembly: SuppressMessage("Performance", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Specific collection type required", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Extensions")]

// Suppress method override parameter warnings for persistence engine
[assembly: SuppressMessage("Design", "MA0061:Method overrides should not change default values", Justification = "Override implementation requires different default behavior", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Implementation")]

// Suppress performance warnings where optimization is not critical
[assembly: SuppressMessage("Performance", "CA1860:Avoid using 'Enumerable.Any()' extension method", Justification = "Readability preferred over micro-optimization in non-hot-path code", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow")]
[assembly: SuppressMessage("Performance", "CA1847:Use char literal for a single character lookup", Justification = "String literal is clearer in context", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence")]

// Temporarily suppress missing XML documentation warnings (TODO: Add comprehensive documentation)
[assembly: SuppressMessage("Documentation", "CS1591:Missing XML comment for publicly visible type or member", Justification = "Documentation in progress", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Builder")]
[assembly: SuppressMessage("Documentation", "CS1591:Missing XML comment for publicly visible type or member", Justification = "Documentation in progress", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Core")]
[assembly: SuppressMessage("Documentation", "CS1591:Missing XML comment for publicly visible type or member", Justification = "Documentation in progress", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Nodes")]
[assembly: SuppressMessage("Documentation", "CS1591:Missing XML comment for publicly visible type or member", Justification = "Documentation in progress", Scope = "namespaceanddescendants", Target = "~N:DropBear.Codex.Workflow.Persistence.Implementation")]
