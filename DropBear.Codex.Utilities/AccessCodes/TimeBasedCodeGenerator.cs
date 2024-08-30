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
///     This class can be used for various scenarios such as two-factor authentication,
///     temporary access codes, or any situation requiring time-sensitive security tokens.
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
    /// <param name="codeLength">The length of the generated code. Default is 4.</param>
    /// <param name="characterSet">The set of characters to use in the code. Default is digits 0-9.</param>
    /// <param name="validityDuration">The duration for which a generated code is valid. Default is 5 minutes.</param>
    /// <param name="gracePeriod">
    ///     An additional period after the validity duration during which the code is still accepted.
    ///     Default is 0.
    /// </param>
    /// <param name="secretKey">A secret key used in code generation. If null, a random key will be generated.</param>
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
    ///     Generates a time-based code using the specified date and time.
    /// </summary>
    /// <param name="dateTime">The date and time to use for code generation.</param>
    /// <returns>A string representing the generated code.</returns>
    /// <exception cref="TimeBasedCodeException">Thrown when code generation fails.</exception>
    public string GenerateCode(DateTime dateTime)
    {
        try
        {
            var message = dateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + _secretKey;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var hashString = Convert.ToBase64String(hash);

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
    /// <returns>True if the code is valid; otherwise, false.</returns>
    public bool ValidateCode(string code)
    {
        return ValidateCode(code, DateTime.UtcNow);
    }

    /// <summary>
    ///     Validates a code against the specified date and time.
    /// </summary>
    /// <param name="code">The code to validate.</param>
    /// <param name="dateTime">The date and time to validate against.</param>
    /// <returns>True if the code is valid; otherwise, false.</returns>
    /// <exception cref="TimeBasedCodeException">Thrown when code validation fails.</exception>
    public bool ValidateCode(string code, DateTime dateTime)
    {
        try
        {
            var validityStart = dateTime - _validityDuration - _gracePeriod;
            var validityEnd = dateTime;

            for (var checkTime = validityStart; checkTime <= validityEnd; checkTime = checkTime.AddMinutes(1))
            {
                if (!string.Equals(code, GenerateCode(checkTime), StringComparison.Ordinal))
                {
                    continue;
                }

                Logger.Information("Code validated successfully for date: {DateTime}", dateTime);
                return true;
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
    ///     Generates a code from a hash string using the specified length and character set.
    /// </summary>
    /// <param name="hash">The hash string to generate the code from.</param>
    /// <param name="length">The desired length of the code.</param>
    /// <param name="characterSet">The set of characters to use in the code.</param>
    /// <returns>A string representing the generated code.</returns>
    private static string GenerateCodeFromHash(string hash, int length, string characterSet)
    {
        var result = new StringBuilder(length);
        foreach (var c in hash.Where(characterSet.Contains))
        {
            result.Append(c);
            if (result.Length == length)
            {
                break;
            }
        }

        while (result.Length < length)
        {
            result.Append(characterSet[0]);
        }

        return result.ToString();
    }

    /// <summary>
    ///     Generates a cryptographically secure random key to use as the default secret key.
    /// </summary>
    /// <returns>A Base64-encoded string representing the generated key.</returns>
    private static string GenerateDefaultSecretKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }
}
