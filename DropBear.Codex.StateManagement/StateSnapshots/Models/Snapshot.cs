#region

using System.Runtime.Serialization;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Models;

/// <summary>
///     Represents a snapshot of state at a given time, optionally including information about who created it.
/// </summary>
/// <typeparam name="T">The state type contained in the snapshot.</typeparam>
[DataContract]
public sealed class Snapshot<T>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Snapshot{T}" /> class.
    /// </summary>
    /// <param name="state">The state to snapshot.</param>
    /// <param name="createdBy">An optional identifier for who created the snapshot (defaults to environment user).</param>
    public Snapshot(T state, string createdBy = "")
    {
        State = state;
        Timestamp = DateTimeOffset.UtcNow;
        CreatedBy = ResolveUserName(createdBy);
    }

    // Parameterless constructor for serialization frameworks requiring it
    private Snapshot()
    {
        State = default!;
        Timestamp = default;
        CreatedBy = string.Empty;
    }

    /// <summary>
    ///     Gets the state object.
    /// </summary>
    [DataMember(Order = 1)]
    public T State { get; private set; }

    /// <summary>
    ///     Gets the UTC timestamp when this snapshot was created.
    /// </summary>
    [DataMember(Order = 2)]
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    ///     Gets the identifier for who created this snapshot, if any.
    /// </summary>
    [DataMember(Order = 3)]
    public string CreatedBy { get; private set; }

    /// <summary>
    ///     Creates a string representing this snapshot, including its type, user, and timestamp.
    /// </summary>
    public override string ToString()
    {
        return $"Snapshot of {typeof(T).Name} taken by {CreatedBy} at {Timestamp}";
    }

    /// <summary>
    ///     Resolves a user name, defaulting to the environment user if <paramref name="createdBy" /> is empty.
    /// </summary>
    private static string ResolveUserName(string createdBy)
    {
        return !string.IsNullOrEmpty(createdBy)
            ? createdBy
            : Environment.UserName;
    }
}
