#region

using System.Collections.Concurrent;
using System.Reflection;
using DropBear.Codex.Workflow.Persistence.Interfaces;
using Microsoft.Extensions.Logging;

#endregion

namespace DropBear.Codex.Workflow.Persistence.Implementation;

/// <summary>
///     Resolves workflow context types by scanning the current AppDomain assemblies.
/// </summary>
public sealed partial class AppDomainWorkflowTypeResolver : IWorkflowTypeResolver
{
    private readonly Lazy<ConcurrentDictionary<string, Type>> _knownTypes;
    private readonly ILogger<AppDomainWorkflowTypeResolver> _logger;

    /// <summary>
    ///     Initializes a new instance of the type resolver.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public AppDomainWorkflowTypeResolver(ILogger<AppDomainWorkflowTypeResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _knownTypes = new Lazy<ConcurrentDictionary<string, Type>>(DiscoverContextTypes);
    }

    public Type? ResolveContextType(string? assemblyQualifiedName, string? typeName)
    {
        if (string.IsNullOrEmpty(assemblyQualifiedName) && string.IsNullOrEmpty(typeName))
        {
            LogNoTypeInformationProvided();
            return null;
        }

        // Try assembly qualified name first (most reliable)
        if (!string.IsNullOrEmpty(assemblyQualifiedName))
        {
            var type = Type.GetType(assemblyQualifiedName);
            if (type is not null)
            {
                LogResolvedFromAssemblyQualifiedName(type.FullName ?? type.Name);
                return type;
            }
        }

        // Fallback: try to find by type name in known types
        if (!string.IsNullOrEmpty(typeName))
        {
            var contextTypes = _knownTypes.Value;
            if (contextTypes.TryGetValue(typeName, out Type? knownType))
            {
                LogResolvedFromKnownTypes(typeName);
                return knownType;
            }
        }

        LogCouldNotResolveType(typeName ?? assemblyQualifiedName ?? "unknown");
        return null;
    }

    public IReadOnlyDictionary<string, Type> GetKnownContextTypes() => _knownTypes.Value;

    private ConcurrentDictionary<string, Type> DiscoverContextTypes()
    {
        var contextTypes = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        try
        {
            LogDiscoveringContextTypes();

            Assembly[] assemblies =
            [
                .. AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !IsSystemAssembly(a))
            ];

            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    Type[] types =
                    [
                        .. assembly.GetTypes()
                            .Where(IsWorkflowContextType)
                    ];

                    foreach (Type type in types)
                    {
                        string key = type.FullName ?? type.Name;
                        _ = contextTypes.TryAdd(key, type);
                        LogDiscoveredContextType(key);
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    LogFailedToLoadTypes(assembly.FullName ?? "Unknown", ex);
                }
            }

            LogDiscoveredContextTypeCount(contextTypes.Count);
        }
        catch (ReflectionTypeLoadException ex)
        {
            LogContextTypeDiscoveryError(ex);
        }

        return contextTypes;
    }

    private static bool IsWorkflowContextType(Type type)
    {
        // More restrictive filtering to avoid internal types
        if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
        {
            return false;
        }

        // Exclude compiler-generated types
        if (type.Name.Contains("<") || type.Name.Contains(">"))
        {
            return false;
        }

        // Exclude WinRT types
        if (type.FullName?.StartsWith("WinRT.", StringComparison.Ordinal) == true)
        {
            return false;
        }

        // Exclude internal runtime types
        if (type.FullName?.Contains("+<") == true || type.FullName?.Contains(">d__") == true)
        {
            return false;
        }

        string? ns = type.Namespace;
        if (ns is null)
        {
            return false;
        }

        // Only include types from non-system namespaces
        if (ns.StartsWith("System", StringComparison.Ordinal) ||
            ns.StartsWith("Microsoft", StringComparison.Ordinal) ||
            ns.StartsWith("Windows", StringComparison.Ordinal) ||
            ns.StartsWith("WinRT", StringComparison.Ordinal) ||
            ns.StartsWith("Internal", StringComparison.Ordinal))
        {
            return false;
        }

        // Must have at least one public property
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length > 0;
    }

    private static bool IsSystemAssembly(Assembly assembly)
    {
        string assemblyName = assembly.GetName().Name ?? string.Empty;
        return assemblyName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase);
    }

    #region Logging

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "No type information provided for type resolution")]
    partial void LogNoTypeInformationProvided();

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Resolved type from assembly qualified name: {TypeName}")]
    partial void LogResolvedFromAssemblyQualifiedName(string typeName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Resolved type from known types: {TypeName}")]
    partial void LogResolvedFromKnownTypes(string typeName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not resolve context type: {TypeIdentifier}")]
    partial void LogCouldNotResolveType(string typeIdentifier);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Discovering workflow context types...")]
    partial void LogDiscoveringContextTypes();

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Discovered context type: {TypeName}")]
    partial void LogDiscoveredContextType(string typeName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Discovered {Count} workflow context types")]
    partial void LogDiscoveredContextTypeCount(int count);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to load types from assembly {AssemblyName}")]
    partial void LogFailedToLoadTypes(string assemblyName, ReflectionTypeLoadException exception);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Error during context type discovery")]
    partial void LogContextTypeDiscoveryError(ReflectionTypeLoadException exception);

    #endregion
}
