﻿#region

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
    ///     Initializes a new instance of the <see cref="Snapshot{T}" /> class with the given state.
    /// </summary>
    /// <param name="state">The state to snapshot.</param>
    /// <param name="createdBy">
    ///     An optional identifier for who created the snapshot (defaults to the environment user).
    /// </param>
    public Snapshot(T state, string createdBy = "")
    {
        State = state;
        Timestamp = DateTimeOffset.UtcNow;
        CreatedBy = ResolveUserName(createdBy);
    }

    private Snapshot() // For serialization frameworks
    {
        State = default!;
        Timestamp = default;
        CreatedBy = string.Empty;
    }

    [DataMember(Order = 1)] public T State { get; private set; }

    [DataMember(Order = 2)] public DateTimeOffset Timestamp { get; private set; }

    [DataMember(Order = 3)] public string CreatedBy { get; private set; }

    public override string ToString()
    {
        return $"Snapshot of {typeof(T).Name} taken by {CreatedBy} at {Timestamp}";
    }

    private static string ResolveUserName(string createdBy)
    {
        return !string.IsNullOrEmpty(createdBy) ? createdBy : Environment.UserName;
    }
}
