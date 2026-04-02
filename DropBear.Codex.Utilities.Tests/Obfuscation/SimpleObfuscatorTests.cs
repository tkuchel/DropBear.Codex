using DropBear.Codex.Utilities.Obfuscation;
using FluentAssertions;

namespace DropBear.Codex.Utilities.Tests.Obfuscation;

public sealed class SimpleObfuscatorTests
{
    [Fact]
    public void Decode_ShouldDescribeShortPayloadFailuresAsCorruptionDetection()
    {
        var decodeResult = SimpleObfuscator.Decode("0");

        decodeResult.IsSuccess.Should().BeFalse();
        decodeResult.Error.Should().NotBeNull();
        decodeResult.Error!.Message.Should().Contain("corruption detection");
        decodeResult.Error.Message.Should().NotContain("integrity");
    }
}
