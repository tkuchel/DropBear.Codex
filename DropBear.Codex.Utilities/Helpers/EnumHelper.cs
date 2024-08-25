#region

using System.ComponentModel;

#endregion

namespace DropBear.Codex.Utilities.Helpers;

/// <summary>
///     Provides utility methods for working with enums, such as retrieving descriptions, parsing values, and accessing
///     custom attributes.
/// </summary>
public static class EnumHelper
{
    /// <summary>
    ///     Retrieves the description attribute of an enum value, if present.
    /// </summary>
    /// <param name="value">The enum value to retrieve the description for.</param>
    /// <returns>The description attribute's value if present; otherwise, the enum value's name.</returns>
    public static string GetEnumDescription(Enum value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var type = value.GetType();
        var fieldInfo = type.GetField(value.ToString());

        if (fieldInfo == null)
        {
            return value.ToString();
        }

        var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

        return attributes.Length > 0 ? attributes[0].Description : value.ToString();
    }

    /// <summary>
    ///     Retrieves a custom attribute applied to an enum value.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the attribute to retrieve.</typeparam>
    /// <param name="value">The enum value to retrieve the attribute for.</param>
    /// <returns>The custom attribute if found; otherwise, null.</returns>
    public static TAttribute? GetCustomAttributeValue<TAttribute>(Enum value) where TAttribute : Attribute
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var type = value.GetType();
        var name = Enum.GetName(type, value);

        return name is not null
            ? type.GetField(name)?.GetCustomAttributes(false).OfType<TAttribute>().SingleOrDefault()
            : null;
    }

    /// <summary>
    ///     Parses a string to an enum of type T.
    /// </summary>
    /// <typeparam name="T">The enum type to parse the value into.</typeparam>
    /// <param name="value">The string representation of the enum value.</param>
    /// <param name="ignoreCase">Indicates whether to ignore case during parsing.</param>
    /// <returns>The enum value corresponding to the string.</returns>
    public static T Parse<T>(string value, bool ignoreCase = true) where T : Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(nameof(value));
        }

        try
        {
            return (T)Enum.Parse(typeof(T), value, ignoreCase);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Failed to parse '{value}' into enum {typeof(T).Name}.", ex);
        }
    }

    /// <summary>
    ///     Retrieves all values of an enum type T.
    /// </summary>
    /// <typeparam name="T">The enum type to retrieve values for.</typeparam>
    /// <returns>An array of all enum values for the specified type.</returns>
    public static T[] GetValues<T>() where T : Enum
    {
        return Enum.GetValues(typeof(T)).Cast<T>().ToArray();
    }
}
