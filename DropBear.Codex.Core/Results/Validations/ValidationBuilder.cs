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
    private readonly T _value;
    private readonly List<ValidationError> _errors;
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
    public static ValidationBuilder<T> For(T value)
    {
        return new ValidationBuilder<T>(value, initialCapacity: 8);
    }

    /// <summary>
    ///     Creates a validation builder with a pre-allocated capacity.
    ///     Use this when you know approximately how many errors to expect.
    /// </summary>
    public static ValidationBuilder<T> CreatePooled(T value, int initialCapacity = 16)
    {
        return new ValidationBuilder<T>(value, initialCapacity);
    }

    /// <summary>
    ///     Configures the builder to stop on the first validation error.
    /// </summary>
    public ValidationBuilder<T> StopOnFirstError()
    {
        _stopOnFirstError = true;
        return this;
    }

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
            return this;

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
            return this;

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
            return this;

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
            return this;

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
            return this;

        if (!await predicateAsync(_value).ConfigureAwait(false))
        {
            _errors.Add(errorFactory(_value));
        }

        return this;
    }

    #endregion

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
            return this;

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
                    break;
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
            return this;

        var collection = collectionSelector(_value);
        var index = 0;

        foreach (var item in collection)
        {
            var result = itemValidator(item, index);
            if (!result.IsValid)
            {
                _errors.AddRange(result.Errors);

                if (_stopOnFirstError)
                    break;
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
            return this;

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
            return this;

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

    internal void AddError(ValidationError error)
    {
        _errors.Add(error);
    }

    internal void AddErrors(IEnumerable<ValidationError> errors)
    {
        _errors.AddRange(errors);
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
}

/// <summary>
///     Fluent builder for property-specific validation.
///     Optimized for .NET 9 with modern C# features.
/// </summary>
public sealed class PropertyValidationBuilder<TObject, TProperty>
{
    private readonly ValidationBuilder<TObject> _parentBuilder;
    private readonly TProperty _propertyValue;
    private readonly string _propertyName;

    internal PropertyValidationBuilder(
        ValidationBuilder<TObject> parentBuilder,
        TProperty propertyValue,
        string propertyName)
    {
        _parentBuilder = parentBuilder;
        _propertyValue = propertyValue;
        _propertyName = propertyName;
    }

    /// <summary>
    ///     Validates that the property is not null.
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> NotNull()
    {
        if (_propertyValue is null)
        {
            _parentBuilder.AddError(ValidationError.Required(_propertyName));
        }

        return this;
    }

    /// <summary>
    ///     Validates that the property is not null or empty (for strings).
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> NotNullOrEmpty()
    {
        if (_propertyValue is string str && string.IsNullOrEmpty(str))
        {
            _parentBuilder.AddError(ValidationError.Required(_propertyName));
        }
        else if (_propertyValue is null)
        {
            _parentBuilder.AddError(ValidationError.Required(_propertyName));
        }

        return this;
    }

    /// <summary>
    ///     Validates that the property is not null or whitespace (for strings).
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> NotNullOrWhiteSpace()
    {
        if (_propertyValue is string str && string.IsNullOrWhiteSpace(str))
        {
            _parentBuilder.AddError(ValidationError.Required(_propertyName));
        }
        else if (_propertyValue is null)
        {
            _parentBuilder.AddError(ValidationError.Required(_propertyName));
        }

        return this;
    }

    /// <summary>
    ///     Validates that a string has a minimum length.
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> MinLength(int minLength)
    {
        if (_propertyValue is string str && str.Length < minLength)
        {
            _parentBuilder.AddError(
                ValidationError.InvalidLength(_propertyName, str, minLength, null));
        }

        return this;
    }

    /// <summary>
    ///     Validates that a string has a maximum length.
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> MaxLength(int maxLength)
    {
        if (_propertyValue is string str && str.Length > maxLength)
        {
            _parentBuilder.AddError(
                ValidationError.InvalidLength(_propertyName, str, null, maxLength));
        }

        return this;
    }

    /// <summary>
    ///     Validates that a value is within a range.
    ///     Requires TProperty to implement IComparable.
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> InRange<TComp>(TComp min, TComp max)
        where TComp : IComparable<TComp>
    {
        if (_propertyValue is TComp comparable)
        {
            if (comparable.CompareTo(min) < 0 || comparable.CompareTo(max) > 0)
            {
                _parentBuilder.AddError(
                    ValidationError.OutOfRange(_propertyName, comparable, min, max));
            }
        }

        return this;
    }

    /// <summary>
    ///     Validates that a value is greater than a minimum.
    ///     Requires TProperty to implement IComparable.
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> GreaterThan<TComp>(TComp minimum)
        where TComp : IComparable<TComp>
    {
        if (_propertyValue is TComp comparable && comparable.CompareTo(minimum) <= 0)
        {
            _parentBuilder.AddError(
                ValidationError.ForProperty(
                    _propertyName,
                    $"{_propertyName} must be greater than {minimum}",
                    _propertyValue));
        }

        return this;
    }

    /// <summary>
    ///     Validates that a value is less than a maximum.
    ///     Requires TProperty to implement IComparable.
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> LessThan<TComp>(TComp maximum)
        where TComp : IComparable<TComp>
    {
        if (_propertyValue is TComp comparable && comparable.CompareTo(maximum) >= 0)
        {
            _parentBuilder.AddError(
                ValidationError.ForProperty(
                    _propertyName,
                    $"{_propertyName} must be less than {maximum}",
                    _propertyValue));
        }

        return this;
    }

    /// <summary>
    ///     Validates using a custom predicate.
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> Must(
        Func<TProperty, bool> predicate,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        if (!predicate(_propertyValue))
        {
            _parentBuilder.AddError(
                ValidationError.ForProperty(_propertyName, errorMessage, _propertyValue));
        }

        return this;
    }

    /// <summary>
    ///     Validates using a custom predicate with error factory.
    /// </summary>
    public PropertyValidationBuilder<TObject, TProperty> Must(
        Func<TProperty, bool> predicate,
        Func<TProperty, ValidationError> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);

        if (!predicate(_propertyValue))
        {
            _parentBuilder.AddError(errorFactory(_propertyValue));
        }

        return this;
    }

    /// <summary>
    ///     Validates using an async predicate.
    /// </summary>
    public async ValueTask<PropertyValidationBuilder<TObject, TProperty>> MustAsync(
        Func<TProperty, ValueTask<bool>> predicateAsync,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(predicateAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        if (!await predicateAsync(_propertyValue).ConfigureAwait(false))
        {
            _parentBuilder.AddError(
                ValidationError.ForProperty(_propertyName, errorMessage, _propertyValue));
        }

        return this;
    }

    /// <summary>
    ///     Returns to the parent builder for further validation.
    /// </summary>
    public ValidationBuilder<TObject> And()
    {
        return _parentBuilder;
    }
}
