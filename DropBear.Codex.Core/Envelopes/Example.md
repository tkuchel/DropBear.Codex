# DropBear.Codex.Core.Envelopes

## Overview

The Envelope class provides a robust, secure, and flexible mechanism for encapsulating and managing payloads with comprehensive validation, serialization, and state management capabilities.

## Key Features

- Payload Encapsulation
- Thread-Safe Operations
- Comprehensive Validation
- Cryptographic Signature Support
- Payload Encryption
- Result-Based Error Handling
- Telemetry and Diagnostics

## Usage Example

### Creating an Envelope

```csharp
// Create a simple envelope with a payload
var payload = new PaymentData(100.00m, "USD");
var envelope = new Envelope<PaymentData>(payload);

// Add domain-specific validators
envelope.RegisterPayloadValidator(payment => 
{
    if (payment.Amount <= 0)
        return ValidationResult.Failed("Amount must be positive");
    return ValidationResult.Success;
});
```

## Payload Validation

Envelopes support comprehensive payload validation through:
- Payload validators
- Sealed/Unsealed state management
- Encryption state checks

## Sealing and Encryption

```csharp
// Seal the envelope with a cryptographic key
byte[] signingKey = GetSecureKey();
envelope.Seal(signingKey);

// Encrypt the payload
byte[] encryptionKey = GetSecureEncryptionKey();
envelope.EncryptPayload(encryptionKey);
```

## Serialization

```csharp
// Serialize the envelope
string serializedEnvelope = envelope.Serialize();

// Deserialize the envelope
var deserializedEnvelope = Envelope<PaymentData>.Deserialize(serializedEnvelope);
```

## Error Handling

All operations return Result types with comprehensive error information:

```csharp
var result = envelope.AddHeader("TransactionId", Guid.NewGuid());
if (!result.IsSuccess)
{
    Console.WriteLine(result.Error.Message);
}
```

## Thread Safety

- Uses `ReaderWriterLockSlim` for efficient concurrent access
- Supports multi-threaded scenarios
- Prevents race conditions during payload and header modifications

## Performance Considerations

- Minimal overhead for validation and state tracking
- Efficient locking mechanisms
- Lazy initialization of components
- Telemetry tracking with minimal performance impact

## Security Features

- Cryptographic payload signatures
- Payload encryption
- Comprehensive validation
- Immutable state after sealing

## Diagnostics and Telemetry

- Tracks result creation and transformations
- Captures exception details
- Provides diagnostic information about envelope state

## Limitations and Considerations

- Payload must be serializable
- Encryption requires secure key management
- Performance overhead for frequent validation

## Best Practices

- Always validate payloads before sealing
- Use secure, randomly generated keys for signing and encryption
- Implement proper key rotation and management
- Consider performance implications of extensive validators

## License

Part of the DropBear.Codex.Core library. See project license for details.
