namespace DropBear.Codex.Files.Models;

/// <summary>
///     Represents a version of a file, including a version label and the date of the version.
/// </summary>
public class FileVersion
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
    ///     Gets or sets the date of the version.
    /// </summary>
    public DateTimeOffset VersionDate { get; set; }

    /// <summary>
    ///     Gets or sets the label of the version.
    /// </summary>
    public string VersionLabel { get; set; }

    /// <summary>
    ///     Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is FileVersion other)
        {
            return string.Equals(VersionLabel, other.VersionLabel, StringComparison.OrdinalIgnoreCase) &&
                   VersionDate.Equals(other.VersionDate);
        }

        return false;
    }

    /// <summary>
    ///     Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        unchecked // Overflow is fine, just wrap
        {
            var hash = 17;
            hash = (hash * 23) +
                   (VersionLabel != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(VersionLabel) : 0);
            hash = (hash * 23) + VersionDate.GetHashCode();
            return hash;
        }
    }
}
