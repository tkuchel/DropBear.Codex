namespace DropBear.Codex.Workflow.Persistence.Interfaces;

/// <summary>
///     Resolves workflow context types using metadata and reflection.
/// </summary>
public interface IWorkflowTypeResolver
{
    /// <summary>
    ///     Attempts to resolve a context type from assembly qualified name or type name.
    /// </summary>
    /// <param name="assemblyQualifiedName">The assembly qualified name of the type</param>
    /// <param name="typeName">The simple type name as fallback</param>
    /// <returns>The resolved type, or null if not found</returns>
    Type? ResolveContextType(string? assemblyQualifiedName, string? typeName);

    /// <summary>
    ///     Gets all known workflow context types discovered at startup.
    /// </summary>
    /// <returns>A read-only dictionary of type name to Type</returns>
    IReadOnlyDictionary<string, Type> GetKnownContextTypes();
}
