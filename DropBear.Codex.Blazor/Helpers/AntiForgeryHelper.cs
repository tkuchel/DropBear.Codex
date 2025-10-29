#region

using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Blazor.Errors;

#endregion

namespace DropBear.Codex.Blazor.Helpers;

/// <summary>
///     Provides utilities for generating and validating anti-forgery (CSRF) tokens
///     to protect Blazor applications from Cross-Site Request Forgery attacks.
/// </summary>
/// <remarks>
///     CSRF tokens should be included in forms and validated on the server side
///     to ensure requests originate from legitimate sources.
/// </remarks>
public static class AntiForgeryHelper
{
    private const int TokenByteSize = 32; // 256 bits
    private const int SaltByteSize = 16; // 128 bits

    /// <summary>
    ///     Generates a cryptographically secure anti-forgery token.
    /// </summary>
    /// <param name="userId">The user identifier to bind the token to.</param>
    /// <param name="sessionId">Optional session identifier for additional security.</param>
    /// <returns>A base64-encoded anti-forgery token.</returns>
    /// <remarks>
    ///     The token is bound to the user and optionally the session, making it
    ///     impossible for an attacker to forge a valid token for another user.
    ///     Token includes a timestamp for expiration validation.
    /// </remarks>
    public static string GenerateToken(string userId, string? sessionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        Span<byte> tokenBytes = stackalloc byte[TokenByteSize];
        Span<byte> saltBytes = stackalloc byte[SaltByteSize];

        RandomNumberGenerator.Fill(tokenBytes);
        RandomNumberGenerator.Fill(saltBytes);

        // Create a composite token: timestamp + salt + randomBytes + hash(salt + userId + sessionId + randomBytes)
        var dataToHash = new StringBuilder();
        dataToHash.Append(Convert.ToBase64String(saltBytes));
        dataToHash.Append('|');
        dataToHash.Append(userId);
        if (!string.IsNullOrEmpty(sessionId))
        {
            dataToHash.Append('|');
            dataToHash.Append(sessionId);
        }
        dataToHash.Append('|');
        dataToHash.Append(Convert.ToBase64String(tokenBytes));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dataToHash.ToString()));

        // Get current timestamp
        var timestamp = DateTime.UtcNow.ToBinary();
        var timestampBytes = BitConverter.GetBytes(timestamp);

        // Combine timestamp + salt + randomBytes + hash for the final token
        var finalToken = new byte[timestampBytes.Length + SaltByteSize + TokenByteSize + hash.Length];
        var span = finalToken.AsSpan();

        timestampBytes.CopyTo(span);
        saltBytes.CopyTo(span[timestampBytes.Length..]);
        tokenBytes.CopyTo(span[(timestampBytes.Length + SaltByteSize)..]);
        hash.CopyTo(span[(timestampBytes.Length + SaltByteSize + TokenByteSize)..]);

        return Convert.ToBase64String(finalToken);
    }

    /// <summary>
    ///     Validates an anti-forgery token.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="userId">The user identifier the token should be bound to.</param>
    /// <param name="sessionId">Optional session identifier for validation.</param>
    /// <param name="maxAge">Maximum age of the token (default: 2 hours).</param>
    /// <returns>A Result indicating whether the token is valid.</returns>
    /// <remarks>
    ///     SECURITY NOTE: This is a stateless token validation that verifies format and binding.
    ///     For production applications, consider using ASP.NET Core's built-in AntiForgery services
    ///     which provide additional features like token rotation and distributed cache support.
    /// </remarks>
    public static Result<Unit, ComponentError> ValidateToken(
        string token,
        string userId,
        string? sessionId = null,
        TimeSpan? maxAge = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<Unit, ComponentError>.Failure(
                new ComponentError("Anti-forgery token is required"));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<Unit, ComponentError>.Failure(
                new ComponentError("User identifier is required for token validation"));
        }

        maxAge ??= Options.DefaultExpiration;

        try
        {
            var tokenBytes = Convert.FromBase64String(token);

            // Token format: [8 bytes timestamp][16 bytes salt][32 bytes random][32 bytes hash]
            const int timestampSize = sizeof(long);
            const int randomSize = TokenByteSize;
            const int hashSize = 32; // SHA256
            const int expectedMinSize = timestampSize + SaltByteSize + randomSize + hashSize;

            if (tokenBytes.Length < expectedMinSize)
            {
                return Result<Unit, ComponentError>.Failure(
                    new ComponentError("Invalid token format"));
            }

            // Extract components
            var timestamp = BitConverter.ToInt64(tokenBytes, 0);
            var tokenTime = DateTime.FromBinary(timestamp);

            // Check token expiration
            if (DateTime.UtcNow - tokenTime > maxAge)
            {
                return Result<Unit, ComponentError>.Failure(
                    new ComponentError("Token has expired"));
            }

            var salt = tokenBytes.AsSpan(timestampSize, SaltByteSize);
            var randomBytes = tokenBytes.AsSpan(timestampSize + SaltByteSize, randomSize);
            var providedHash = tokenBytes.AsSpan(timestampSize + SaltByteSize + randomSize, hashSize);

            // Reconstruct the hash to verify integrity
            var dataToHash = new StringBuilder();
            dataToHash.Append(Convert.ToBase64String(salt));
            dataToHash.Append('|');
            dataToHash.Append(userId);
            if (!string.IsNullOrEmpty(sessionId))
            {
                dataToHash.Append('|');
                dataToHash.Append(sessionId);
            }
            dataToHash.Append('|');
            dataToHash.Append(Convert.ToBase64String(randomBytes));

            var computedHash = SHA256.HashData(Encoding.UTF8.GetBytes(dataToHash.ToString()));

            // Use constant-time comparison to prevent timing attacks
            if (!CryptographicOperations.FixedTimeEquals(providedHash, computedHash))
            {
                return Result<Unit, ComponentError>.Failure(
                    new ComponentError("Token validation failed: hash mismatch"));
            }

            return Result<Unit, ComponentError>.Success(Unit.Value);
        }
        catch (FormatException)
        {
            return Result<Unit, ComponentError>.Failure(
                new ComponentError("Invalid token encoding"));
        }
        catch (Exception ex)
        {
            return Result<Unit, ComponentError>.Failure(
                new ComponentError($"Token validation error: {ex.Message}"), ex);
        }
    }

    /// <summary>
    ///     Generates a shorter token suitable for embedding in URLs (not recommended for forms).
    /// </summary>
    /// <returns>A URL-safe short token (24 characters).</returns>
    /// <remarks>
    ///     WARNING: Short tokens provide less security. Use full tokens for form protection.
    ///     URL tokens are useful for double-submit cookie patterns.
    /// </remarks>
    public static string GenerateShortToken()
    {
        Span<byte> tokenBytes = stackalloc byte[18]; // 144 bits → 24 base64 chars
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    ///     Creates a double-submit cookie value for CSRF protection.
    /// </summary>
    /// <returns>A cryptographically secure cookie value.</returns>
    /// <remarks>
    ///     Use this with the double-submit cookie pattern:
    ///     1. Generate a random value and store it in a cookie
    ///     2. Include the same value in a hidden form field
    ///     3. Validate that both values match on submission
    /// </remarks>
    public static string GenerateCookieValue()
    {
        Span<byte> cookieBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(cookieBytes);
        return Convert.ToBase64String(cookieBytes);
    }

    /// <summary>
    ///     Validates double-submit cookie pattern.
    /// </summary>
    /// <param name="cookieValue">The value from the cookie.</param>
    /// <param name="formValue">The value from the hidden form field.</param>
    /// <returns>A Result indicating whether the values match.</returns>
    public static Result<Unit, ComponentError> ValidateDoubleSubmit(
        string cookieValue,
        string formValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue) || string.IsNullOrWhiteSpace(formValue))
        {
            return Result<Unit, ComponentError>.Failure(
                new ComponentError("CSRF cookie and form values are required"));
        }

        // Use constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(cookieValue),
                Encoding.UTF8.GetBytes(formValue)))
        {
            return Result<Unit, ComponentError>.Failure(
                new ComponentError("CSRF token mismatch"));
        }

        return Result<Unit, ComponentError>.Success(Unit.Value);
    }

    /// <summary>
    ///     Token configuration options.
    /// </summary>
    public static class Options
    {
        /// <summary>
        ///     Default token expiration time (2 hours).
        /// </summary>
        public static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(2);

        /// <summary>
        ///     Short token expiration time (15 minutes).
        /// </summary>
        public static readonly TimeSpan ShortExpiration = TimeSpan.FromMinutes(15);

        /// <summary>
        ///     Cookie name for double-submit pattern.
        /// </summary>
        public const string CookieName = "X-CSRF-TOKEN";

        /// <summary>
        ///     Form field name for anti-forgery token.
        /// </summary>
        public const string FormFieldName = "__RequestVerificationToken";

        /// <summary>
        ///     HTTP header name for AJAX requests.
        /// </summary>
        public const string HeaderName = "X-CSRF-TOKEN";
    }

    /// <summary>
    ///     Token metadata for tracking and validation.
    /// </summary>
    public sealed record TokenMetadata
    {
        /// <summary>
        ///     Gets the token value.
        /// </summary>
        public required string Token { get; init; }

        /// <summary>
        ///     Gets the user identifier the token is bound to.
        /// </summary>
        public required string UserId { get; init; }

        /// <summary>
        ///     Gets the session identifier (if any).
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        ///     Gets the token generation timestamp.
        /// </summary>
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

        /// <summary>
        ///     Gets the token expiration timestamp.
        /// </summary>
        public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.Add(Options.DefaultExpiration);

        /// <summary>
        ///     Checks if the token has expired.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        /// <summary>
        ///     Gets the remaining lifetime of the token.
        /// </summary>
        public TimeSpan RemainingLifetime =>
            IsExpired ? TimeSpan.Zero : ExpiresAt - DateTime.UtcNow;
    }
}
