using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Serialization.Encryption;
using FluentAssertions;
using Microsoft.IO;

namespace DropBear.Codex.Serialization.Tests.Encryption;

[SupportedOSPlatform("windows")]
public sealed class AESCNGEncryptorTests
{
    [Fact]
    public async Task EncryptAsync_ThenDecryptAsync_ShouldRoundTripPlaintext()
    {
        using var rsa = RSA.Create(2048);
        using var encryptor = new AESCNGEncryptor(rsa, new RecyclableMemoryStreamManager());
        var plaintext = Encoding.UTF8.GetBytes("authenticated payload");

        var encrypted = await encryptor.EncryptAsync(plaintext);
        var decrypted = await encryptor.DecryptAsync(encrypted.Value!);

        encrypted.IsSuccess.Should().BeTrue();
        decrypted.IsSuccess.Should().BeTrue();
        decrypted.Value.Should().Equal(plaintext);
    }

    [Fact]
    public async Task DecryptAsync_ShouldFail_WhenCiphertextIsTampered()
    {
        using var rsa = RSA.Create(2048);
        using var encryptor = new AESCNGEncryptor(rsa, new RecyclableMemoryStreamManager());
        var plaintext = Encoding.UTF8.GetBytes("authenticated payload");

        var encrypted = await encryptor.EncryptAsync(plaintext);
        var tampered = encrypted.Value!.ToArray();
        tampered[^1] ^= 0x01;

        var decrypted = await encryptor.DecryptAsync(tampered);

        decrypted.IsSuccess.Should().BeFalse();
        decrypted.Error!.Message.Should().Contain("authentication");
    }
}
