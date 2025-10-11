namespace DropBear.Codex.Core.Results.Validations;

/// <summary>
///     Fluent builder for property-specific validation.
///     Optimized for .NET 9 with modern C# features.
/// </summary>
public sealed class PropertyValidationBuilder<TObject, TProperty>
{
    private readonly ValidationBuilder<TObject> _parentBuilder;
    private readonly string _propertyName;
    private readonly TProperty _propertyValue;

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
                ValidationError.InvalidLength(_propertyName, str, minLength));
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
    public ValidationBuilder<TObject> And() => _parentBuilder;
}
