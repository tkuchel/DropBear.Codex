#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Enums;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Extension methods for <see cref="ResultState" /> enum.
///     Optimized for .NET 9 with aggressive inlining.
/// </summary>
public static class ResultStateExtensions
{
    /// <summary>
    ///     Determines whether the specified state represents a successful outcome.
    /// </summary>
    /// <param name="state">The result state to check.</param>
    /// <returns>
    ///     <c>true</c> if the state is <see cref="ResultState.Success" /> or <see cref="ResultState.PartialSuccess" />;
    ///     otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSuccessState(this ResultState state) =>
        state is ResultState.Success or ResultState.PartialSuccess;

    /// <summary>
    ///     Determines whether the specified state represents a failure outcome.
    /// </summary>
    /// <param name="state">The result state to check.</param>
    /// <returns>
    ///     <c>true</c> if the state is <see cref="ResultState.Failure" />; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFailureState(this ResultState state) => state is ResultState.Failure;

    /// <summary>
    ///     Determines whether the specified state represents a cancelled outcome.
    /// </summary>
    /// <param name="state">The result state to check.</param>
    /// <returns>
    ///     <c>true</c> if the state is <see cref="ResultState.Cancelled" />; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCancelledState(this ResultState state) => state is ResultState.Cancelled;

    /// <summary>
    ///     Determines whether the specified state represents a pending outcome.
    /// </summary>
    /// <param name="state">The result state to check.</param>
    /// <returns>
    ///     <c>true</c> if the state is <see cref="ResultState.Pending" />; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPendingState(this ResultState state) => state is ResultState.Pending;

    /// <summary>
    ///     Determines whether the specified state represents a warning outcome.
    /// </summary>
    /// <param name="state">The result state to check.</param>
    /// <returns>
    ///     <c>true</c> if the state is <see cref="ResultState.Warning" />; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWarningState(this ResultState state) => state is ResultState.Warning;

    /// <summary>
    ///     Determines whether the specified state represents a no-operation outcome.
    /// </summary>
    /// <param name="state">The result state to check.</param>
    /// <returns>
    ///     <c>true</c> if the state is <see cref="ResultState.NoOp" />; otherwise, <c>false</c>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNoOpState(this ResultState state) => state is ResultState.NoOp;

    /// <summary>
    ///     Gets a human-readable description of the result state.
    /// </summary>
    /// <param name="state">The result state.</param>
    /// <returns>A string description of the state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToDescription(this ResultState state)
    {
        return state switch
        {
            ResultState.Success => "Operation completed successfully",
            ResultState.Failure => "Operation failed",
            ResultState.Pending => "Operation is pending",
            ResultState.Cancelled => "Operation was cancelled",
            ResultState.Warning => "Operation completed with warnings",
            ResultState.PartialSuccess => "Operation partially succeeded",
            ResultState.NoOp => "No operation was performed",
            _ => "Unknown state"
        };
    }
}
