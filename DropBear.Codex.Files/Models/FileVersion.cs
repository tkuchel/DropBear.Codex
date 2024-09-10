namespace DropBear.Codex.Files.Models;

/// <summary>
///     Represents a version of a file, including a version label and the date of the version.
/// </summary>
public sealed class FileVersion : IEquatable<FileVersion>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileVersion" /> class.
    /// </summary>
    /// <param name="versionLabel">The label of the version.</param>
    /// <param name="versionDate">The date of the version.</param>
    /// <exception cref="ArgumentNullException">Thrown when the version label is null.</exception>
    public FileVersion(string versionLabel, DateTimeOffset versionDate)
    {
        VersionLabel = versionLabel ?? throw new ArgumentNullException(nameof(versionLabel));
        VersionDate = versionDate;
    }

    /// <summary>
    ///     Gets the date of the version.
    /// </summary>
    public DateTimeOffset VersionDate { get; }

    /// <summary>
    ///     Gets the label of the version.
    /// </summary>
    public string VersionLabel { get; }

    /// <summary>
    ///     Determines whether the specified FileVersion is equal to the current FileVersion.
    /// </summary>
    /// <param name="other">The FileVersion to compare with the current FileVersion.</param>
    /// <returns>True if the specified FileVersion is equal to the current FileVersion; otherwise, false.</returns>
    public bool Equals(FileVersion? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(VersionLabel, other.VersionLabel, StringComparison.OrdinalIgnoreCase) &&
               VersionDate.Equals(other.VersionDate);
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as FileVersion);
    }

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(VersionLabel),
            VersionDate.GetHashCode());
    }

    public static bool operator ==(FileVersion? left, FileVersion? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    public static bool operator !=(FileVersion? left, FileVersion? right)
    {
        return !(left == right);
    }

    /// <summary>
    ///     Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"{VersionLabel} ({VersionDate:g})";
    }
}
