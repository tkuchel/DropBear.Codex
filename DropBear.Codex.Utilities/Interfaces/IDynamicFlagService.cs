#region

using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Interfaces;

/// <summary>
///     Interface for a service that manages dynamic feature flags.
/// </summary>
public interface IDynamicFlagService
{
    /// <summary>
    ///     Adds a new flag to the manager if it does not already exist.
    /// </summary>
    /// <param name="flagName">The name of the flag to add.</param>
    /// <returns>A Result indicating success or an error.</returns>
    Result<Unit, FlagServiceError> AddFlag(string flagName);

    /// <summary>
    ///     Removes a flag from the manager.
    /// </summary>
    /// <param name="flagName">The name of the flag to remove.</param>
    /// <returns>A Result indicating success or an error.</returns>
    Result<Unit, FlagServiceError> RemoveFlag(string flagName);

    /// <summary>
    ///     Sets a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to set.</param>
    /// <returns>A Result indicating success or an error.</returns>
    Result<Unit, FlagServiceError> SetFlag(string flagName);

    /// <summary>
    ///     Clears a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to clear.</param>
    /// <returns>A Result indicating success or an error.</returns>
    Result<Unit, FlagServiceError> ClearFlag(string flagName);

    /// <summary>
    ///     Toggles the state of a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to toggle.</param>
    /// <returns>A Result indicating success or an error.</returns>
    Result<Unit, FlagServiceError> ToggleFlag(string flagName);

    /// <summary>
    ///     Checks if a specific flag is set.
    /// </summary>
    /// <param name="flagName">The name of the flag to check.</param>
    /// <returns>A Result containing a boolean indicating if the flag is set, or an error.</returns>
    Result<bool, FlagServiceError> IsFlagSet(string flagName);

    /// <summary>
    ///     Serializes the current state of the flag manager.
    /// </summary>
    /// <returns>A Result containing the serialized state as a JSON string, or an error.</returns>
    Result<string, FlagServiceError> Serialize();

    /// <summary>
    ///     Deserializes the provided data into the flag manager.
    /// </summary>
    /// <param name="serializedData">The JSON string representing the serialized state.</param>
    /// <returns>A Result indicating success or an error.</returns>
    Result<Unit, FlagServiceError> Deserialize(string serializedData);

    /// <summary>
    ///     Returns a list of all flags and their current states.
    /// </summary>
    /// <returns>A Result containing a dictionary with flag names as keys and their states as values, or an error.</returns>
    Result<IDictionary<string, bool>, FlagServiceError> GetAllFlags();

    /// <summary>
    ///     Clears all flags and their states.
    /// </summary>
    /// <returns>A Result indicating success or an error.</returns>
    Result<Unit, FlagServiceError> ClearAllFlags();

    /// <summary>
    ///     Checks if a flag with the specified name exists.
    /// </summary>
    /// <param name="flagName">The name of the flag to check.</param>
    /// <returns>A Result containing a boolean indicating if the flag exists, or an error.</returns>
    Result<bool, FlagServiceError> FlagExists(string flagName);

    /// <summary>
    ///     Gets the number of flags currently registered.
    /// </summary>
    /// <returns>A Result containing the number of flags, or an error.</returns>
    Result<int, FlagServiceError> GetFlagCount();

    #region Backwards Compatibility Methods

    /// <summary>
    ///     For backwards compatibility: Adds a new flag to the manager if it does not already exist.
    /// </summary>
    /// <param name="flagName">The name of the flag to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if the flag already exists or the limit is exceeded.</exception>
    void AddFlagLegacy(string flagName);

    /// <summary>
    ///     For backwards compatibility: Removes a flag from the manager.
    /// </summary>
    /// <param name="flagName">The name of the flag to remove.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    void RemoveFlagLegacy(string flagName);

    /// <summary>
    ///     For backwards compatibility: Sets a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to set.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    void SetFlagLegacy(string flagName);

    /// <summary>
    ///     For backwards compatibility: Clears a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to clear.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    void ClearFlagLegacy(string flagName);

    /// <summary>
    ///     For backwards compatibility: Toggles the state of a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to toggle.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    void ToggleFlagLegacy(string flagName);

    /// <summary>
    ///     For backwards compatibility: Checks if a specific flag is set.
    /// </summary>
    /// <param name="flagName">The name of the flag to check.</param>
    /// <returns>True if the flag is set; otherwise, false.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    bool IsFlagSetLegacy(string flagName);

    /// <summary>
    ///     For backwards compatibility: Serializes the current state of the flag manager.
    /// </summary>
    /// <returns>A JSON string representing the serialized state.</returns>
    string SerializeLegacy();

    /// <summary>
    ///     For backwards compatibility: Deserializes the provided data into the flag manager.
    /// </summary>
    /// <param name="serializedData">The JSON string representing the serialized state.</param>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails.</exception>
    void DeserializeLegacy(string serializedData);

    /// <summary>
    ///     For backwards compatibility: Returns a list of all flags and their current states.
    /// </summary>
    /// <returns>A dictionary with flag names as keys and their states as values.</returns>
    IDictionary<string, bool> GetAllFlagsLegacy();

    #endregion
}
