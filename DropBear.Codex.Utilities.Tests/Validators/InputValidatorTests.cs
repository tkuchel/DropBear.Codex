using DropBear.Codex.Utilities.Validators;
using FluentAssertions;

namespace DropBear.Codex.Utilities.Tests.Validators;

public sealed class InputValidatorTests
{
    [Fact]
    public void ValidateSafe_ShouldFlag_SuspiciousPatternsWithoutClaimingTheyAreSafe()
    {
        var method = typeof(InputValidator).GetMethod(nameof(InputValidator.ValidateSafe), [typeof(string), typeof(string)]);

        method.Should().NotBeNull();

        dynamic result = method!.Invoke(null, ["<script>alert(1)</script>", "html"])!;

        ((bool)result.IsSuccess).Should().BeFalse();
        result.Error.Should().NotBeNull();
        ((string)result.Error!.Message).Should().Contain("suspicious");
    }

    [Fact]
    public void ValidateFilePath_ShouldReject_SiblingPrefixPathsOutsideBaseDirectory()
    {
        var tempPath = Path.GetTempPath();
        var basePath = Path.GetFullPath("allowed", tempPath);
        var siblingPath = Path.GetFullPath(Path.Combine("allowed2", "file.txt"), tempPath);

        var result = InputValidator.ValidateFilePath(siblingPath, basePath);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("outside allowed directory");
    }
}
