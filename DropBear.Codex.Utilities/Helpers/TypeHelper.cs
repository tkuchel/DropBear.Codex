namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for working with types.
/// </summary>
public static class TypeHelper
{
    /// <summary>
    ///     Determines whether the specified type is of the specified target type or derives from it.
    /// </summary>
    /// <param name="typeToCheck">The type to check.</param>
    /// <param name="targetType">The target type to compare against.</param>
    /// <returns>
    ///     <see langword="true" /> if <paramref name="typeToCheck" /> is of type <paramref name="targetType" /> or
    ///     derives from it; otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when either <paramref name="typeToCheck" /> or
    ///     <paramref name="targetType" /> is null.
    /// </exception>
    public static bool IsOfTypeOrDerivedFrom(Type typeToCheck, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(typeToCheck, nameof(typeToCheck));
        ArgumentNullException.ThrowIfNull(targetType, nameof(targetType));

        // Check if the type is the same or if the type can be assigned to the target type
        return targetType.IsAssignableFrom(typeToCheck);
    }
}
