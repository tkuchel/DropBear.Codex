using System.Runtime.Versioning;
using DropBear.Codex.Serialization.Providers;
using FluentAssertions;

namespace DropBear.Codex.Serialization.Tests.Providers;

[SupportedOSPlatform("windows")]
public sealed class RSAKeyProviderTests : IDisposable
{
    private readonly string _testDirectory = Path.GetFullPath(
        Path.Join(Path.GetTempPath(), "DropBear.Codex.Serialization.Tests", Guid.NewGuid().ToString("N")));

    [Fact]
    public void GetRsaProvider_ShouldPersist_PrivatePemUsingProtectedEnvelope()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(_testDirectory);
        var publicKeyPath = Path.Combine(_testDirectory, "public.pem");
        var privateKeyPath = Path.Combine(_testDirectory, "private.pem");
        var provider = new RSAKeyProvider(publicKeyPath, privateKeyPath);

        using var rsa = provider.GetRsaProvider();
        var persistedPrivateKey = File.ReadAllText(privateKeyPath);

        persistedPrivateKey.Should().Contain("BEGIN DPAPI PROTECTED PRIVATE KEY");
        persistedPrivateKey.Should().NotContain("<RSAKeyValue>");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
