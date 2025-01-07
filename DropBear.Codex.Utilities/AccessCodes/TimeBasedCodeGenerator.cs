#region

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Logging;
using DropBear.Codex.Utilities.Exceptions;
using Serilog;

#endregion

namespace DropBear.Codex.Utilities.AccessCodes;

/// <summary>
///     Generates and validates time-based security codes.
///     Useful for scenarios like two-factor authentication or other time-sensitive token generation.
/// </summary>
public class TimeBasedCodeGenerator
{
    private static readonly ILogger Logger = LoggerFactory.Logger.ForContext<TimeBasedCodeGenerator>();

    private readonly string _characterSet;
    private readonly int _codeLength;
    private readonly TimeSpan _gracePeriod;
    private readonly string _secretKey;
    private readonly TimeSpan _validityDuration;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TimeBasedCodeGenerator" /> class.
    /// </summary>
    /// <param name="codeLength">Length of the generated code (default: 4).</param>
    /// <param name="characterSet">
    ///     Characters used in the generated code (default: digits '0'-'9').
    /// </param>
    /// <param name="validityDuration">
    ///     How long (time span) a generated code remains valid (default: 5 minutes).
    /// </param>
    /// <param name="gracePeriod">
    ///     Additional grace period after <paramref name="validityDuration" /> during which the code is still accepted
    ///     (default: 0).
    /// </param>
    /// <param name="secretKey">
    ///     A secret key used for hashing. If <c>null</c>, a random Base64-encoded 32-byte key is generated.
    /// </param>
    public TimeBasedCodeGenerator(
        int codeLength = 4,
        string characterSet = "0123456789",
        TimeSpan? validityDuration = null,
        TimeSpan? gracePeriod = null,
        string? secretKey = null)
    {
        _codeLength = codeLength;
        _characterSet = characterSet;
        _validityDuration = validityDuration ?? TimeSpan.FromMinutes(5);
        _gracePeriod = gracePeriod ?? TimeSpan.Zero;
        _secretKey = secretKey ?? GenerateDefaultSecretKey();

        Logger.Information(
            "TimeBasedCodeGenerator initialized with code length: {CodeLength}, character set: {CharacterSet}, validity duration: {ValidityDuration}, grace period: {GracePeriod}",
            codeLength, characterSet, _validityDuration, _gracePeriod);
    }

    /// <summary>
    ///     Generates a time-based code using the current UTC time.
    /// </summary>
    /// <returns>A string representing the generated code.</returns>
    public string GenerateCode()
    {
        return GenerateCode(DateTime.UtcNow);
    }

    /// <summary>
    ///     Generates a time-based code using a specified <paramref name="dateTime" />.
    /// </summary>
    /// <param name="dateTime">The date and time to use for code generation.</param>
    /// <returns>A string representing the generated code.</returns>
    /// <exception cref="TimeBasedCodeException">Thrown if code generation fails.</exception>
    public string GenerateCode(DateTime dateTime)
    {
        try
        {
            // We incorporate the date/time and the secret key into an HMAC-SHA256 hash
            var message = dateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + _secretKey;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var hashString = Convert.ToBase64String(hash);

            // Convert the hash into a code of length _codeLength using characters from _characterSet
            return GenerateCodeFromHash(hashString, _codeLength, _characterSet);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error generating code for date: {DateTime}", dateTime);
            throw new TimeBasedCodeException("Failed to generate code", ex);
        }
    }

    /// <summary>
    ///     Validates a code against the current UTC time.
    /// </summary>
    /// <param name="code">The code to validate.</param>
    /// <returns><c>true</c> if the code is valid within the current time window; otherwise <c>false</c>.</returns>
    public bool ValidateCode(string code)
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
    /// <returns><c>true</c> if the code is valid within that window; otherwise <c>false</c>.</returns>
    /// <exception cref="TimeBasedCodeException">Thrown if code validation fails unexpectedly.</exception>
    public bool ValidateCode(string code, DateTime dateTime)
    {
        try
        {
            // The earliest time we consider a code valid
            var validityStart = dateTime - _validityDuration - _gracePeriod;
            // The latest time it can be valid
            var validityEnd = dateTime;

            // We check every minute from validityStart up to validityEnd for a match
            for (var checkTime = validityStart; checkTime <= validityEnd; checkTime = checkTime.AddMinutes(1))
            {
                var generatedCode = GenerateCode(checkTime);
                if (string.Equals(code, generatedCode, StringComparison.Ordinal))
                {
                    Logger.Information("Code validated successfully for date: {DateTime}", dateTime);
                    return true;
                }
            }

            Logger.Information("Code validation failed for date: {DateTime}", dateTime);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error validating code: {Code} for date: {DateTime}", code, dateTime);
            throw new TimeBasedCodeException("Failed to validate code", ex);
        }
    }

    /// <summary>
    ///     Generates a short code from a hash string by filtering it against a <paramref name="characterSet" />
    ///     and truncating (or padding) to <paramref name="length" />.
    /// </summary>
    /// <param name="hash">A Base64-encoded hash string.</param>
    /// <param name="length">Desired code length.</param>
    /// <param name="characterSet">Set of characters used to form the code.</param>
    /// <returns>A string code of length <paramref name="length" /> made from <paramref name="characterSet" />.</returns>
    private static string GenerateCodeFromHash(string hash, int length, string characterSet)
    {
        var result = new StringBuilder(length);

        // Filter the hash so only characters from 'characterSet' appear in the code
        foreach (var c in hash)
        {
            if (characterSet.Contains(c))
            {
                result.Append(c);
                if (result.Length == length)
                {
                    break;
                }
            }
        }

        // If the result is shorter than needed, pad with the first character in the character set
        while (result.Length < length)
        {
            result.Append(characterSet[0]);
        }

        return result.ToString();
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
}
