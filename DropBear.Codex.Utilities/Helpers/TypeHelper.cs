#region

using System.Collections.Frozen;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for working with types, optimized for performance and .NET 8 features.
/// </summary>
public static class TypeHelper
{
    private static readonly FrozenSet<Type> PrimitiveTypes = new[]
    {
        typeof(int), typeof(double), typeof(float), typeof(long), typeof(short), typeof(byte), typeof(bool),
        typeof(char), typeof(decimal), typeof(string)
    }.ToFrozenSet();

    /// <summary>
    ///     Determines whether the specified type is of the specified target type or derives from it.
    /// </summary>
    public static Result<bool, TypeError> IsOfTypeOrDerivedFrom(Type? typeToCheck, Type? targetType)
    {
        if (typeToCheck is null || targetType is null)
        {
            return Result<bool, TypeError>.Failure(new TypeError("Types cannot be null."));
        }

        try
        {
            return Result<bool, TypeError>.Success(targetType.IsAssignableFrom(typeToCheck));
        }
        catch (Exception ex)
        {
            return Result<bool, TypeError>.Failure(new TypeError("Error checking type hierarchy.", ex));
        }
    }

    /// <summary>
    ///     Checks if a given type is a primitive or well-known simple type.
    /// </summary>
    public static Result<bool, TypeError> IsPrimitiveOrSimpleType(Type? type)
    {
        if (type is null)
        {
            return Result<bool, TypeError>.Failure(new TypeError("Type cannot be null."));
        }

        try
        {
            return Result<bool, TypeError>.Success(PrimitiveTypes.Contains(type));
        }
        catch (Exception ex)
        {
            return Result<bool, TypeError>.Failure(new TypeError("Error checking if type is primitive.", ex));
        }
    }
}
