#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Extensions;

/// <summary>
///     Extension methods for Unit type.
/// </summary>
public static class UnitExtensions
{
    /// <summary>
    ///     Converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Unit ToUnit<T>(this T _)
    {
        return Unit.Value;
    }

    /// <summary>
    ///     Asynchronously converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ToUnitAsync<T>(this Task<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }

    /// <summary>
    ///     Asynchronously converts any value to Unit, effectively discarding it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async ValueTask<Unit> ToUnitAsync<T>(this ValueTask<T> task)
    {
        await task.ConfigureAwait(false);
        return Unit.Value;
    }
}
