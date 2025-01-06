#region

using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Logging;
using Serilog;

#endregion

namespace DropBear.Codex.Core.Results.Base
{
    /// <summary>
    ///     An abstract base class for all Result types, providing
    ///     common functionality such as <see cref="State"/>, <see cref="Exception"/>,
    ///     and a set of protected methods for safe execution.
    /// </summary>
    public abstract class ResultBase : IResult
    {
        /// <summary>
        ///     A shared Serilog logger for this class and its subclasses.
        /// </summary>
        private protected static readonly ILogger Logger = LoggerFactory.Logger.ForContext<ResultBase>();

        private readonly IReadOnlyCollection<Exception> _exceptions;

        #region Constructor

        /// <summary>
        ///     Initializes a new instance of <see cref="ResultBase"/>.
        /// </summary>
        /// <param name="state">The <see cref="ResultState"/> (e.g., Success, Failure, etc.).</param>
        /// <param name="exception">
        ///     An optional <see cref="Exception"/> if the result failed due to an error condition.
        /// </param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="state"/> is not a valid <see cref="ResultState"/>.</exception>
        protected ResultBase(ResultState state, Exception? exception = null)
        {
            ValidateState(state);

            State = state;
            Exception = exception;
            _exceptions = CreateExceptionCollection(exception);
        }

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets the <see cref="ResultState"/> of this result (e.g. Success, Failure, etc.).
        /// </summary>
        public ResultState State { get; }

        /// <summary>
        ///     Indicates whether the result represents success or partial success.
        /// </summary>
        public bool IsSuccess => State is ResultState.Success or ResultState.PartialSuccess;

        /// <summary>
        ///     Indicates whether the result represents a complete success (i.e., <see cref="ResultState.Success"/>).
        /// </summary>
        public bool IsCompleteSuccess => State == ResultState.Success;

        /// <summary>
        ///     Indicates whether the result represents a failure (i.e., not <see cref="IsSuccess"/>).
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        ///     Gets the primary exception associated with this result, if any.
        ///     May be <c>null</c> if no exception was set.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        ///     Gets all exceptions associated with this result. For an <see cref="AggregateException"/>,
        ///     this collection may contain multiple exceptions.
        /// </summary>
        public virtual IReadOnlyCollection<Exception> Exceptions => _exceptions;

        #endregion

        #region Protected Methods

        /// <summary>
        ///     Executes the specified <paramref name="action"/> safely,
        ///     catching and logging any <see cref="Exception"/> that occurs.
        /// </summary>
        /// <param name="action">A synchronous <see cref="Action"/> to execute.</param>
        protected static void SafeExecute(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception during synchronous execution");
            }
        }

        /// <summary>
        ///     Executes an asynchronous <paramref name="action"/> safely,
        ///     catching and logging any <see cref="Exception"/> that occurs.
        /// </summary>
        /// <param name="action">An async function that accepts a <see cref="CancellationToken"/>.</param>
        /// <param name="cancellationToken">
        ///     A token used to cancel the operation, if desired.
        /// </param>
        protected static async ValueTask SafeExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error(ex, "Exception during asynchronous execution");
            }
        }

        /// <summary>
        ///     Executes an asynchronous <paramref name="action"/> safely with a specified <paramref name="timeout"/>.
        ///     If the operation does not complete in time, the <see cref="CancellationToken"/> is canceled.
        /// </summary>
        /// <param name="action">An async function that accepts a <see cref="CancellationToken"/>.</param>
        /// <param name="timeout">The <see cref="TimeSpan"/> duration to allow before cancellation.</param>
        /// <param name="cancellationToken">A token used to cancel the operation early, if desired.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown if <paramref name="timeout"/> is less than or equal to zero.
        /// </exception>
        protected static async ValueTask SafeExecuteWithTimeoutAsync(
            Func<CancellationToken, Task> action,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            try
            {
                await action(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Error(ex, "Exception during asynchronous execution with timeout");
            }
        }

        #endregion

        #region Private Methods

        private static void ValidateState(ResultState state)
        {
            if (!Enum.IsDefined(state))
            {
                throw new ArgumentException($"Invalid result state: {state}", nameof(state));
            }
        }

        private static IReadOnlyCollection<Exception> CreateExceptionCollection(Exception? exception)
        {
            if (exception is null)
            {
                return Array.Empty<Exception>();
            }

            if (exception is AggregateException aggregateException)
            {
                return aggregateException.InnerExceptions.ToList().AsReadOnly();
            }

            return new[] { exception }.ToList().AsReadOnly();
        }

        #endregion
    }
}
