using System.Runtime.Versioning;
using DropBear.Codex.Files.Models;
using FluentAssertions;

namespace DropBear.Codex.Files.Tests.Models;

[SupportedOSPlatform("windows")]
public sealed class ContentContainerTests
{
    [Fact]
    public async Task GetDataAsync_ShouldDescribeHashFailuresAsCorruptionDetection()
    {
        var container = new ContentContainer();
        container.SetData("hello world").IsSuccess.Should().BeTrue();
        container.Data![0] ^= 0x01;

        var result = await container.GetDataAsync<string>();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("corruption");
        result.Error.Message.Should().NotContain("integrity");
    }
}
