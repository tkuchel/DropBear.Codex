#region

using System.Runtime.CompilerServices;
using DropBear.Codex.Core.Results.Base;

#endregion

namespace DropBear.Codex.Core.Results.Validations;

/// <summary>
///     Fluent builder for creating complex validation chains.
///     Optimized for .NET 9 with modern patterns and performance.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
public sealed class ValidationBuilder<T>
{
    private readonly List<ValidationError> _errors;
    private readonly T _value;
    private bool _stopOnFirstError;

    // Single private constructor
    private ValidationBuilder(T value, int initialCapacity)
    {
        _value = value;
        _errors = new List<ValidationError>(initialCapacity);
    }

    /// <summary>
    ///     Creates a new validation builder for the specified value.
    /// </summary>
    public static ValidationBuilder<T> For(T value) => new(value, 8);

    /// <summary>
    ///     Creates a validation builder with a pre-allocated capacity.
    ///     Use this when you know approximately how many errors to expect.
    /// </summary>
    public static ValidationBuilder<T> CreatePooled(T value, int initialCapacity = 16) => new(value, initialCapacity);

    /// <summary>
    ///     Configures the builder to stop on the first validation error.
    /// </summary>
    public ValidationBuilder<T> StopOnFirstError()
    {
        _stopOnFirstError = true;
        return this;
    }

    #region Property Validation

    /// <summary>
    ///     Validates a property of the value.
    ///     Returns a property-specific builder for fluent chaining.
    /// </summary>
    public PropertyValidationBuilder<T, TProp> Property<TProp>(
        Func<T, TProp> propertySelector,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var propertyValue = propertySelector(_value);
        return new PropertyValidationBuilder<T, TProp>(
            this,
            propertyValue,
            propertyName);
    }

    #endregion

    #region Performance-Optimized Validation

    /// <summary>
    ///     Validates multiple values efficiently using pooled resources.
    ///     Optimized for batch validation scenarios.
    ///     This is a static method that works with any type, not limited to T.
    /// </summary>
    public static async ValueTask<IReadOnlyList<ValidationResult>> ValidateManyAsync<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, ValidationBuilder<TItem>, ValueTask> configure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(configure);

        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return [];
        }

        var results = new ValidationResult[itemList.Count];

        // Process items sequentially to avoid span issues
        for (var i = 0; i < itemList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Create a builder for TItem, not T
            var builder = ValidationBuilder<TItem>.CreatePooled(itemList[i]);
            await configure(itemList[i], builder).ConfigureAwait(false);
            results[i] = builder.Build();
        }

        return results;
    }

    #endregion

    #region Validation Methods

    /// <summary>
    ///     Validates that a condition is true.
    ///     Uses aggressive inlining for optimal performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValidationBuilder<T> Must(
        Func<T, bool> predicate,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        if (!predicate(_value))
        {
            _errors.Add(new ValidationError(errorMessage));
        }

        return this;
    }

    /// <summary>
    ///     Validates that a condition is true with a custom error.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValidationBuilder<T> Must(
        Func<T, bool> predicate,
        ValidationError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        if (!predicate(_value))
        {
            _errors.Add(error);
        }

        return this;
    }

    /// <summary>
    ///     Validates that a condition is true with error factory.
    /// </summary>
    public ValidationBuilder<T> Must(
        Func<T, bool> predicate,
        Func<T, ValidationError> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        if (!predicate(_value))
        {
            _errors.Add(errorFactory(_value));
        }

        return this;
    }

    /// <summary>
    ///     Async validation with predicate.
    /// </summary>
    public async ValueTask<ValidationBuilder<T>> MustAsync(
        Func<T, ValueTask<bool>> predicateAsync,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(predicateAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        if (!await predicateAsync(_value).ConfigureAwait(false))
        {
            _errors.Add(new ValidationError(errorMessage));
        }

        return this;
    }

    /// <summary>
    ///     Async validation with error factory.
    /// </summary>
    public async ValueTask<ValidationBuilder<T>> MustAsync(
        Func<T, ValueTask<bool>> predicateAsync,
        Func<T, ValidationError> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicateAsync);
        ArgumentNullException.ThrowIfNull(errorFactory);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        if (!await predicateAsync(_value).ConfigureAwait(false))
        {
            _errors.Add(errorFactory(_value));
        }

        return this;
    }

    #endregion

    #region Collection Validation

    /// <summary>
    ///     Validates each item in a collection property.
    ///     Uses modern foreach patterns for better performance.
    /// </summary>
    public ValidationBuilder<T> ForEach<TItem>(
        Func<T, IEnumerable<TItem>> collectionSelector,
        Action<ValidationBuilder<TItem>, TItem, int> itemValidation)
    {
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(itemValidation);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        var collection = collectionSelector(_value);
        var index = 0;

        foreach (var item in collection)
        {
            var itemBuilder = ValidationBuilder<TItem>.For(item);
            itemValidation(itemBuilder, item, index);

            var itemResult = itemBuilder.Build();
            if (!itemResult.IsValid)
            {
                _errors.AddRange(itemResult.Errors);

                if (_stopOnFirstError)
                {
                    break;
                }
            }

            index++;
        }

        return this;
    }

    /// <summary>
    ///     Validates each item in a collection with simple validation.
    /// </summary>
    public ValidationBuilder<T> ForEach<TItem>(
        Func<T, IEnumerable<TItem>> collectionSelector,
        Func<TItem, int, ValidationResult> itemValidator)
    {
        ArgumentNullException.ThrowIfNull(collectionSelector);
        ArgumentNullException.ThrowIfNull(itemValidator);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        var collection = collectionSelector(_value);
        var index = 0;

        foreach (var item in collection)
        {
            var result = itemValidator(item, index);
            if (!result.IsValid)
            {
                _errors.AddRange(result.Errors);

                if (_stopOnFirstError)
                {
                    break;
                }
            }

            index++;
        }

        return this;
    }

    #endregion

    #region Custom Validation

    /// <summary>
    ///     Adds a custom validation result.
    /// </summary>
    public ValidationBuilder<T> AddValidation(ValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult);

        if (!validationResult.IsValid)
        {
            _errors.AddRange(validationResult.Errors);
        }

        return this;
    }

    /// <summary>
    ///     Executes a custom validation function.
    /// </summary>
    public ValidationBuilder<T> Validate(Func<T, ValidationResult> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        var result = validator(_value);
        if (!result.IsValid)
        {
            _errors.AddRange(result.Errors);
        }

        return this;
    }

    /// <summary>
    ///     Executes an async custom validation function.
    /// </summary>
    public async ValueTask<ValidationBuilder<T>> ValidateAsync(
        Func<T, ValueTask<ValidationResult>> validatorAsync)
    {
        ArgumentNullException.ThrowIfNull(validatorAsync);

        if (_stopOnFirstError && _errors.Count > 0)
        {
            return this;
        }

        var result = await validatorAsync(_value).ConfigureAwait(false);
        if (!result.IsValid)
        {
            _errors.AddRange(result.Errors);
        }

        return this;
    }

    #endregion

    #region Build

    /// <summary>
    ///     Builds the final validation result.
    /// </summary>
    public ValidationResult Build()
    {
        return _errors.Count == 0
            ? ValidationResult.Success
            : ValidationResult.Failed(_errors);
    }

    /// <summary>
    ///     Builds a Result{T, ValidationError} with the validated value.
    /// </summary>
    public Result<T, ValidationError> BuildWithValue()
    {
        return _errors.Count == 0
            ? Result<T, ValidationError>.Success(_value)
            : Result<T, ValidationError>.Failure(ValidationResult.Failed(_errors).Error!);
    }

    #endregion

    #region Internal

    internal void AddError(ValidationError error) => _errors.Add(error);

    internal void AddErrors(IEnumerable<ValidationError> errors) => _errors.AddRange(errors);

    #endregion
}

