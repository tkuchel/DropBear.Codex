#region

using MessagePack;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     Data Transfer Object for envelope serialization.
///     Optimized for MessagePack binary serialization with concrete types for compatibility.
/// </summary>
/// <typeparam name="TPayload">The type of the payload contained in the envelope.</typeparam>
/// <remarks>
///     <para>
///         This DTO uses <see cref="Dictionary{TKey,TValue}" /> for the Headers property to ensure
///         compatibility with MessagePack serialization, which requires concrete collection types.
///     </para>
///     <para>
///         After deserialization, the <see cref="Envelope{T}.FromDto" /> method converts the Dictionary
///         to a <see cref="System.Collections.Frozen.FrozenDictionary{TKey,TValue}" /> for optimal
///         read performance in the domain model.
///     </para>
/// </remarks>
[MessagePackObject]
public sealed record EnvelopeDto<TPayload>
{
    /// <summary>
    ///     Gets or initializes the payload contained in the envelope.
    /// </summary>
    /// <value>
    ///     The payload data, or <c>null</c> if the envelope is empty or being used as a control message.
    /// </value>
    [Key(0)]
    public TPayload? Payload { get; init; }

    /// <summary>
    ///     Gets or initializes the headers associated with the envelope.
    /// </summary>
    /// <value>
    ///     A dictionary of header key-value pairs, or <c>null</c> if no headers are present.
    /// </value>
    /// <remarks>
    ///     <para>
    ///         Headers contain metadata about the envelope and its payload such as correlation IDs,
    ///         message types, routing information, and custom application-specific metadata.
    ///     </para>
    ///     <para>
    ///         Uses <see cref="Dictionary{TKey,TValue}" /> for MessagePack serialization compatibility.
    ///         Keys use <see cref="StringComparer.Ordinal" /> for case-sensitive comparison.
    ///         The CA1002 analyzer warning is suppressed via GlobalSuppressions.cs due to serialization requirements.
    ///     </para>
    /// </remarks>
    [Key(1)]
#pragma warning disable MA0016
    public Dictionary<string, object>? Headers { get; init; }
#pragma warning restore MA0016

    /// <summary>
    ///     Gets or initializes a value indicating whether the envelope is sealed (immutable and signed).
    /// </summary>
    /// <value>
    ///     <c>true</c> if the envelope is sealed; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    ///     When sealed, the envelope has been cryptographically signed and cannot be modified.
    ///     Sealed envelopes must have both <see cref="Signature" /> and <see cref="SealedAt" /> populated.
    ///     Attempting to seal an already sealed envelope will throw an <see cref="InvalidOperationException" />.
    /// </remarks>
    [Key(2)]
    public bool IsSealed { get; init; }

    /// <summary>
    ///     Gets or initializes the UTC timestamp when the envelope was created.
    /// </summary>
    /// <value>
    ///     A <see cref="DateTime" /> representing the creation time in UTC.
    /// </value>
    /// <remarks>
    ///     This timestamp is immutable and set when the envelope is first instantiated.
    ///     Should never be <see cref="DateTime.MinValue" /> or default. The <see cref="IsValid" />
    ///     method validates this constraint.
    /// </remarks>
    [Key(3)]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    ///     Gets or initializes the UTC timestamp when the envelope was sealed.
    /// </summary>
    /// <value>
    ///     A <see cref="DateTime" /> if the envelope is sealed; otherwise, <c>null</c>.
    /// </value>
    /// <remarks>
    ///     This value is required for sealed envelopes (<see cref="IsSealed" /> = <c>true</c>)
    ///     and must be <c>null</c> for unsealed envelopes. The sealed timestamp must be
    ///     equal to or after <see cref="CreatedAt" />.
    /// </remarks>
    [Key(4)]
    public DateTime? SealedAt { get; init; }

    /// <summary>
    ///     Gets or initializes the digital signature of the envelope.
    /// </summary>
    /// <value>
    ///     A cryptographic signature string if the envelope is sealed; otherwise, <c>null</c>.
    /// </value>
    /// <remarks>
    ///     <para>
    ///         This value is required for sealed envelopes (<see cref="IsSealed" /> = <c>true</c>)
    ///         and must be <c>null</c> for unsealed envelopes.
    ///     </para>
    ///     <para>
    ///         The signature is generated from the envelope's payload and headers using
    ///         a cryptographic signing function provided to <see cref="Envelope{T}.Seal" />,
    ///         enabling integrity verification through <see cref="Envelope{T}.VerifySignature" />.
    ///     </para>
    /// </remarks>
    [Key(5)]
    public string? Signature { get; init; }

    /// <summary>
    ///     Validates the consistency and integrity of the DTO's state.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if the DTO state is valid and consistent; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    ///     Validates the following constraints:
    ///     <list type="bullet">
    ///         <item>Sealed envelopes must have both <see cref="Signature" /> and <see cref="SealedAt" /> populated</item>
    ///         <item><see cref="CreatedAt" /> must not be default (<see cref="DateTime.MinValue" />)</item>
    ///         <item>If <see cref="SealedAt" /> exists, it must be equal to or after <see cref="CreatedAt" /></item>
    ///     </list>
    ///     This method is called by <see cref="MessagePackEnvelopeSerializer" /> during deserialization
    ///     to ensure data integrity.
    /// </remarks>
    public bool IsValid()
    {
        // Sealed envelopes must have signature and sealed date
        if (IsSealed && (string.IsNullOrWhiteSpace(Signature) || SealedAt is null))
        {
            return false;
        }

        // CreatedAt must be set
        if (CreatedAt == default)
        {
            return false;
        }

        // SealedAt must be after or equal to CreatedAt
        if (SealedAt.HasValue && SealedAt.Value < CreatedAt)
        {
            return false;
        }

        return true;
    }
}
