#region

using System.Collections.Concurrent;
using System.Text.Json;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.Interfaces;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.Services;

/// <summary>
///     Manages dynamic feature flags with thread-safe operations and logging.
/// </summary>
public class DynamicFlagService : IDynamicFlagService
{
    private static ILogger? _logger;
    private readonly ConcurrentDictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _flagMap = new(StringComparer.OrdinalIgnoreCase);
    private int _flags;
    private int _nextFreeBit;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DynamicFlagService" /> class.
    /// </summary>
    public DynamicFlagService()
    {
        _flags = 0;
        _nextFreeBit = 0;
        _logger = LoggerFactory.Logger.ForContext<DynamicFlagService>();
    }

    /// <summary>
    ///     Adds a new flag to the manager if it does not already exist.
    /// </summary>
    /// <param name="flagName">The name of the flag to add.</param>
    /// <exception cref="InvalidOperationException">Thrown if the flag already exists or the limit is exceeded.</exception>
    public void AddFlag(string flagName)
    {
        if (_flagMap.ContainsKey(flagName) || _nextFreeBit >= 32)
        {
            var ex = new InvalidOperationException(
                "Cannot add flag. Either the flag already exists or the maximum number of flags (32) has been reached.");

            _logger?.Error(ex,
                "Cannot add flag. Either the flag already exists or the maximum number of flags (32) has been reached.");
            throw ex;
        }

        _flagMap[flagName] = 1 << _nextFreeBit++;
        _cache.Clear(); // Reset the cache to ensure consistency.
        _logger?.Information($"Flag '{flagName}' added successfully.");
    }

    /// <summary>
    ///     Removes a flag from the manager.
    /// </summary>
    /// <param name="flagName">The name of the flag to remove.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public void RemoveFlag(string flagName)
    {
        if (_flagMap.TryRemove(flagName, out var bitValue))
        {
            _flags &= ~bitValue;
            _cache.Clear();
            _logger?.Information($"Flag '{flagName}' removed successfully.");
        }
        else
        {
            var ex = new KeyNotFoundException($"Flag '{flagName}' not found.");
            _logger?.Error(ex, $"Cannot remove flag '{flagName}'. Flag not found.");
            throw ex;
        }
    }

    /// <summary>
    ///     Sets a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to set.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public void SetFlag(string flagName)
    {
        if (_flagMap.TryGetValue(flagName, out var bitValue))
        {
            _flags |= bitValue;
            _cache[flagName] = true;
            _logger?.Information($"Flag '{flagName}' set.");
        }
        else
        {
            var ex = new KeyNotFoundException($"Flag '{flagName}' not found.");
            _logger?.Error(ex, $"Cannot set flag '{flagName}'. Flag not found.");
            throw ex;
        }
    }

    /// <summary>
    ///     Clears a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to clear.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public void ClearFlag(string flagName)
    {
        if (_flagMap.TryGetValue(flagName, out var bitValue))
        {
            _flags &= ~bitValue;
            _cache[flagName] = false;
            _logger?.Information($"Flag '{flagName}' cleared.");
        }
        else
        {
            var ex = new KeyNotFoundException($"Flag '{flagName}' not found.");
            _logger?.Error(ex, $"Cannot clear flag '{flagName}'. Flag not found.");
            throw ex;
        }
    }

    /// <summary>
    ///     Toggles the state of a specific flag.
    /// </summary>
    /// <param name="flagName">The name of the flag to toggle.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public void ToggleFlag(string flagName)
    {
        if (_flagMap.TryGetValue(flagName, out var bitValue))
        {
            _flags ^= bitValue;
            _cache[flagName] = (_flags & bitValue) == bitValue;
            _logger?.Information($"Flag '{flagName}' toggled.");
        }
        else
        {
            var ex = new KeyNotFoundException($"Flag '{flagName}' not found.");
            _logger?.Error(ex, $"Cannot toggle flag '{flagName}'. Flag not found.");
            throw ex;
        }
    }

    /// <summary>
    ///     Checks if a specific flag is set.
    /// </summary>
    /// <param name="flagName">The name of the flag to check.</param>
    /// <returns>True if the flag is set; otherwise, false.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the flag does not exist.</exception>
    public bool IsFlagSet(string flagName)
    {
        if (_cache.TryGetValue(flagName, out var isSet))
        {
            return isSet;
        }

        if (!_flagMap.TryGetValue(flagName, out var bitValue))
        {
            var ex = new KeyNotFoundException($"Flag '{flagName}' not found.");
            _logger?.Error(ex, $"Flag {flagName} not found.");
            throw ex;
        }

        isSet = (_flags & bitValue) == bitValue;
        _cache[flagName] = isSet; // Cache the result.

        return isSet;
    }

    /// <summary>
    ///     Serializes the current state of the flag manager.
    /// </summary>
    /// <returns>A JSON string representing the serialized state.</returns>
    public string Serialize()
    {
        var data = new SerializationData
        {
            Flags = _flagMap.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            CurrentState = _flags,
            NextFreeBit = _nextFreeBit
        };

        _logger?.Information("Flag data serialized successfully.");
        return JsonSerializer.Serialize(data);
    }

    /// <summary>
    ///     Deserializes the provided data into the flag manager.
    /// </summary>
    /// <param name="serializedData">The JSON string representing the serialized state.</param>
    /// <exception cref="InvalidOperationException">Thrown if deserialization fails.</exception>
    public void Deserialize(string serializedData)
    {
        var data = JsonSerializer.Deserialize<SerializationData>(serializedData);
        if (data is null)
        {
            var ex = new InvalidOperationException("Flag data deserialization failed.");
            _logger?.Error(ex, "Flag data deserialization failed.");
            throw ex;
        }

        _flagMap.Clear();
        foreach (var (key, value) in data!.Flags)
        {
            _flagMap[key] = value;
        }

        _flags = data.CurrentState;
        _nextFreeBit = data.NextFreeBit;
        _cache.Clear();
        _logger?.Information("Flag data deserialized successfully.");
    }

    /// <summary>
    ///     Returns a list of all flags and their current states.
    /// </summary>
    /// <returns>A dictionary with flag names as keys and their states as values.</returns>
    public Dictionary<string, bool> GetAllFlags()
    {
        return _flagMap.Keys.ToDictionary(flag => flag, IsFlagSet, StringComparer.OrdinalIgnoreCase);
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
