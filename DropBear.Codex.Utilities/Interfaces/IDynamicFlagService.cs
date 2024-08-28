namespace DropBear.Codex.Utilities.Interfaces;

/// <summary>
///     Interface for managing dynamic feature flags with thread-safe operations and logging.
/// </summary>
public interface IDynamicFlagService
{
    /// <summary>
    ///     Adds a new flag to the manager if it does not already exist.
    /// </summary>
    /// <param name="flagName">The name of the flag to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if the flag already exists or the limit is exceeded.</exception>
    void AddFlag(string flagName);

    /// <summary>
    ///     Removes a flag from the manager.
    /// </summary>
    /// <param name="flagName">The name of the flag to remove.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    void RemoveFlag(string flagName);

    /// <summary>
    ///     Sets a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to set.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    void SetFlag(string flagName);

    /// <summary>
    ///     Clears a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to clear.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    void ClearFlag(string flagName);

    /// <summary>
    ///     Toggles the state of a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to toggle.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    void ToggleFlag(string flagName);

    /// <summary>
    ///     Checks if a specific flag is set.
    /// </summary>
    /// <param name="flagName">The name of the flag to check.</param>
    /// <returns>True if the flag is set; otherwise, false.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    bool IsFlagSet(string flagName);

    /// <summary>
    ///     Serializes the current state of the flag manager.
    /// </summary>
    /// <returns>A JSON string representing the serialized state.</returns>
    string Serialize();

    /// <summary>
    ///     Deserializes the provided data into the flag manager.
    /// </summary>
    /// <param name="serializedData">The JSON string representing the serialized state.</param>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails.</exception>
    void Deserialize(string serializedData);

    /// <summary>
    ///     Returns a list of all flags and their current states.
    /// </summary>
    /// <returns>A dictionary with flag names as keys and their states as values.</returns>
    IDictionary<string, bool> GetAllFlags();
}
