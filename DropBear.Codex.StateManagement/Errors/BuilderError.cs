#region

using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.StateManagement.Errors;

/// <summary>
///     Represents errors that can occur during builder operations.
///     Provides strongly-typed error information for the Result pattern.
/// </summary>
public sealed record BuilderError : ResultError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BuilderError" /> class.
    /// </summary>
    /// <param name="message">The error message describing the builder operation failure.</param>
    public BuilderError(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Gets or sets the name of the parameter that caused the validation error, if applicable.
    /// </summary>
    public string? ParameterName { get; init; }

    /// <summary>
    ///     Creates a new <see cref="BuilderError" /> from an exception.
    /// </summary>
    /// <param name="ex">The exception to create the error from.</param>
    /// <returns>A new <see cref="BuilderError" /> instance.</returns>
    public static BuilderError FromException(Exception ex)
    {
        var error = new BuilderError(ex.Message);

        // Use WithException to set the exception properly
        var errorWithException = (BuilderError)error.WithException(ex);

        // Add additional metadata
        var result = (BuilderError)errorWithException
            .WithMetadata("ExceptionType", ex.GetType().Name);

        // If it's an ArgumentException, capture the parameter name
        if (ex is ArgumentException argEx && argEx.ParamName is not null)
        {
            result = result with { ParameterName = argEx.ParamName };
        }

        return result;
    }

    /// <summary>
    ///     Creates a new <see cref="BuilderError" /> with the specified context.
    /// </summary>
    /// <param name="context">The context where the error occurred.</param>
    /// <returns>A new <see cref="BuilderError" /> with updated context.</returns>
    public BuilderError WithContext(string context)
    {
        return (BuilderError)WithMetadata("Context", context);
    }

    /// <summary>
    ///     Creates a new <see cref="BuilderError" /> with the specified parameter name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter that caused the error.</param>
    /// <returns>A new <see cref="BuilderError" /> with updated parameter name.</returns>
    public BuilderError WithParameterName(string parameterName)
    {
        return this with { ParameterName = parameterName };
    }

    #region Factory Methods

    /// <summary>
    ///     Creates an error for invalid interval values.
    /// </summary>
    public static BuilderError InvalidInterval(string parameterName, string reason) =>
        new($"Invalid interval: {reason}")
        {
            ParameterName = parameterName
        };

    /// <summary>
    ///     Creates an error for invalid retention time values.
    /// </summary>
    public static BuilderError InvalidRetentionTime(string reason) =>
        new($"Invalid retention time: {reason}")
        {
            ParameterName = "retentionTime"
        };

    /// <summary>
    ///     Creates an error for when a builder has already been built.
    /// </summary>
    public static BuilderError AlreadyBuilt() =>
        new("Builder has already been built and cannot be modified.");

    /// <summary>
    ///     Creates an error for when trying to configure a builder after it has been built.
    /// </summary>
    public static BuilderError CannotConfigureAfterBuild() =>
        new("Cannot configure builder after it has been built.");

    /// <summary>
    ///     Creates an error for invalid builder configuration.
    /// </summary>
    public static BuilderError InvalidConfiguration(string reason) =>
        new($"Invalid builder configuration: {reason}");

    /// <summary>
    ///     Creates an error for required parameters.
    /// </summary>
    public static BuilderError RequiredParameter(string parameterName) =>
        new($"Required parameter '{parameterName}' was not provided.")
        {
            ParameterName = parameterName
        };

    #endregion
}
