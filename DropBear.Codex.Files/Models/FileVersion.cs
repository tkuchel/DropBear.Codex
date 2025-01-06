namespace DropBear.Codex.Files.Models;

/// <summary>
///     Represents a version label and date for a file, allowing basic equality checks.
/// </summary>
public sealed class FileVersion : IEquatable<FileVersion>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FileVersion" /> class.
    /// </summary>
    /// <param name="versionLabel">A string label identifying the version (e.g., "1.0.0").</param>
    /// <param name="versionDate">The <see cref="DateTimeOffset" /> representing when this version was created.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="versionLabel" /> is null.</exception>
    public FileVersion(string versionLabel, DateTimeOffset versionDate)
    {
        VersionLabel = versionLabel ?? throw new ArgumentNullException(nameof(versionLabel));
        VersionDate = versionDate;
    }

    /// <summary>
    ///     Gets the date/time associated with this version (UTC or local).
    /// </summary>
    public DateTimeOffset VersionDate { get; }

    /// <summary>
    ///     Gets the string label describing this version.
    /// </summary>
    public string VersionLabel { get; }

    /// <inheritdoc />
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

        return string.Equals(VersionLabel, other.VersionLabel, StringComparison.OrdinalIgnoreCase)
               && VersionDate.Equals(other.VersionDate);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as FileVersion);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(VersionLabel),
            VersionDate.GetHashCode());
    }

    /// <summary>
    ///     Equality operator for two <see cref="FileVersion" /> instances.
    /// </summary>
    public static bool operator ==(FileVersion? left, FileVersion? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    ///     Inequality operator for two <see cref="FileVersion" /> instances.
    /// </summary>
    public static bool operator !=(FileVersion? left, FileVersion? right)
    {
        return !(left == right);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{VersionLabel} ({VersionDate:g})";
    }
}
