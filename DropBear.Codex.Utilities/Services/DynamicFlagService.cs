#region

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;
using DropBear.Codex.Utilities.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Services;

/// <summary>
///     Manages dynamic feature flags with thread-safe operations and logging.
///     Uses bit flags for efficient storage and retrieval.
/// </summary>
public class DynamicFlagService : IDynamicFlagService
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<DynamicFlagService>();

    // JSON serialization options
    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        WriteIndented = false, PropertyNameCaseInsensitive = true
    };

    // Cache for flag values to avoid recalculating
    private readonly ConcurrentDictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Maps flag names to bit positions
    private readonly ConcurrentDictionary<string, int> _flagMap = new(StringComparer.OrdinalIgnoreCase);

    // The internal bit flag state
    private int _flags;

    // The next available bit position (0-31)
    private int _nextFreeBit;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicFlagService" /> class.
    /// </summary>
    public DynamicFlagService()
    {
        _flags = 0;
        _nextFreeBit = 0;
        Logger.Debug("DynamicFlagService initialized");
    }

    /// <summary>
    ///     For backwards compatibility: Adds a new flag to the manager if it does not already exist.
    /// </summary>
    /// <param name="flagName">The name of the flag to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if the flag already exists or the limit is exceeded.</exception>
    public void AddFlagLegacy(string flagName)
    {
        var result = AddFlag(flagName);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }
    }

    /// <summary>
    ///     For backwards compatibility: Removes a flag from the manager.
    /// </summary>
    /// <param name="flagName">The name of the flag to remove.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public void RemoveFlagLegacy(string flagName)
    {
        var result = RemoveFlag(flagName);
        if (!result.IsSuccess)
        {
            throw new KeyNotFoundException(result.Error!.Message);
        }
    }

    /// <summary>
    ///     For backwards compatibility: Sets a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to set.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public void SetFlagLegacy(string flagName)
    {
        var result = SetFlag(flagName);
        if (!result.IsSuccess)
        {
            throw new KeyNotFoundException(result.Error!.Message);
        }
    }

    /// <summary>
    ///     For backwards compatibility: Clears a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to clear.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public void ClearFlagLegacy(string flagName)
    {
        var result = ClearFlag(flagName);
        if (!result.IsSuccess)
        {
            throw new KeyNotFoundException(result.Error!.Message);
        }
    }

    /// <summary>
    ///     For backwards compatibility: Toggles the state of a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to toggle.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public void ToggleFlagLegacy(string flagName)
    {
        var result = ToggleFlag(flagName);
        if (!result.IsSuccess)
        {
            throw new KeyNotFoundException(result.Error!.Message);
        }
    }

    /// <summary>
    ///     For backwards compatibility: Checks if a specific flag is set.
    /// </summary>
    /// <param name="flagName">The name of the flag to check.</param>
    /// <returns>True if the flag is set; otherwise, false.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public bool IsFlagSetLegacy(string flagName)
    {
        var result = IsFlagSet(flagName);
        if (!result.IsSuccess)
        {
            throw new KeyNotFoundException(result.Error!.Message);
        }

        return result.Value;
    }

    /// <summary>
    ///     For backwards compatibility: Serializes the current state of the flag manager.
    /// </summary>
    /// <returns>A JSON string representing the serialized state.</returns>
    public string SerializeLegacy()
    {
        var result = Serialize();
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        return result.Value!;
    }

    /// <summary>
    ///     For backwards compatibility: Deserializes the provided data into the flag manager.
    /// </summary>
    /// <param name="serializedData">The JSON string representing the serialized state.</param>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails.</exception>
    public void DeserializeLegacy(string serializedData)
    {
        var result = Deserialize(serializedData);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }
    }

    /// <summary>
    ///     For backwards compatibility: Returns a list of all flags and their current states.
    /// </summary>
    /// <returns>A dictionary with flag names as keys and their states as values.</returns>
    public IDictionary<string, bool> GetAllFlagsLegacy()
    {
        var result = GetAllFlags();
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        return result.Value!;
    }

    /// <summary>
    ///     Adds a new flag to the manager if it does not already exist.
    /// </summary>
    /// <param name="flagName">The name of the flag to add.</param>
    /// <returns>A Result indicating success or an error.</returns>
    public Result<Unit, FlagServiceError> AddFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError("Flag name cannot be null or empty."));
        }

        if (_flagMap.ContainsKey(flagName))
        {
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError($"Flag '{flagName}' already exists."));
        }

        if (_nextFreeBit >= 32)
        {
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError("Cannot add more flags. Maximum number of flags (32) has been reached."));
        }

        // Allocate a bit position to this flag
        _flagMap[flagName] = 1 << _nextFreeBit++;

        // Clear the cache to ensure consistency
        _cache.Clear();

        Logger.Information("Flag '{FlagName}' added successfully", flagName);
        return Result<Unit, FlagServiceError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Removes a flag from the manager.
    /// </summary>
    /// <param name="flagName">The name of the flag to remove.</param>
    /// <returns>A Result indicating success or an error.</returns>
    public Result<Unit, FlagServiceError> RemoveFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError("Flag name cannot be null or empty."));
        }

        if (_flagMap.TryRemove(flagName, out var bitValue))
        {
            // Clear the bit in the flags
            _flags &= ~bitValue;

            // Clear the cache
            _cache.Clear();

            Logger.Information("Flag '{FlagName}' removed successfully", flagName);
            return Result<Unit, FlagServiceError>.Success(Unit.Value);
        }

        return Result<Unit, FlagServiceError>.Failure(
            new FlagServiceError($"Flag '{flagName}' not found."));
    }

    /// <summary>
    ///     Sets a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to set.</param>
    /// <returns>A Result indicating success or an error.</returns>
    public Result<Unit, FlagServiceError> SetFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError("Flag name cannot be null or empty."));
        }

        if (_flagMap.TryGetValue(flagName, out var bitValue))
        {
            // Set the bit
            _flags |= bitValue;

            // Update cache
            _cache[flagName] = true;

            Logger.Information("Flag '{FlagName}' set", flagName);
            return Result<Unit, FlagServiceError>.Success(Unit.Value);
        }

        return Result<Unit, FlagServiceError>.Failure(
            new FlagServiceError($"Flag '{flagName}' not found."));
    }

    /// <summary>
    ///     Clears a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to clear.</param>
    /// <returns>A Result indicating success or an error.</returns>
    public Result<Unit, FlagServiceError> ClearFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError("Flag name cannot be null or empty."));
        }

        if (_flagMap.TryGetValue(flagName, out var bitValue))
        {
            // Clear the bit
            _flags &= ~bitValue;

            // Update cache
            _cache[flagName] = false;

            Logger.Information("Flag '{FlagName}' cleared", flagName);
            return Result<Unit, FlagServiceError>.Success(Unit.Value);
        }

        return Result<Unit, FlagServiceError>.Failure(
            new FlagServiceError($"Flag '{flagName}' not found."));
    }

    /// <summary>
    ///     Toggles the state of a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to toggle.</param>
    /// <returns>A Result indicating success or an error.</returns>
    public Result<Unit, FlagServiceError> ToggleFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError("Flag name cannot be null or empty."));
        }

        if (_flagMap.TryGetValue(flagName, out var bitValue))
        {
            // Toggle the bit using XOR
            _flags ^= bitValue;

            // Update cache with new state
            var newState = (_flags & bitValue) == bitValue;
            _cache[flagName] = newState;

            Logger.Information("Flag '{FlagName}' toggled to {State}", flagName, newState);
            return Result<Unit, FlagServiceError>.Success(Unit.Value);
        }

        return Result<Unit, FlagServiceError>.Failure(
            new FlagServiceError($"Flag '{flagName}' not found."));
    }

    /// <summary>
    ///     Checks if a specific flag is set.
    /// </summary>
    /// <param name="flagName">The name of the flag to check.</param>
    /// <returns>A Result containing a boolean indicating if the flag is set, or an error.</returns>
    public Result<bool, FlagServiceError> IsFlagSet(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return Result<bool, FlagServiceError>.Failure(
                new FlagServiceError("Flag name cannot be null or empty."));
        }

        // Check cache first for performance
        if (_cache.TryGetValue(flagName, out var isSet))
        {
            return Result<bool, FlagServiceError>.Success(isSet);
        }

        if (!_flagMap.TryGetValue(flagName, out var bitValue))
        {
            return Result<bool, FlagServiceError>.Failure(
                new FlagServiceError($"Flag '{flagName}' not found."));
        }

        isSet = (_flags & bitValue) == bitValue;

        // Cache the result
        _cache[flagName] = isSet;

        return Result<bool, FlagServiceError>.Success(isSet);
    }

    /// <summary>
    ///     Serializes the current state of the flag manager.
    /// </summary>
    /// <returns>A Result containing the serialized state as a JSON string, or an error.</returns>
    public Result<string, FlagServiceError> Serialize()
    {
        try
        {
            var data = new SerializationData
            {
                Flags = _flagMap.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
                CurrentState = _flags,
                NextFreeBit = _nextFreeBit
            };

            var serialized = JsonSerializer.Serialize(data, SerializationOptions);

            Logger.Information("Flag data serialized successfully");
            return Result<string, FlagServiceError>.Success(serialized);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error serializing flag data");
            return Result<string, FlagServiceError>.Failure(
                new FlagServiceError($"Failed to serialize flag data: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Deserializes the provided data into the flag manager.
    /// </summary>
    /// <param name="serializedData">The JSON string representing the serialized state.</param>
    /// <returns>A Result indicating success or an error.</returns>
    public Result<Unit, FlagServiceError> Deserialize(string serializedData)
    {
        if (string.IsNullOrWhiteSpace(serializedData))
        {
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError("Serialized data cannot be null or empty."));
        }

        try
        {
            var data = JsonSerializer.Deserialize<SerializationData>(serializedData, SerializationOptions);

            if (data is null)
            {
                return Result<Unit, FlagServiceError>.Failure(
                    new FlagServiceError("Flag data deserialization failed: Invalid format."));
            }

            // Clear existing flags
            _flagMap.Clear();

            // Load the new flags
            foreach (var (key, value) in data.Flags)
            {
                _flagMap[key] = value;
            }

            _flags = data.CurrentState;
            _nextFreeBit = data.NextFreeBit;

            // Clear cache
            _cache.Clear();

            Logger.Information("Flag data deserialized successfully");
            return Result<Unit, FlagServiceError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deserializing flag data");
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError($"Failed to deserialize flag data: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Returns a list of all flags and their current states.
    /// </summary>
    /// <returns>A Result containing a dictionary with flag names as keys and their states as values, or an error.</returns>
    public Result<IDictionary<string, bool>, FlagServiceError> GetAllFlags()
    {
        try
        {
            var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var flagName in _flagMap.Keys)
            {
                var result = IsFlagSet(flagName);
                if (result.IsSuccess)
                {
                    flags[flagName] = result.Value;
                }
                else
                {
                    // This shouldn't happen since we're iterating over existing flags,
                    // but handle it gracefully anyway
                    Logger.Warning("Error getting state for flag '{FlagName}': {Error}",
                        flagName, result.Error?.Message);
                }
            }

            return Result<IDictionary<string, bool>, FlagServiceError>.Success(flags);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting all flags");
            return Result<IDictionary<string, bool>, FlagServiceError>.Failure(
                new FlagServiceError($"Failed to get all flags: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Clears all flags and their states.
    /// </summary>
    /// <returns>A Result indicating success or an error.</returns>
    public Result<Unit, FlagServiceError> ClearAllFlags()
    {
        try
        {
            _flagMap.Clear();
            _flags = 0;
            _nextFreeBit = 0;
            _cache.Clear();

            Logger.Information("All flags cleared");
            return Result<Unit, FlagServiceError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error clearing all flags");
            return Result<Unit, FlagServiceError>.Failure(
                new FlagServiceError($"Failed to clear all flags: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Checks if a flag with the specified name exists.
    /// </summary>
    /// <param name="flagName">The name of the flag to check.</param>
    /// <returns>A Result containing a boolean indicating if the flag exists, or an error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<bool, FlagServiceError> FlagExists(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
        {
            return Result<bool, FlagServiceError>.Failure(
                new FlagServiceError("Flag name cannot be null or empty."));
        }

        return Result<bool, FlagServiceError>.Success(_flagMap.ContainsKey(flagName));
    }

    /// <summary>
    ///     Gets the number of flags currently registered.
    /// </summary>
    /// <returns>A Result containing the number of flags, or an error.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Result<int, FlagServiceError> GetFlagCount()
    {
        return Result<int, FlagServiceError>.Success(_flagMap.Count);
    }

    /// <summary>
    ///     Helper class for serialization and deserialization of the flag manager state.
    /// </summary>
    private sealed class SerializationData
    {
        public Dictionary<string, int> Flags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int CurrentState { get; set; }
        public int NextFreeBit { get; set; }
    }
}
