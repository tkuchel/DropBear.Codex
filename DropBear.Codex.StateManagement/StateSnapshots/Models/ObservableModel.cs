#region

using System.ComponentModel;
using R3;

#endregion

namespace DropBear.Codex.StateManagement.StateSnapshots.Models;

/// <summary>
///     Represents a model that notifies observers about state changes and supports property change notifications.
/// </summary>
/// <typeparam name="T">The type of the state managed by this model.</typeparam>
public class ObservableModel<T> : INotifyPropertyChanged
{
    private readonly object _lock = new();
    private T _state = default!;

    /// <summary>
    ///     Gets a subject that emits events whenever the state changes.
    /// </summary>
    public Subject<T> StateChanged { get; } = new();

    /// <summary>
    ///     Gets or sets the state. Notifies observers if the state changes.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the new state value is invalid.</exception>
    public T State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
        set
        {
            if (!IsValid(value))
            {
                throw new ArgumentException("Invalid state value.", nameof(value));
            }

            lock (_lock)
            {
                if (Equals(_state, value))
                {
                    return;
                }

                _state = value;
            }

            StateChanged.OnNext(value);
            OnPropertyChanged(nameof(State));
        }
    }

    /// <summary>
    ///     Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Determines whether the specified state value is valid.
    /// </summary>
    /// <param name="value">The state value to validate.</param>
    /// <returns><c>true</c> if the value is valid; otherwise, <c>false</c>.</returns>
    private static bool IsValid(T value)
    {
        return value is not null;
        // Add additional validation logic if needed
    }

    /// <summary>
    ///     Raises the <see cref="PropertyChanged" /> event.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
