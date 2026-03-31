using DropBear.Codex.Utilities.Validators;
using FluentAssertions;

namespace DropBear.Codex.Utilities.Tests.Validators;

public sealed class InputValidatorTests
{
    [Fact]
    public void ValidateSafe_ShouldFlag_SuspiciousPatternsWithoutClaimingTheyAreSafe()
    {
#pragma warning disable CS0618
        var result = InputValidator.ValidateSafe("<script>alert(1)</script>", "html");
#pragma warning restore CS0618

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("suspicious");
    }

    [Fact]
    public void ValidateFilePath_ShouldReject_SiblingPrefixPathsOutsideBaseDirectory()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "allowed");
        var siblingPath = Path.Combine(Path.GetTempPath(), "allowed2", "file.txt");

        var result = InputValidator.ValidateFilePath(siblingPath, basePath);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("outside allowed directory");
    }
}
