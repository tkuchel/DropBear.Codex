#region

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Files.Converters;

/// <summary>
///     A custom JSON converter for serializing and deserializing <see cref="Type" /> objects.
/// </summary>
public sealed class TypeConverter : JsonConverter<Type>
{
    // Use Dictionary<string, Type> for caching Type lookups to improve performance
    private static readonly Dictionary<string, Type> TypeCache = new(StringComparer.Ordinal);
    private static readonly Lock SyncLock = new();
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeConverter" /> class.
    /// </summary>
    /// <param name="logger">
    ///     An optional <see cref="ILogger" /> for logging errors and information.
    ///     If not provided, a default logger is created.
    /// </param>
    public TypeConverter(ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.Logger.ForContext<TypeConverter>();
    }

    /// <summary>
    ///     Reads and converts JSON input into a <see cref="Type" /> object.
    /// </summary>
    /// <param name="reader">A <see cref="Utf8JsonReader" /> pointing to the JSON input.</param>
    /// <param name="typeToConvert">The type to convert (always <see cref="Type" />).</param>
    /// <param name="options">Serializer options, including any custom converters.</param>
    /// <returns>A <see cref="Type" /> object or <c>null</c> if resolution failed.</returns>
    public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var typeName = reader.GetString();
        if (string.IsNullOrEmpty(typeName))
        {
            _logger.Warning("Failed to deserialize Type: typeName is null or empty.");
            return null;
        }

        try
        {
            // Check cache first for performance
            lock (SyncLock)
            {
                if (TypeCache.TryGetValue(typeName, out var cachedType))
                {
                    return cachedType;
                }
            }

            // Attempt to resolve the type by name
            var resolvedType = Type.GetType(typeName, AssemblyResolver, null, true);

            // Add to cache if resolved
            if (resolvedType != null)
            {
                lock (SyncLock)
                {
                    TypeCache[typeName] = resolvedType;
                }
            }

            return resolvedType;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to resolve Type: {TypeName}", typeName);
            return null;
        }
    }

    /// <summary>
    ///     Writes a <see cref="Type" /> object to JSON, storing its assembly-qualified name.
    /// </summary>
    /// <param name="writer">A <see cref="Utf8JsonWriter" /> for the JSON output.</param>
    /// <param name="value">The <see cref="Type" /> being serialized.</param>
    /// <param name="options">Serializer options, including any custom converters.</param>
    public override void Write(Utf8JsonWriter writer, Type? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        try
        {
            writer.WriteStringValue(value.AssemblyQualifiedName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to write Type: {TypeName}", value.FullName);
            writer.WriteNullValue();
        }
    }

    /// <summary>
    ///     Resolves an assembly name to an <see cref="Assembly" />, used when deserializing a <see cref="Type" />.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly to load.</param>
    /// <returns>An <see cref="Assembly" /> if resolution succeeds, or <c>null</c> otherwise.</returns>
    private Assembly? AssemblyResolver(AssemblyName assemblyName)
    {
        try
        {
            // Try to load from loaded assemblies first
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(
                    assemblyName, a.GetName()));

            if (assembly != null)
            {
                return assembly;
            }

            // Fall back to normal loading
            return Assembly.Load(assemblyName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load assembly: {AssemblyName}", assemblyName.FullName);
            return null;
        }
    }
}
