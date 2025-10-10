#region

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Factories;

/// <summary>
///     High-performance factory for creating ResultError instances.
///     Uses compiled expressions cached per type for near-native performance.
/// </summary>
public static class ResultErrorFactory<TError> where TError : ResultError
{
    private static readonly Func<string, TError> Factory = CreateFactory();

    private static Func<string, TError> CreateFactory()
    {
        var ctor = typeof(TError).GetConstructor(new[] { typeof(string) });
        if (ctor == null)
        {
            throw new InvalidOperationException(
                $"{typeof(TError).Name} must have a constructor accepting a string message parameter");
        }

        var messageParam = Expression.Parameter(typeof(string), "message");
        var newExpression = Expression.New(ctor, messageParam);
        var lambda = Expression.Lambda<Func<string, TError>>(newExpression, messageParam);

        return lambda.Compile();
    }

    /// <summary>
    ///     Creates an instance of TError with the specified message.
    ///     Performance is comparable to direct instantiation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TError Create(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return Factory(message);
    }
}
