#region

using System.Runtime.Serialization;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Models;

[DataContract]
public sealed class Snapshot<T>
{
    public Snapshot(T state, string createdBy = "")
    {
        State = state;
        Timestamp = DateTimeOffset.UtcNow;
        CreatedBy = ResolveUserName(createdBy);
    }

    private Snapshot() // For serialization frameworks that require a parameterless constructor
    {
        State = default!;
        Timestamp = default;
        CreatedBy = string.Empty;
    }

    [DataMember(Order = 1)] public T State { get; private set; }

    [DataMember(Order = 2)] public DateTimeOffset Timestamp { get; private set; }

    [DataMember(Order = 3)] public string CreatedBy { get; private set; }

    private static string ResolveUserName(string createdBy)
    {
        if (!string.IsNullOrEmpty(createdBy))
        {
            return createdBy;
        }

        return Environment.UserName;
    }

    public override string ToString()
    {
        return $"Snapshot of {typeof(T).Name} taken by {CreatedBy} at {Timestamp}";
    }
}
