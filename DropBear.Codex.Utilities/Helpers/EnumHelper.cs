#region

using System.Collections.Frozen;
using System.ComponentModel;
using System.Reflection;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for working with enums, optimized for .NET 8.
/// </summary>
public static class EnumHelper
{
    private static readonly FrozenDictionary<Enum, string> EnumDescriptions = Enum.GetValues(typeof(Enum))
        .Cast<Enum>()
        .ToDictionary(e => e, e => e.GetType().GetField(e.ToString())?
            .GetCustomAttribute<DescriptionAttribute>()?.Description ?? e.ToString())
        .ToFrozenDictionary();

    /// <summary>
    ///     Retrieves the description attribute of an enum value, if present.
    /// </summary>
    public static Result<string, EnumError> GetEnumDescription(Enum value)
    {
        if (value is null)
        {
            return Result<string, EnumError>.Failure(new EnumError("Enum value cannot be null."));
        }

        try
        {
            return Result<string, EnumError>.Success(EnumDescriptions.GetValueOrDefault(value, value.ToString()));
        }
        catch (Exception ex)
        {
            return Result<string, EnumError>.Failure(new EnumError("Failed to retrieve enum description.", ex));
        }
    }

    /// <summary>
    ///     Parses a string to an enum of type T, optimized with <see cref="Span{T}" />.
    /// </summary>
    public static Result<T, EnumError> Parse<T>(ReadOnlySpan<char> value, bool ignoreCase = true) where T : struct, Enum
    {
        if (value.IsEmpty)
        {
            return Result<T, EnumError>.Failure(new EnumError("Input value cannot be null or empty."));
        }

        try
        {
            if (Enum.TryParse(value, ignoreCase, out T result))
            {
                return Result<T, EnumError>.Success(result);
            }

            return Result<T, EnumError>.Failure(
                new EnumError($"Failed to parse '{value.ToString()}' into {typeof(T).Name}."));
        }
        catch (Exception ex)
        {
            return Result<T, EnumError>.Failure(new EnumError("Error parsing enum value.", ex));
        }
    }

    /// <summary>
    ///     Retrieves all values of an enum type T.
    /// </summary>
    public static Result<FrozenSet<T>, EnumError> GetValues<T>() where T : struct, Enum
    {
        try
        {
            return Result<FrozenSet<T>, EnumError>.Success(Enum.GetValues<T>().ToFrozenSet());
        }
        catch (Exception ex)
        {
            return Result<FrozenSet<T>, EnumError>.Failure(new EnumError("Failed to retrieve enum values.", ex));
        }
    }
}
