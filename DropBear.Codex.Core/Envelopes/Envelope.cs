#region

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DropBear.Codex.Core.Enums;
using DropBear.Codex.Core.Envelopes.Serializers;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Core.Results.Diagnostics;
using DropBear.Codex.Core.Results.Validations;
using MessagePack;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     Represents an envelope that encapsulates a payload with metadata, validations, and state management.
///     Integrates with Result types for comprehensive error handling and diagnostics.
/// </summary>
/// <typeparam name="T">The type of payload contained in the envelope.</typeparam>
public partial class Envelope<T> : IResultDiagnostics
{
    private readonly DiagnosticInfo _diagnosticInfo;

    // Validators and headers
    private readonly ConcurrentDictionary<string, object> _headers;
    private readonly List<Func<string, object, ValidationResult>> _headerValidators;

    private readonly List<Func<T, ValidationResult>> _payloadValidators;

    // Core state management
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.SupportsRecursion);
    private readonly IResultTelemetry _telemetry;

    // Encryption and security
    private byte[]? _encryptedPayload;

    // Payload and state tracking
    private T _payload;
    private string? _signature;
    private EnvelopeState _state;

    /// <summary>
    ///     Initializes a new instance of the Envelope with the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encapsulate.</param>
    /// <param name="telemetry">Optional telemetry tracking.</param>
    public Envelope(
        T payload,
        IResultTelemetry? telemetry = null)
    {
        _payload = payload;
        _telemetry = telemetry ?? new DefaultResultTelemetry();
        _headers = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        _payloadValidators = new List<Func<T, ValidationResult>>();
        _headerValidators = new List<Func<string, object, ValidationResult>>();

        // Initialize state
        _state = new EnvelopeState { CreatedAt = DateTime.UtcNow, CurrentState = ResultState.Success };

        // Create diagnostic info
        _diagnosticInfo = new DiagnosticInfo(
            ResultState.Success,
            typeof(Envelope<T>),
            _state.CreatedAt,
            Activity.Current?.Id
        );

        // Track envelope creation
        _telemetry.TrackResultCreated(ResultState.Success, typeof(Envelope<T>));
    }

    // New constructor for DTO-based creation
    internal Envelope(
        EnvelopeDto<T> dto,
        IResultTelemetry? telemetry = null)
    {
        _telemetry = telemetry ?? new DefaultResultTelemetry();

        // Initialize state from DTO
        _state = new EnvelopeState { CreatedAt = dto.CreatedDate, IsSealed = dto.IsSealed, SealedAt = dto.SealedDate };

        // Initialize headers
        _headers = dto.Headers != null
            ? new ConcurrentDictionary<string, object>(dto.Headers, StringComparer.Ordinal)
            : new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

        // Handle payload
        if (!string.IsNullOrEmpty(dto.EncryptedPayload))
        {
            _encryptedPayload = Convert.FromBase64String(dto.EncryptedPayload);
        }
        else
        {
            _payload = dto.Payload;
        }

        // Set signature
        _signature = dto.Signature;

        // Create diagnostic info
        _diagnosticInfo = new DiagnosticInfo(
            _state.IsSealed ? ResultState.Success : ResultState.Failure,
            typeof(Envelope<T>),
            _state.CreatedAt,
            Activity.Current?.Id
        );

        // Initialize validators
        _payloadValidators = new List<Func<T, ValidationResult>>();
        _headerValidators = new List<Func<string, object, ValidationResult>>();

        // Track envelope creation
        _telemetry.TrackResultCreated(
            _state.IsSealed ? ResultState.Success : ResultState.Failure,
            typeof(Envelope<T>)
        );
    }

    protected T Payload
    {
        get
        {
            _rwLock.EnterReadLock();
            try
            {
                // Check if the envelope is sealed but not encrypted
                if (_state.IsSealed && _encryptedPayload == null)
                {
                    return _payload;
                }

                // Check if payload is encrypted
                if (_encryptedPayload != null)
                {
                    throw new InvalidOperationException("Payload is currently encrypted. Decrypt first.");
                }

                // Check if envelope is not sealed and requires validation
                var validationResult = ValidatePayload();
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Payload validation failed: {validationResult.ErrorMessage}");
                }

                return _payload;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }
    }

    // Diagnostic information implementation
    public DiagnosticInfo GetDiagnostics()
    {
        return _diagnosticInfo;
    }

    public ActivityContext GetTraceContext()
    {
        return Activity.Current?.Context ?? default;
    }

    // State tracking struct for improved memory efficiency
    [StructLayout(LayoutKind.Auto)]
    private struct EnvelopeState
    {
        public bool IsSealed;
        public DateTime CreatedAt;
        public DateTime? SealedAt;
        public ResultState CurrentState;
    }
}

public partial class Envelope<T>
{
    /// <summary>
    ///     Validates the current payload using registered validators.
    /// </summary>
    /// <returns>A ValidationResult indicating the outcome of payload validation.</returns>
    public ValidationResult ValidatePayload()
    {
        _rwLock.EnterReadLock();
        try
        {
            var validationResults = _payloadValidators
                .Select(validator => validator(_payload))
                .ToList();

            return ValidationResult.Combine(validationResults);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    ///     Attempts to modify the payload with validation.
    /// </summary>
    /// <param name="newPayload">The new payload to set.</param>
    /// <returns>A Result indicating the success or failure of payload modification.</returns>
    public Result<T, ValidationError> TryModifyPayload(T newPayload)
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Check if envelope is sealed
            if (_state.IsSealed)
            {
                return Result<T, ValidationError>.Failure(
                    new ValidationError("Cannot modify payload of a sealed envelope")
                );
            }

            // Validate new payload
            var validationResult = _payloadValidators
                .Select(validator => validator(newPayload))
                .ToList();

            var combinedValidation = ValidationResult.Combine(validationResult);

            if (!combinedValidation.IsValid)
            {
                return Result<T, ValidationError>.Failure(
                    new ValidationError(combinedValidation.ErrorMessage)
                );
            }

            // Store previous payload for potential rollback
            var previousPayload = _payload;
            _payload = newPayload;

            // Track payload modification
            _telemetry.TrackResultTransformed(
                _state.CurrentState,
                ResultState.Success,
                typeof(Envelope<T>)
            );

            // Raise modification event
            OnPayloadModified();

            return Result<T, ValidationError>.Success(newPayload);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<T, ValidationError>.Failure(
                new ValidationError($"Payload modification failed: {ex.Message}")
            );
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Adds a header to the envelope with validation.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
    /// <returns>A Result indicating the success or failure of header addition.</returns>
    public Result<Unit, ValidationError> AddHeader(string key, object value)
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Check if envelope is sealed
            if (_state.IsSealed)
            {
                return Result<Unit, ValidationError>.Failure(
                    new ValidationError("Cannot modify headers of a sealed envelope")
                );
            }

            // Validate header
            var validationResults = _headerValidators
                .Select(validator => validator(key, value))
                .ToList();

            var combinedValidation = ValidationResult.Combine(validationResults);

            if (!combinedValidation.IsValid)
            {
                return Result<Unit, ValidationError>.Failure(
                    new ValidationError(combinedValidation.ErrorMessage)
                );
            }

            // Add header
            _headers[key] = value;

            // Track header modification
            _telemetry.TrackResultTransformed(
                _state.CurrentState,
                ResultState.Success,
                typeof(Envelope<T>)
            );

            return Result<Unit, ValidationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Header addition failed: {ex.Message}")
            );
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Registers a payload validator.
    /// </summary>
    /// <param name="validator">The validator function.</param>
    public void RegisterPayloadValidator(Func<T, ValidationResult> validator)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _payloadValidators.Add(validator);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Registers a header validator.
    /// </summary>
    /// <param name="validator">The validator function.</param>
    public void RegisterHeaderValidator(Func<string, object, ValidationResult> validator)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _headerValidators.Add(validator);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
}

public partial class Envelope<T>
{
    /// <summary>
    ///     Seals the envelope, preventing further modifications.
    /// </summary>
    /// <param name="cryptographicKey">Optional key for computing a cryptographic signature.</param>
    /// <returns>A Result indicating the success of sealing the envelope.</returns>
    public Result<Unit, ValidationError> Seal(byte[]? cryptographicKey = null)
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Check if already sealed
            if (_state.IsSealed)
            {
                return Result<Unit, ValidationError>.Failure(
                    new ValidationError("Envelope is already sealed")
                );
            }

            // Validate payload before sealing
            var validationResult = ValidatePayload();
            if (!validationResult.IsValid)
            {
                return Result<Unit, ValidationError>.Failure(
                    new ValidationError($"Cannot seal envelope: {validationResult.ErrorMessage}")
                );
            }

            // Update state
            _state.IsSealed = true;
            _state.SealedAt = DateTime.UtcNow;
            _state.CurrentState = ResultState.Success;

            // Compute signature if key provided
            if (cryptographicKey != null)
            {
                _signature = ComputeSignature(cryptographicKey);
            }

            // Track sealing operation
            _telemetry.TrackResultTransformed(
                ResultState.Success,
                ResultState.Success,
                typeof(Envelope<T>)
            );

            // Raise sealed event
            OnSealed();

            return Result<Unit, ValidationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Sealing failed: {ex.Message}")
            );
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Unseals the envelope, allowing modifications.
    /// </summary>
    /// <returns>A Result indicating the success of unsealing the envelope.</returns>
    public Result<Unit, ValidationError> Unseal()
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Check if already unsealed
            if (!_state.IsSealed)
            {
                return Result<Unit, ValidationError>.Failure(
                    new ValidationError("Envelope is not sealed")
                );
            }

            // Reset sealing state
            _state.IsSealed = false;
            _state.SealedAt = null;
            _signature = null;

            // Track unsealing operation
            _telemetry.TrackResultTransformed(
                ResultState.Success,
                ResultState.Success,
                typeof(Envelope<T>)
            );

            // Raise unsealed event
            OnUnsealed();

            return Result<Unit, ValidationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Unsealing failed: {ex.Message}")
            );
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Encrypts the payload using the provided encryption key.
    /// </summary>
    /// <param name="encryptionKey">Symmetric encryption key.</param>
    /// <returns>A Result indicating the success of payload encryption.</returns>
    public Result<Unit, ValidationError> EncryptPayload(byte[] encryptionKey)
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Ensure envelope is sealed before encryption
            if (!_state.IsSealed)
            {
                return Result<Unit, ValidationError>.Failure(
                    new ValidationError("Payload must be sealed before encryption")
                );
            }

            // Use AES encryption
            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();

            // Serialize payload
            var plainText = JsonSerializer.Serialize(_payload);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            // Encrypt payload
            _encryptedPayload = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Store IV in headers
            _headers["PayloadIV"] = Convert.ToBase64String(aes.IV);

            // Track encryption operation
            _telemetry.TrackResultTransformed(
                _state.CurrentState,
                ResultState.Success,
                typeof(Envelope<T>)
            );

            return Result<Unit, ValidationError>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Unit, ValidationError>.Failure(
                new ValidationError($"Payload encryption failed: {ex.Message}")
            );
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    ///     Decrypts the payload using the provided encryption key.
    /// </summary>
    /// <param name="decryptionKey">Symmetric decryption key.</param>
    /// <returns>A Result containing the decrypted payload.</returns>
    public Result<T, ValidationError> DecryptPayload(byte[] decryptionKey)
    {
        _rwLock.EnterWriteLock();
        try
        {
            // Ensure encrypted payload exists
            if (_encryptedPayload == null)
            {
                return Result<T, ValidationError>.Failure(
                    new ValidationError("No encrypted payload found")
                );
            }

            // Retrieve IV from headers
            if (!_headers.TryGetValue("PayloadIV", out var ivObj) ||
                ivObj is not string ivString)
            {
                return Result<T, ValidationError>.Failure(
                    new ValidationError("Missing encryption IV")
                );
            }

            var iv = Convert.FromBase64String(ivString);

            // Decrypt payload
            using var aes = Aes.Create();
            aes.Key = decryptionKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(_encryptedPayload, 0, _encryptedPayload.Length);
            var plainText = Encoding.UTF8.GetString(plainBytes);

            // Deserialize payload
            var decryptedPayload = JsonSerializer.Deserialize<T>(plainText);

            if (decryptedPayload == null)
            {
                return Result<T, ValidationError>.Failure(
                    new ValidationError("Failed to deserialize decrypted payload")
                );
            }

            // Clear encrypted payload
            _encryptedPayload = null;

            // Track decryption operation
            _telemetry.TrackResultTransformed(
                _state.CurrentState,
                ResultState.Success,
                typeof(Envelope<T>)
            );

            return Result<T, ValidationError>.Success(decryptedPayload);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<T, ValidationError>.Failure(
                new ValidationError($"Payload decryption failed: {ex.Message}")
            );
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
}

public partial class Envelope<T>
{
    // Event handlers
    public event EventHandler? Sealed;
    public event EventHandler? Unsealed;
    public event EventHandler? PayloadModified;

    // Protected event raising methods
    protected virtual void OnSealed()
    {
        Sealed?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnUnsealed()
    {
        Unsealed?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnPayloadModified()
    {
        PayloadModified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Creates a deep clone of the envelope.
    /// </summary>
    /// <returns>A Result containing the cloned envelope.</returns>
    public Result<Envelope<T>, ValidationError> Clone()
    {
        _rwLock.EnterReadLock();
        try
        {
            // Deep clone headers
            var clonedHeaders = new Dictionary<string, object>(_headers, StringComparer.Ordinal);

            // Deep clone payload if possible
            T clonedPayload;
            if (_payload is ICloneable cloneablePayload)
            {
                clonedPayload = (T)cloneablePayload.Clone();
            }
            else
            {
                // Fallback to serialization-based cloning
                var serializedPayload = JsonSerializer.Serialize(_payload);
                clonedPayload = JsonSerializer.Deserialize<T>(serializedPayload)
                                ?? throw new InvalidOperationException("Failed to clone payload");
            }

            // Create new envelope with cloned data
            var clonedEnvelope = new Envelope<T>(clonedPayload, _telemetry);

            // Copy validators
            foreach (var validator in _payloadValidators)
            {
                clonedEnvelope.RegisterPayloadValidator(validator);
            }

            foreach (var validator in _headerValidators)
            {
                clonedEnvelope.RegisterHeaderValidator(validator);
            }

            // Copy headers
            foreach (var header in clonedHeaders)
            {
                clonedEnvelope.AddHeader(header.Key, header.Value);
            }

            return Result<Envelope<T>, ValidationError>.Success(clonedEnvelope);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError($"Envelope cloning failed: {ex.Message}")
            );
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    ///     Serializes the envelope using the specified serializer.
    /// </summary>
    /// <param name="serializer">Optional serializer (defaults to JSON).</param>
    /// <returns>A Result containing the serialized envelope.</returns>
    public Result<string, ValidationError> Serialize(IEnvelopeSerializer? serializer = null)
    {
        _rwLock.EnterReadLock();
        try
        {
            serializer ??= new JsonEnvelopeSerializer();

            var serializedEnvelope = serializer.Serialize(this);

            return Result<string, ValidationError>.Success(serializedEnvelope);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));
            return Result<string, ValidationError>.Failure(
                new ValidationError($"Envelope serialization failed: {ex.Message}")
            );
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    ///     Deserializes an envelope from a string.
    /// </summary>
    /// <param name="serializedData">The serialized envelope data.</param>
    /// <param name="serializer">Optional serializer (defaults to JSON).</param>
    /// <returns>A Result containing the deserialized envelope.</returns>
    public static Result<Envelope<T>, ValidationError> Deserialize(
        string serializedData,
        IEnvelopeSerializer? serializer = null)
    {
        try
        {
            serializer ??= new JsonEnvelopeSerializer();

            var deserializedEnvelope = serializer.Deserialize<T>(serializedData);

            return Result<Envelope<T>, ValidationError>.Success(deserializedEnvelope);
        }
        catch (Exception ex)
        {
            // Use static telemetry method or default telemetry
            var telemetry = new DefaultResultTelemetry();
            telemetry.TrackException(ex, ResultState.Failure, typeof(Envelope<T>));

            return Result<Envelope<T>, ValidationError>.Failure(
                new ValidationError($"Envelope deserialization failed: {ex.Message}")
            );
        }
    }

    // Signature computation method
    private string? ComputeSignature(byte[]? key)
    {
        if (key == null)
        {
            return null;
        }

        try
        {
            // Prepare signing data
            var dataForSigning = new { Payload = _payload, Headers = _headers, _state.CreatedAt };

            var jsonData = JsonSerializer.Serialize(dataForSigning);
            var dataBytes = Encoding.UTF8.GetBytes(jsonData);

            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(dataBytes);

            return Convert.ToBase64String(hash);
        }
        catch
        {
            return null;
        }
    }
}

public partial class Envelope<T>
{
    /// <summary>
    ///     Creates a Data Transfer Object (DTO) representing the envelope's current state.
    ///     This is used for cloning and serialization.
    /// </summary>
    /// <returns>An instance of <see cref="EnvelopeDto{T}" />.</returns>
    internal EnvelopeDto<T> GetDto()
    {
        _rwLock.EnterReadLock();
        try
        {
            return new EnvelopeDto<T>
            {
                Payload = _payload,
                Headers = new Dictionary<string, object>(_headers, StringComparer.Ordinal),
                IsSealed = _state.IsSealed,
                CreatedDate = _state.CreatedAt,
                SealedDate = _state.SealedAt,
                Signature = _signature,
                EncryptedPayload = _encryptedPayload != null
                    ? Convert.ToBase64String(_encryptedPayload)
                    : null
            };
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    ///     Data Transfer Object for envelope serialization and cloning.
    ///     Supports both System.Text.Json and MessagePack serialization.
    /// </summary>
    [MessagePackObject(true)]
    internal sealed class EnvelopeDto<TDto>
    {
        /// <summary>
        ///     The payload of the envelope.
        /// </summary>
        [Key(0)]
        [JsonPropertyName("Payload")]
        public TDto Payload { get; init; } = default!;

        /// <summary>
        ///     Optional headers associated with the envelope.
        /// </summary>
        [Key(1)]
        [JsonPropertyName("Headers")]
        public Dictionary<string, object>? Headers { get; init; }

        /// <summary>
        ///     Indicates whether the envelope is sealed.
        /// </summary>
        [Key(2)]
        [JsonPropertyName("IsSealed")]
        public bool IsSealed { get; set; }

        /// <summary>
        ///     The timestamp when the envelope was created.
        /// </summary>
        [Key(3)]
        [JsonPropertyName("CreatedDate")]
        public DateTime CreatedDate { get; init; }

        /// <summary>
        ///     The timestamp when the envelope was sealed (if applicable).
        /// </summary>
        [Key(4)]
        [JsonPropertyName("SealedDate")]
        public DateTime? SealedDate { get; set; }

        /// <summary>
        ///     Cryptographic signature of the envelope (if computed).
        /// </summary>
        [Key(5)]
        [JsonPropertyName("Signature")]
        public string? Signature { get; set; }

        /// <summary>
        ///     Encrypted payload, if the envelope's payload is encrypted.
        /// </summary>
        [Key(6)]
        [JsonPropertyName("EncryptedPayload")]
        public string? EncryptedPayload { get; set; }
    }
}
