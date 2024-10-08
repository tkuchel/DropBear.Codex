﻿#region

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
    private readonly ILogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeConverter" /> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging errors and information.</param>
    public TypeConverter(ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.Logger.ForContext<TypeConverter>();
    }

    /// <summary>
    ///     Reads and converts the JSON to <see cref="Type" />.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>The <see cref="Type" /> represented by the JSON string.</returns>
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
            return Type.GetType(typeName, AssemblyResolver, null, true);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to resolve Type: {TypeName}", typeName);
            return null;
        }
    }

    /// <summary>
    ///     Writes the <see cref="Type" /> to JSON.
    /// </summary>
    /// <param name="writer">The UTF-8 JSON writer.</param>
    /// <param name="value">The <see cref="Type" /> to write.</param>
    /// <param name="options">The serializer options.</param>
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

    private Assembly? AssemblyResolver(AssemblyName assemblyName)
    {
        try
        {
            return Assembly.Load(assemblyName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load assembly: {AssemblyName}", assemblyName.FullName);
            return null;
        }
    }
}
