#region Envelope Class

#region

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DropBear.Codex.Core.Envelopes.Serializers;
using DropBear.Codex.Core.Interfaces;
using DropBear.Codex.Core.Results.Validations;

#endregion

namespace DropBear.Codex.Core.Envelopes;

/// <summary>
///     A generic envelope that encapsulates a payload with metadata, validations, security, and state management.
/// </summary>
/// <typeparam name="T">Type of the payload.</typeparam>
public class Envelope<T>
{
    #region DTO for Serialization/Cloning

    /// <summary>
    ///     Data Transfer Object for envelope serialization and cloning.
    /// </summary>
    /// <typeparam name="TDto">The type of the payload.</typeparam>
    internal sealed class EnvelopeDto<TDto>
    {
        public TDto Payload { get; init; } = default!;
        public Dictionary<string, object>? Headers { get; init; }
        public bool IsSealed { get; set; }
        public DateTime CreatedDate { get; init; }
        public DateTime? SealedDate { get; set; }
        public string? Signature { get; set; }
        public string? EncryptedPayload { get; set; }
    }

    #endregion

    #region Private Fields

    private T _payload = default!;
    private readonly Dictionary<string, object> _headers;
    private IReadOnlyDictionary<string, object>? _cachedReadOnlyHeaders; // Cached read-only header view.
    private readonly object _lock = new();

    // Validators for headers and payload.
    private readonly List<Func<string, object, ValidationResult>> _headerValidators;
    private readonly List<Func<T, ValidationResult>> _payloadValidators;

    // State fields.
    private bool _isSealed;
    private DateTime? _sealedDate;
    private string? _signature; // Cryptographic signature (if computed).

    // For payload encryption.
    private byte[]? _encryptedPayload;

    #endregion

    #region Events

    /// <summary>
    ///     Occurs when the envelope is sealed.
    /// </summary>
    public event EventHandler? Sealed;

    /// <summary>
    ///     Occurs when the envelope is unsealed.
    /// </summary>
    public event EventHandler? Unsealed;

    /// <summary>
    ///     Occurs when the payload is modified.
    /// </summary>
    public event EventHandler? PayloadModified;

    #endregion

    #region Public Properties

    /// <summary>
    ///     Gets the payload.
    ///     If the payload is encrypted, an exception is thrown until it is decrypted.
    /// </summary>
    public T Payload
    {
        get
        {
            lock (_lock)
            {
                if (_encryptedPayload != null)
                {
                    throw new InvalidOperationException("Payload is currently encrypted. Decrypt first.");
                }

                return _payload;
            }
        }
    }

    /// <summary>
    ///     Gets a cached, read-only view of the envelope headers.
    /// </summary>
    public IReadOnlyDictionary<string, object> Headers
    {
        get
        {
            lock (_lock)
            {
                return _cachedReadOnlyHeaders ??= new ReadOnlyDictionary<string, object>(_headers);
            }
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the envelope is sealed.
    /// </summary>
    public bool IsSealed
    {
        get
        {
            lock (_lock)
            {
                return _isSealed;
            }
        }
    }

    /// <summary>
    ///     Gets the creation timestamp in UTC.
    /// </summary>
    public DateTime CreatedDate { get; }

    /// <summary>
    ///     Gets the sealed timestamp (if sealed); otherwise, null.
    /// </summary>
    public DateTime? SealedDate
    {
        get
        {
            lock (_lock)
            {
                return _sealedDate;
            }
        }
    }

    /// <summary>
    ///     Gets the cryptographic signature (if computed) as a hexadecimal string.
    /// </summary>
    public string? Signature
    {
        get
        {
            lock (_lock)
            {
                return _signature;
            }
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="Envelope{T}" /> class.
    /// </summary>
    /// <param name="payload">The payload to encapsulate.</param>
    /// <param name="headers">Optional initial headers.</param>
    /// <param name="headerValidators">Optional validators for headers.</param>
    /// <param name="payloadValidators">Optional validators for the payload.</param>
    protected Envelope(
        T payload,
        IDictionary<string, object>? headers = null,
        IEnumerable<Func<string, object, ValidationResult>>? headerValidators = null,
        IEnumerable<Func<T, ValidationResult>>? payloadValidators = null)
    {
        CreatedDate = DateTime.UtcNow;

        // Initialize headers.
        _headers = headers != null
            ? new Dictionary<string, object>(headers, StringComparer.Ordinal)
            : new Dictionary<string, object>(StringComparer.Ordinal);

        // Automatically add a trace identifier if not provided.
        if (!_headers.ContainsKey("TraceId"))
        {
            _headers["TraceId"] = Guid.NewGuid().ToString();
        }

        // Initialize validators.
        _headerValidators = headerValidators != null
            ? [..headerValidators]
            : [];

        _payloadValidators = payloadValidators != null
            ? [..payloadValidators]
            : [];

        // Validate the payload.
        ValidatePayload(payload);
        _payload = payload;

        _isSealed = false;
        _sealedDate = null;
        _signature = null;
        _encryptedPayload = null;
    }

    /// <summary>
    ///     Internal constructor used for cloning and deserialization.
    /// </summary>
    /// <param name="dto">Data transfer object containing envelope state.</param>
    internal Envelope(EnvelopeDto<T> dto)
    {
        CreatedDate = dto.CreatedDate;
        _sealedDate = dto.SealedDate;
        _isSealed = dto.IsSealed;
        _signature = dto.Signature;
        _headers = dto.Headers != null
            ? new Dictionary<string, object>(dto.Headers, StringComparer.Ordinal)
            : new Dictionary<string, object>(StringComparer.Ordinal);

        // Deserialize encrypted payload if provided.
        if (!string.IsNullOrEmpty(dto.EncryptedPayload))
        {
            _encryptedPayload = Convert.FromBase64String(dto.EncryptedPayload);
        }
        else
        {
            _payload = dto.Payload;
        }

        // Note: Validators and events are not serialized; they must be registered after deserialization.
        _headerValidators = new List<Func<string, object, ValidationResult>>();
        _payloadValidators = new List<Func<T, ValidationResult>>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Adds or updates a header in the envelope after validating it.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
    /// <exception cref="InvalidOperationException">Thrown if the envelope is sealed.</exception>
    /// <exception cref="ArgumentException">Thrown if header validation fails.</exception>
    public void AddHeader(string key, object value)
    {
        lock (_lock)
        {
            if (_isSealed)
            {
                throw new InvalidOperationException("Cannot modify headers; envelope is sealed.");
            }

            // Check for null key.
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // Check for null value.
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            // Run through all header validators.
            foreach (var validator in _headerValidators)
            {
                var result = validator(key, value);
                if (!result.IsValid)
                {
                    throw new ArgumentException($"Header '{key}' failed validation: {result.ErrorMessage}",
                        nameof(value));
                }
            }

            _headers[key] = value;
            _cachedReadOnlyHeaders = null; // Invalidate cached view.
        }
    }

    /// <summary>
    ///     Registers an additional header validator.
    /// </summary>
    /// <param name="validator">A delegate to validate headers.</param>
    public void RegisterHeaderValidator(Func<string, object, ValidationResult> validator)
    {
        lock (_lock)
        {
            _headerValidators.Add(validator);
        }
    }

    /// <summary>
    ///     Registers an additional payload validator.
    /// </summary>
    /// <param name="validator">A delegate to validate the payload.</param>
    public void RegisterPayloadValidator(Func<T, ValidationResult> validator)
    {
        lock (_lock)
        {
            _payloadValidators.Add(validator);
        }
    }

    /// <summary>
    ///     Attempts to modify the payload.
    /// </summary>
    /// <param name="newPayload">The new payload.</param>
    /// <returns><c>true</c> if the payload was updated; otherwise, <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the envelope is sealed.</exception>
    public bool TryModifyPayload(T newPayload)
    {
        lock (_lock)
        {
            if (_isSealed)
            {
                return false;
            }

            ValidatePayload(newPayload);
            _payload = newPayload;
            OnPayloadModified();
            return true;
        }
    }

    /// <summary>
    ///     Seals the envelope, preventing further modifications.
    ///     Optionally computes a cryptographic signature using the provided key.
    /// </summary>
    /// <param name="cryptographicKey">Optional key for computing an HMACSHA256 signature.</param>
    /// <exception cref="InvalidOperationException">Thrown if already sealed.</exception>
    public void Seal(byte[]? cryptographicKey = null)
    {
        lock (_lock)
        {
            if (_isSealed)
            {
                throw new InvalidOperationException("Envelope is already sealed.");
            }

            _isSealed = true;
            _sealedDate = DateTime.UtcNow;

            if (cryptographicKey != null)
            {
                _signature = ComputeSignature(cryptographicKey);
            }
        }

        OnSealed();
    }

    /// <summary>
    ///     Unseals the envelope, allowing modifications.
    ///     Clears any computed signature.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the envelope is not sealed.</exception>
    public void Unseal()
    {
        lock (_lock)
        {
            if (!_isSealed)
            {
                throw new InvalidOperationException("Envelope is not sealed.");
            }

            _isSealed = false;
            _sealedDate = null;
            _signature = null;
        }

        OnUnsealed();
    }

    /// <summary>
    ///     Encrypts the payload using the provided encryption key.
    ///     The payload must be sealed before encryption.
    /// </summary>
    /// <param name="encryptionKey">A symmetric key for encryption (e.g. AES key bytes).</param>
    /// <exception cref="InvalidOperationException">Thrown if the envelope is not sealed.</exception>
    public void EncryptPayload(byte[] encryptionKey)
    {
        lock (_lock)
        {
            if (!_isSealed)
            {
                throw new InvalidOperationException("Payload must be sealed before encryption.");
            }

            // Use AES encryption.
            using var aes = Aes.Create();
            aes.Key = encryptionKey;
            aes.GenerateIV();
            using (var encryptor = aes.CreateEncryptor())
            {
                // Serialize the payload to JSON.
                var plainText = JsonSerializer.Serialize(_payload);
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                _encryptedPayload = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }

            // Store the IV in headers as base64.
            _headers["PayloadIV"] = Convert.ToBase64String(aes.IV);
            _cachedReadOnlyHeaders = null;
        }
    }

    /// <summary>
    ///     Decrypts the payload using the provided encryption key.
    /// </summary>
    /// <param name="encryptionKey">The symmetric key used for encryption.</param>
    /// <exception cref="InvalidOperationException">Thrown if there is no encrypted payload or missing IV.</exception>
    public void DecryptPayload(byte[] encryptionKey)
    {
        lock (_lock)
        {
            if (_encryptedPayload == null)
            {
                throw new InvalidOperationException("Payload is not encrypted.");
            }

            if (!_headers.TryGetValue("PayloadIV", out var ivObj) || ivObj is not string ivString)
            {
                throw new InvalidOperationException("Missing encryption IV in headers.");
            }

            var iv = Convert.FromBase64String(ivString);
            using (var aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                {
                    var plainBytes = decryptor.TransformFinalBlock(_encryptedPayload, 0, _encryptedPayload.Length);
                    var plainText = Encoding.UTF8.GetString(plainBytes);
                    _payload = JsonSerializer.Deserialize<T>(plainText) ??
                               throw new InvalidOperationException("Failed to deserialize payload.");
                }
            }

            // Clear the encrypted payload after successful decryption.
            _encryptedPayload = null;
        }
    }

    /// <summary>
    ///     Creates a clone of the envelope.
    ///     The cloned envelope is unsealed by default.
    /// </summary>
    /// <returns>A new envelope instance with a deep copy of the payload and headers.</returns>
    public Envelope<T> Clone()
    {
        lock (_lock)
        {
            // Create a DTO snapshot and then a new instance.
            var dto = GetDto();
            // Ensure the clone is unsealed and without cryptographic signature.
            dto.IsSealed = false;
            dto.SealedDate = null;
            dto.Signature = null;
            // Also, we do not clone the encrypted payload.
            dto.EncryptedPayload = null;
            return new Envelope<T>(dto);
        }
    }

    /// <summary>
    ///     Serializes the envelope using the specified serializer (or default JSON serializer).
    /// </summary>
    /// <param name="serializer">Optional serializer; if null, the default JSON serializer is used.</param>
    /// <returns>A string representing the serialized envelope.</returns>
    public string ToSerializedString(IEnvelopeSerializer? serializer = null)
    {
        serializer ??= new JsonEnvelopeSerializer();
        return serializer.Serialize(this);
    }

    /// <summary>
    ///     Deserializes an envelope from a string using the specified serializer.
    /// </summary>
    /// <param name="data">The serialized envelope data.</param>
    /// <param name="serializer">Optional serializer; if null, the default JSON serializer is used.</param>
    /// <returns>An envelope instance.</returns>
    public static Envelope<T> FromSerializedString(string data, IEnvelopeSerializer? serializer = null)
    {
        serializer ??= new JsonEnvelopeSerializer();
        return serializer.Deserialize<T>(data);
    }

    #endregion

    #region Protected Virtual Methods (Event Raisers)

    /// <summary>
    ///     Raises the Sealed event.
    /// </summary>
    protected virtual void OnSealed()
    {
        Sealed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Raises the Unsealed event.
    /// </summary>
    protected virtual void OnUnsealed()
    {
        Unsealed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Raises the PayloadModified event.
    /// </summary>
    protected virtual void OnPayloadModified()
    {
        PayloadModified?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Validates the payload by running all registered payload validators.
    /// </summary>
    /// <param name="payload">The payload to validate.</param>
    /// <exception cref="ArgumentException">Thrown if validation fails.</exception>
    private void ValidatePayload(T payload)
    {
        foreach (var validator in _payloadValidators)
        {
            var result = validator(payload);
            if (!result.IsValid)
            {
                throw new ArgumentException($"Payload validation failed: {result.ErrorMessage}", nameof(payload));
            }
        }
    }

    /// <summary>
    ///     Computes an HMACSHA256 signature over the envelope's critical data.
    /// </summary>
    /// <param name="key">The cryptographic key.</param>
    /// <returns>A hexadecimal string representing the signature.</returns>
    private string? ComputeSignature(byte[]? key)
    {
        // Prepare the data to be signed.
        var dataForSigning = new { Payload = _payload, Headers = _headers, CreatedDate, SealedDate = _sealedDate };

        var jsonData = JsonSerializer.Serialize(dataForSigning);
        var dataBytes = Encoding.UTF8.GetBytes(jsonData);

        if (key != null)
        {
            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(dataBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        return null;
    }

    /// <summary>
    ///     Creates a Data Transfer Object (DTO) representing the envelope's current state.
    ///     This is used for cloning and serialization.
    /// </summary>
    /// <returns>An instance of <see cref="EnvelopeDto{T}" />.</returns>
    internal EnvelopeDto<T> GetDto()
    {
        lock (_lock)
        {
            return new EnvelopeDto<T>
            {
                Payload = _payload,
                Headers = new Dictionary<string, object>(_headers, StringComparer.Ordinal),
                IsSealed = _isSealed,
                CreatedDate = CreatedDate,
                SealedDate = _sealedDate,
                Signature = _signature,
                EncryptedPayload = _encryptedPayload != null ? Convert.ToBase64String(_encryptedPayload) : null
            };
        }
    }

    #endregion
}

#endregion
