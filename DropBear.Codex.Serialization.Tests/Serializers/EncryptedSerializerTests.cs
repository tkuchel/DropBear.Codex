using System.Text;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Interfaces;
using DropBear.Codex.Serialization.Serializers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DropBear.Codex.Serialization.Tests.Serializers;

public sealed class EncryptedSerializerTests
{
    [Fact]
    public async Task SerializeAsync_ShouldStillEncryptSmallPayloads_UnlessPlaintextFallbackIsExplicitlyEnabled()
    {
        var innerSerializer = new StubSerializer();
        var encryptionProvider = new StubEncryptionProvider();
        var serializer = new EncryptedSerializer(
            innerSerializer,
            encryptionProvider,
            NullLogger<EncryptedSerializer>.Instance,
            skipSmallObjects: true,
            encryptionThreshold: 100,
            allowPlaintextFallbackForSmallObjects: false);

        var result = await serializer.SerializeAsync("tiny");

        result.IsSuccess.Should().BeTrue();
        result.Value![0].Should().Be(1);
    }

    [Fact]
    public async Task SerializeAsync_ShouldAllowPlaintextFallback_WhenExplicitlyEnabled()
    {
        var innerSerializer = new StubSerializer();
        var encryptionProvider = new StubEncryptionProvider();
        var serializer = new EncryptedSerializer(
            innerSerializer,
            encryptionProvider,
            NullLogger<EncryptedSerializer>.Instance,
            skipSmallObjects: true,
            encryptionThreshold: 100,
            allowPlaintextFallbackForSmallObjects: true);

        var result = await serializer.SerializeAsync("tiny");

        result.IsSuccess.Should().BeTrue();
        result.Value![0].Should().Be(0);
    }

    private sealed class StubSerializer : ISerializer
    {
        public Task<Result<byte[], SerializationError>> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<byte[], SerializationError>.Success(Encoding.UTF8.GetBytes(value?.ToString() ?? string.Empty)));
        }

        public Task<Result<T, SerializationError>> DeserializeAsync<T>(byte[] data, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IReadOnlyDictionary<string, object> GetCapabilities() => new Dictionary<string, object>();
    }

    private sealed class StubEncryptionProvider : IEncryptionProvider
    {
        public IEncryptor GetEncryptor() => new StubEncryptor();

        public IDictionary<string, object> GetProviderInfo() => new Dictionary<string, object>();
    }

    private sealed class StubEncryptor : IEncryptor
    {
        public Task<Result<byte[], SerializationError>> EncryptAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            var encrypted = new byte[data.Length + 1];
            encrypted[0] = 0x7F;
            Buffer.BlockCopy(data, 0, encrypted, 1, data.Length);
            return Task.FromResult(Result<byte[], SerializationError>.Success(encrypted));
        }

        public Task<Result<byte[], SerializationError>> DecryptAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
