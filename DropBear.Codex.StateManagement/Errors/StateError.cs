#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.StateManagement.Errors;

/// <summary>
///     Represents errors that can occur during state machine operations.
///     Provides strongly-typed error information for the Result pattern.
/// </summary>
public sealed record StateError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StateError" /> class.
    /// </summary>
    /// <param name="message">The error message describing the state operation failure.</param>
    public StateError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Creates a new <see cref="StateError" /> from an exception.
    /// </summary>
    /// <param name="ex">The exception to create the error from.</param>
    /// <returns>A new <see cref="StateError" /> instance.</returns>
    public static StateError FromException(Exception ex)
    {
        var error = new StateError(ex.Message);

        // Use WithException to set the exception properly
        var errorWithException = (StateError)error.WithException(ex);

        // Add additional metadata
        return (StateError)errorWithException
            .WithMetadata("ExceptionType", ex.GetType().Name);
    }

    /// <summary>
    ///     Creates a new <see cref="StateError" /> with the specified context.
    /// </summary>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new <see cref="StateError" /> with updated context.</returns>
    public StateError WithContext(string context)
    {
        return (StateError)WithMetadata("Context", context);
    }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for invalid state transitions.
    /// </summary>
    public static StateError InvalidTransition(string fromState, string toState, string trigger) =>
        new($"Invalid transition from '{fromState}' to '{toState}' via trigger '{trigger}'.");

    /// <summary>
    ///     Creates an error for when a state is not found.
    /// </summary>
    public static StateError StateNotFound(string stateName) =>
        new($"State '{stateName}' not found.");

    /// <summary>
    ///     Creates an error for when a trigger is not valid.
    /// </summary>
    public static StateError InvalidTrigger(string trigger) =>
        new($"Trigger '{trigger}' is not valid in the current state.");

    /// <summary>
    ///     Creates an error for when a guard condition fails.
    /// </summary>
    public static StateError GuardConditionFailed(string guardDescription) =>
        new($"Guard condition failed: {guardDescription}");

    #endregion
}
