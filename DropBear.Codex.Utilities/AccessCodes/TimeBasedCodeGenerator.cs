#region

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Core.Results.Base;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.AccessCodes;

/// <summary>
///     Generates and validates short-lived verification codes derived from time windows.
/// </summary>
/// <remarks>
///     This helper does not enforce one-time use, replay protection, or attempt throttling and should not be
///     presented as a standalone multi-factor authentication solution without additional controls.
/// </remarks>
public class TimeBasedCodeGenerator
{
    // Constants to avoid allocations from string formatting
    private const string DateTimeFormat = "yyyyMMddHHmmss";
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<TimeBasedCodeGenerator>();

    private readonly string _characterSet;
    private readonly int _codeLength;
    private readonly TimeSpan _gracePeriod;
    private readonly string _secretKey;
    private readonly TimeSpan _timeStep;

    // Cache for validated codes to improve performance
    private readonly ConcurrentDictionary<string, (DateTime ExpiryTime, bool IsValid)> _validationCache = new(StringComparer.Ordinal);
    private readonly TimeSpan _validityDuration;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TimeBasedCodeGenerator" /> class.
    /// </summary>
    /// <param name="codeLength">Length of the generated code (default: 6).</param>
    /// <param name="characterSet">
    ///     Characters used in the generated code (default: digits '0'-'9').
    /// </param>
    /// <param name="validityDuration">
    ///     How long (time span) a generated code remains valid (default: 30 seconds).
    /// </param>
    /// <param name="gracePeriod">
    ///     Additional grace period after <paramref name="validityDuration" /> during which the code is still accepted
    ///     (default: 0).
    /// </param>
    /// <param name="secretKey">
    ///     A secret key used for hashing. If <c>null</c>, a random Base64-encoded 32-byte key is generated.
    /// </param>
    /// <param name="timeStep">
    ///     The time step used to normalize generated codes into windows (default: 30 seconds).
    /// </param>
    public TimeBasedCodeGenerator(
        int codeLength = 6,
        string characterSet = "0123456789",
        TimeSpan? validityDuration = null,
        TimeSpan? gracePeriod = null,
        string? secretKey = null,
        TimeSpan? timeStep = null)
    {
        _codeLength = codeLength;
        _characterSet = characterSet;
        _validityDuration = validityDuration ?? TimeSpan.FromSeconds(30);
        _gracePeriod = gracePeriod ?? TimeSpan.Zero;
        _secretKey = secretKey ?? GenerateDefaultSecretKey();
        _timeStep = timeStep ?? TimeSpan.FromSeconds(30);

        if (_codeLength < 4)
        {
            throw new ArgumentOutOfRangeException(nameof(codeLength), "Code length must be at least 4 characters.");
        }

        if (string.IsNullOrEmpty(_characterSet))
        {
            throw new ArgumentException("Character set cannot be null or empty.", nameof(characterSet));
        }

        if (_validityDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(validityDuration), "Validity duration must be positive.");
        }

        if (_timeStep <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeStep), "Time step must be positive.");
        }

        Logger.Information(
            "TimeBasedCodeGenerator initialized with code length: {CodeLength}, character set: {CharacterSet}, validity duration: {ValidityDuration}, grace period: {GracePeriod}, time step: {TimeStep}",
            codeLength, characterSet, _validityDuration, _gracePeriod, _timeStep);
    }

    /// <summary>
    ///     Generates a time-based code using the current UTC time.
    /// </summary>
    /// <returns>A Result containing the generated code or error information.</returns>
    public Result<string, TimeBasedCodeError> GenerateCode()
    {
        return GenerateCode(DateTime.UtcNow);
    }

    /// <summary>
    ///     Generates a time-based code using a specified <paramref name="dateTime" />.
    /// </summary>
    /// <param name="dateTime">The date and time to use for code generation.</param>
    /// <returns>A Result containing the generated code or error information.</returns>
    public Result<string, TimeBasedCodeError> GenerateCode(DateTime dateTime)
    {
        var normalizedDateTime = NormalizeToTimeStep(dateTime.ToUniversalTime());

        // We incorporate the date/time and the secret key into an HMAC-SHA256 hash
        var message = normalizedDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture) + _secretKey;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var hashString = Convert.ToBase64String(hash);

        // Convert the hash into a code of length _codeLength using characters from _characterSet
        var code = GenerateCodeFromHash(hashString, _codeLength, _characterSet);

        Logger.Debug("Generated code for normalized date window: {DateTime}", normalizedDateTime);
        return Result<string, TimeBasedCodeError>.Success(code);
    }

    /// <summary>
    ///     Validates a code against the current UTC time.
    /// </summary>
    /// <param name="code">The code to validate.</param>
    /// <returns>A Result indicating whether the code is valid.</returns>
    public Result<bool, TimeBasedCodeError> ValidateCode(string code)
    {
        return ValidateCode(code, DateTime.UtcNow);
    }

    /// <summary>
    ///     Validates a code against a specified <paramref name="dateTime" />.
    ///     The code is considered valid if it matches any generated code within
    ///     [ <paramref name="dateTime" /> - (<see cref="_validityDuration" /> + <see cref="_gracePeriod" /> ),
    ///     <paramref name="dateTime" /> ].
    /// </summary>
    /// <param name="code">The code to validate.</param>
    /// <param name="dateTime">The date/time against which to validate.</param>
    /// <returns>A Result indicating whether the code is valid.</returns>
    public Result<bool, TimeBasedCodeError> ValidateCode(string code, DateTime dateTime)
    {
        if (string.IsNullOrEmpty(code))
        {
            return Result<bool, TimeBasedCodeError>.Failure(
                new TimeBasedCodeError("Code cannot be null or empty"));
        }

        // Check cache first to avoid unnecessary code generation for repeated validations
        if (_validationCache.TryGetValue(code, out var cacheEntry))
        {
            if (cacheEntry.ExpiryTime >= dateTime)
            {
                return Result<bool, TimeBasedCodeError>.Success(cacheEntry.IsValid);
            }

            // Expired entry, remove it
            _validationCache.TryRemove(code, out _);
        }

        var normalizedDateTime = NormalizeToTimeStep(dateTime.ToUniversalTime());
        // The earliest time we consider a code valid
        var validityStart = normalizedDateTime - _validityDuration - _gracePeriod;
        // The latest time it can be valid
        var validityEnd = normalizedDateTime;

        // We check each configured time step from validityStart up to validityEnd for a match
        for (var checkTime = NormalizeToTimeStep(validityStart); checkTime <= validityEnd; checkTime = checkTime.Add(_timeStep))
        {
            var generatedCodeResult = GenerateCode(checkTime);
            if (!generatedCodeResult.IsSuccess)
            {
                continue;
            }

            if (string.Equals(code, generatedCodeResult.Value, StringComparison.Ordinal))
            {
                Logger.Information("Code validated successfully for date: {DateTime}", dateTime);

                // Cache the successful validation result
                var expiryTime = dateTime + _validityDuration;
                _validationCache.TryAdd(code, (expiryTime, true));

                return Result<bool, TimeBasedCodeError>.Success(true);
            }
        }

        // Cache the failed validation to prevent brute force attacks
        var failedExpiryTime = dateTime + TimeSpan.FromMinutes(1); // Short cache time for failures
        _validationCache.TryAdd(code, (failedExpiryTime, false));

        Logger.Information("Code validation failed for date: {DateTime}", dateTime);
        return Result<bool, TimeBasedCodeError>.Success(false);
    }

    /// <summary>
    ///     Generates a short code from a hash string by filtering it against a <paramref name="characterSet" />
    ///     and truncating (or padding) to <paramref name="length" />.
    /// </summary>
    /// <param name="hash">A Base64-encoded hash string.</param>
    /// <param name="length">Desired code length.</param>
    /// <param name="characterSet">Set of characters used to form the code.</param>
    /// <returns>A string code of length <paramref name="length" /> made from <paramref name="characterSet" />.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateCodeFromHash(ReadOnlySpan<char> hash, int length, ReadOnlySpan<char> characterSet)
    {
        // Use stack allocation for small codes to avoid heap allocations
        var resultBuffer = length <= 128
            ? stackalloc char[length]
            : new char[length];

        var resultPos = 0;

        // Filter the hash so only characters from characterSet appear in the code
        foreach (var c in hash)
        {
            if (characterSet.Contains(c))
            {
                resultBuffer[resultPos++] = c;
                if (resultPos == length)
                {
                    break;
                }
            }
        }

        // If the result is shorter than needed, pad with the first character in the character set
        while (resultPos < length)
        {
            resultBuffer[resultPos++] = characterSet[0];
        }

        return new string(resultBuffer);
    }

    /// <summary>
    ///     Generates a cryptographically secure random key as the default secret key (32 bytes, Base64-encoded).
    /// </summary>
    private static string GenerateDefaultSecretKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    /// <summary>
    ///     Clears the validation cache.
    /// </summary>
    /// <remarks>
    ///     Can be useful if you need to force revalidation of codes or when
    ///     system time changes significantly.
    /// </remarks>
    public void ClearValidationCache()
    {
        _validationCache.Clear();
        Logger.Information("Validation cache has been cleared");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DateTime NormalizeToTimeStep(DateTime dateTimeUtc)
    {
        var ticks = dateTimeUtc.Ticks - (dateTimeUtc.Ticks % _timeStep.Ticks);
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}
