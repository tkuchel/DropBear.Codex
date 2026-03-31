using DropBear.Codex.Utilities.AccessCodes;
using FluentAssertions;

namespace DropBear.Codex.Utilities.Tests.AccessCodes;

public sealed class TimeBasedCodeGeneratorTests
{
    [Fact]
    public void GenerateCode_ShouldUseSixDigitDefaultCodes()
    {
        var generator = new TimeBasedCodeGenerator(secretKey: "test-secret");

        var result = generator.GenerateCode(new DateTime(2026, 3, 30, 10, 0, 0, DateTimeKind.Utc));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveLength(6);
    }

    [Fact]
    public void GenerateCode_ShouldRemainStableWithinDefaultThirtySecondWindow()
    {
        var generator = new TimeBasedCodeGenerator(secretKey: "test-secret");
        var start = new DateTime(2026, 3, 30, 10, 0, 5, DateTimeKind.Utc);
        var laterInSameWindow = new DateTime(2026, 3, 30, 10, 0, 25, DateTimeKind.Utc);

        var first = generator.GenerateCode(start);
        var second = generator.GenerateCode(laterInSameWindow);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().Be(first.Value);
    }
}
