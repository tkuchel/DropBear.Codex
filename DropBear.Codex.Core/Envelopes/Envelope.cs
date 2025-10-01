#region

using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Envelopes.Serializers;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     Represents an immutable envelope that encapsulates a payload with metadata.
///     Thread-safe by design through immutability.
///     Optimized for .NET 9 with modern patterns.
/// </summary>
/// <typeparam name="T">The type of payload contained in the envelope.</typeparam>
[DebuggerDisplay("Sealed = {IsSealed}, HasPayload = {_payload != null}")]
public sealed class Envelope<T>
{
    private readonly T _payload;
    private readonly FrozenDictionary<string, object> _headers;
    private readonly bool _isSealed;
    private readonly DateTime _createdAt;
    private readonly DateTime? _sealedAt;
    private readonly string? _signature;
    private readonly byte[]? _encryptedPayload;
    private readonly IResultTelemetry _telemetry;

    #region Constructors

    /// <summary>
    ///     Internal constructor used by builder.
    /// </summary>
    internal Envelope(
        T payload,
        FrozenDictionary<string, object> headers,
        bool isSealed,
        DateTime createdAt,
        DateTime? sealedAt,
        string? signature,
        byte[]? encryptedPayload,
        IResultTelemetry telemetry)
    {
        _payload = payload;
        _headers = headers;
        _isSealed = isSealed;
        _createdAt = createdAt;
        _sealedAt = sealedAt;
        _signature = signature;
        _encryptedPayload = encryptedPayload;
        _telemetry = telemetry;

        _telemetry.TrackResultCreated(
            isSealed ? ResultState.Success : ResultState.Pending,
            typeof(Envelope<T>));
    }

    /// <summary>
    ///     Creates a new envelope with the specified payload.
    ///     Use EnvelopeBuilder for more control.
    /// </summary>
    public Envelope(T payload, IResultTelemetry? telemetry = null)
        : this(
            payload,
            FrozenDictionary<string, object>.Empty,
            isSealed: false,
            createdAt: DateTime.UtcNow,
            sealedAt: null,
            signature: null,
            encryptedPayload: null,
            telemetry ?? new DefaultResultTelemetry())
    {
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the payload. Throws if envelope is not sealed or is encrypted.
    /// </summary>
    public T Payload
    {
        get
        {
            if (!_isSealed)
            {
                throw new InvalidOperationException("Envelope must be sealed before accessing payload");
            }

            if (_encryptedPayload != null)
            {
                throw new InvalidOperationException("Payload is encrypted. Decrypt first.");
            }

            return _payload;
        }
    }

    /// <summary>
    ///     Gets the headers as a read-only collection.
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers => _headers;

    /// <summary>
    ///     Gets a value indicating whether the envelope is sealed.
    /// </summary>
    public bool IsSealed => _isSealed;

    /// <summary>
    ///     Gets a value indicating whether the payload is encrypted.
    /// </summary>
    public bool IsEncrypted => _encryptedPayload != null;

    /// <summary>
    ///     Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    ///     Gets the sealed timestamp if the envelope is sealed.
    /// </summary>
    public DateTime? SealedAt => _sealedAt;

    /// <summary>
    ///     Gets the cryptographic signature if present.
    /// </summary>
    public string? Signature => _signature;

    #endregion

    #region Modification Methods (Return New Instances)

    /// <summary>
    ///     Creates a new envelope with an additional header.
    /// </summary>
    public Result<Envelope<T>, ValidationError> WithHeader(string key, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        if (_isSealed)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Cannot modify sealed envelope"));
        }

        var newHeaders = _headers.ToDictionary(StringComparer.Ordinal);
        newHeaders[key] = value;

        var newEnvelope = new Envelope<T>(
            _payload,
            newHeaders.ToFrozenDictionary(StringComparer.Ordinal),
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _encryptedPayload,
            _telemetry);

        return Result<Envelope<T>, ValidationError>.Success(newEnvelope);
    }

    /// <summary>
    ///     Creates a new envelope with multiple headers.
    /// </summary>
    public Result<Envelope<T>, ValidationError> WithHeaders(IReadOnlyDictionary<string, object> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (_isSealed)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Cannot modify sealed envelope"));
        }

        var newHeaders = _headers.ToDictionary(StringComparer.Ordinal);
        foreach (var (key, value) in headers)
        {
            newHeaders[key] = value;
        }

        var newEnvelope = new Envelope<T>(
            _payload,
            newHeaders.ToFrozenDictionary(StringComparer.Ordinal),
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _encryptedPayload,
            _telemetry);

        return Result<Envelope<T>, ValidationError>.Success(newEnvelope);
    }

    /// <summary>
    ///     Creates a new envelope with a modified payload.
    /// </summary>
    public Result<Envelope<T>, ValidationError> WithPayload(T newPayload)
    {
        ArgumentNullException.ThrowIfNull(newPayload);

        if (_isSealed)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Cannot modify sealed envelope"));
        }

        var newEnvelope = new Envelope<T>(
            newPayload,
            _headers,
            _isSealed,
            _createdAt,
            _sealedAt,
            _signature,
            _encryptedPayload,
            _telemetry);

        _telemetry.TrackResultTransformed(
            ResultState.Pending,
            ResultState.Pending,
            typeof(Envelope<T>));

        return Result<Envelope<T>, ValidationError>.Success(newEnvelope);
    }

    #endregion

    #region Sealing and Encryption

    /// <summary>
    ///     Creates a new sealed envelope with optional cryptographic signature.
    /// </summary>
    public Result<Envelope<T>, ValidationError> Seal(byte[]? cryptographicKey = null)
    {
        if (_isSealed)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Envelope is already sealed"));
        }

        var signature = cryptographicKey != null ? ComputeSignature(cryptographicKey) : null;

        var sealedEnvelope = new Envelope<T>(
            _payload,
            _headers,
            isSealed: true,
            _createdAt,
            sealedAt: DateTime.UtcNow,
            signature,
            _encryptedPayload,
            _telemetry);

        _telemetry.TrackResultTransformed(
            ResultState.Pending,
            ResultState.Success,
            typeof(Envelope<T>));

        return Result<Envelope<T>, ValidationError>.Success(sealedEnvelope);
    }

    /// <summary>
    ///     Creates a new unsealed envelope (removes seal).
    /// </summary>
    public Result<Envelope<T>, ValidationError> Unseal()
    {
        if (!_isSealed)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Envelope is not sealed"));
        }

        var unsealedEnvelope = new Envelope<T>(
            _payload,
            _headers,
            isSealed: false,
            _createdAt,
            sealedAt: null,
            signature: null,
            _encryptedPayload,
            _telemetry);

        return Result<Envelope<T>, ValidationError>.Success(unsealedEnvelope);
    }

    /// <summary>
    ///     Creates a new envelope with encrypted payload.
    /// </summary>
    public Result<Envelope<T>, ValidationError> Encrypt(byte[] encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);

        if (!_isSealed)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Payload must be sealed before encryption"));
        }

        if (_encryptedPayload != null)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Payload is already encrypted"));
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();

            var plainText = JsonSerializer.Serialize(_payload);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Add IV to headers
            var newHeaders = _headers.ToDictionary(StringComparer.Ordinal);
            newHeaders["PayloadIV"] = Convert.ToBase64String(aes.IV);

            var encryptedEnvelope = new Envelope<T>(
                _payload,
                newHeaders.ToFrozenDictionary(StringComparer.Ordinal),
                _isSealed,
                _createdAt,
                _sealedAt,
                _signature,
                encrypted,
                _telemetry);

            return Result<Envelope<T>, ValidationError>.Success(encryptedEnvelope);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError($"Encryption failed: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Creates a new envelope with decrypted payload.
    /// </summary>
    public Result<Envelope<T>, ValidationError> Decrypt(byte[] decryptionKey)
    {
        ArgumentNullException.ThrowIfNull(decryptionKey);

        if (_encryptedPayload == null)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Payload is not encrypted"));
        }

        if (!_headers.TryGetValue("PayloadIV", out var ivObj) || ivObj is not string ivString)
        {
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError("Missing encryption IV"));
        }

        try
        {
            var iv = Convert.FromBase64String(ivString);

            using var aes = Aes.Create();
            aes.Key = decryptionKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(_encryptedPayload, 0, _encryptedPayload.Length);
            var plainText = Encoding.UTF8.GetString(plainBytes);

            var decryptedPayload = JsonSerializer.Deserialize<T>(plainText);
            if (decryptedPayload == null)
            {
                return Result<Envelope<T>, ValidationError>.Failure(
                    new ValidationError("Failed to deserialize decrypted payload"));
            }

            var decryptedEnvelope = new Envelope<T>(
                decryptedPayload,
                _headers,
                _isSealed,
                _createdAt,
                _sealedAt,
                _signature,
                encryptedPayload: null,
                _telemetry);

            return Result<Envelope<T>, ValidationError>.Success(decryptedEnvelope);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError($"Decryption failed: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Verifies the envelope signature.
    /// </summary>
    public Result<bool, ValidationError> VerifySignature(byte[] cryptographicKey)
    {
        ArgumentNullException.ThrowIfNull(cryptographicKey);

        if (string.IsNullOrEmpty(_signature))
        {
            return Result<bool, ValidationError>.Failure(
                new ValidationError("Envelope has no signature"));
        }

        try
        {
            var computedSignature = ComputeSignature(cryptographicKey);
            var isValid = _signature == computedSignature;
            return Result<bool, ValidationError>.Success(isValid);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<bool, ValidationError>.Failure(
                new ValidationError($"Signature verification failed: {ex.Message}"));
        }
    }

    #endregion

    #region Validation

    /// <summary>
    ///     Validates the envelope using the provided validator.
    /// </summary>
    public ValidationResult Validate(Func<T, ValidationResult> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        try
        {
            return validator(_payload);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return ValidationResult.Failed($"Validation failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Validates the envelope asynchronously.
    /// </summary>
    public async ValueTask<ValidationResult> ValidateAsync(
        Func<T, CancellationToken, ValueTask<ValidationResult>> validator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);

        try
        {
            return await validator(_payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return ValidationResult.Failed($"Validation failed: {ex.Message}");
        }
    }

    #endregion

    #region Serialization

    /// <summary>
    ///     Serializes the envelope using the specified serializer.
    /// </summary>
    public Result<string, ValidationError> Serialize(IEnvelopeSerializer? serializer = null)
    {
        try
        {
            serializer ??= new JsonEnvelopeSerializer();
            var serialized = serializer.Serialize(this);
            return Result<string, ValidationError>.Success(serialized);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<string, ValidationError>.Failure(
                new ValidationError($"Serialization failed: {ex.Message}"));
        }
    }

    /// <summary>
    ///     Deserializes an envelope from a string.
    /// </summary>
    public static Result<Envelope<T>, ValidationError> Deserialize(
        string serializedData,
        IEnvelopeSerializer? serializer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedData);

        try
        {
            serializer ??= new JsonEnvelopeSerializer();
            var envelope = serializer.Deserialize<T>(serializedData);
            return Result<Envelope<T>, ValidationError>.Success(envelope);
        }
        catch (Exception ex)
        {
            var telemetry = new DefaultResultTelemetry();
            telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError($"Deserialization failed: {ex.Message}"));
        }
    }

    #endregion

    #region Cloning

    /// <summary>
    ///     Creates a deep clone of the envelope.
    /// </summary>
    public Result<Envelope<T>, ValidationError> Clone()
    {
        try
        {
            T clonedPayload;

            if (_payload is ICloneable cloneable)
            {
                clonedPayload = (T)cloneable.Clone();
            }
            else
            {
                // Fallback to serialization-based cloning
                var serialized = JsonSerializer.Serialize(_payload);
                clonedPayload = JsonSerializer.Deserialize<T>(serialized)
                    ?? throw new InvalidOperationException("Failed to clone payload");
            }

            var clonedEnvelope = new Envelope<T>(
                clonedPayload,
                _headers, // FrozenDictionary is immutable, safe to share
                _isSealed,
                _createdAt,
                _sealedAt,
                _signature,
                _encryptedPayload?.ToArray(), // Create new array if encrypted
                _telemetry);

            return Result<Envelope<T>, ValidationError>.Success(clonedEnvelope);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError($"Cloning failed: {ex.Message}"));
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Computes a cryptographic signature for the envelope.
    /// </summary>
    private string ComputeSignature(byte[] key)
    {
        var dataForSigning = new
        {
            Payload = _payload,
            Headers = _headers,
            CreatedAt = _createdAt
        };

        var jsonData = JsonSerializer.Serialize(dataForSigning);
        var dataBytes = Encoding.UTF8.GetBytes(jsonData);

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(dataBytes);

        return Convert.ToBase64String(hash);
    }

    /// <summary>
    ///     Gets the internal DTO for serialization.
    /// </summary>
    internal EnvelopeDto<T> GetDto()
    {
        return new EnvelopeDto<T>
        {
            Payload = _payload,
            Headers = _headers.ToDictionary(StringComparer.Ordinal),
            IsSealed = _isSealed,
            CreatedDate = _createdAt,
            SealedDate = _sealedAt,
            Signature = _signature,
            EncryptedPayload = _encryptedPayload != null
                ? Convert.ToBase64String(_encryptedPayload)
                : null
        };
    }

    #endregion

    #region DTO

    /// <summary>
    ///     Data Transfer Object for envelope serialization.
    /// </summary>
    internal sealed class EnvelopeDto<TDto>
    {
        public TDto Payload { get; init; } = default!;
        public Dictionary<string, object>? Headers { get; init; }
        public bool IsSealed { get; init; }
        public DateTime CreatedDate { get; init; }
        public DateTime? SealedDate { get; init; }
        public string? Signature { get; init; }
        public string? EncryptedPayload { get; init; }
    }

    #endregion
}
