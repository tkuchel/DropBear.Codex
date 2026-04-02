using System.Security.Cryptography;
using System.Text;
using DropBear.Codex.Blazor.Helpers;
using FluentAssertions;

namespace DropBear.Codex.Blazor.Tests.Helpers;

public sealed class AntiForgeryHelperTests
{
    private static readonly byte[] TestSigningKey = SHA256.HashData(Encoding.UTF8.GetBytes("dropbear-antiforgery-test-key"));

    [Fact]
    public void GenerateToken_ShouldValidate_WhenSignedWithConfiguredKey()
    {
        AntiForgeryHelper.Options.ConfigureSigningKey(TestSigningKey);

        var token = AntiForgeryHelper.GenerateToken("user-123", "session-456");
        var validationResult = AntiForgeryHelper.ValidateToken(token, "user-123", "session-456");

        validationResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_ShouldReject_LegacyForgeableTokenFormat()
    {
        AntiForgeryHelper.Options.ConfigureSigningKey(TestSigningKey);

        var forgedToken = CreateLegacyToken("user-123", "session-456");
        var validationResult = AntiForgeryHelper.ValidateToken(forgedToken, "user-123", "session-456");

        validationResult.IsSuccess.Should().BeFalse();
    }

    private static string CreateLegacyToken(string userId, string? sessionId)
    {
        var timestamp = DateTime.UtcNow.ToBinary();
        var timestampBytes = BitConverter.GetBytes(timestamp);
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var tokenBytes = RandomNumberGenerator.GetBytes(32);

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
        var finalToken = new byte[timestampBytes.Length + saltBytes.Length + tokenBytes.Length + hash.Length];
        var finalTokenSpan = finalToken.AsSpan();

        timestampBytes.CopyTo(finalTokenSpan);
        saltBytes.CopyTo(finalTokenSpan[timestampBytes.Length..]);
        tokenBytes.CopyTo(finalTokenSpan[(timestampBytes.Length + saltBytes.Length)..]);
        hash.CopyTo(finalTokenSpan[(timestampBytes.Length + saltBytes.Length + tokenBytes.Length)..]);

        return Convert.ToBase64String(finalToken);
    }
}
