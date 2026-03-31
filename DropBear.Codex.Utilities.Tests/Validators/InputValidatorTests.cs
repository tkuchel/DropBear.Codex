using DropBear.Codex.Utilities.Errors;
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

        var result = method!.Invoke(null, ["<script>alert(1)</script>", "html"]);
        result.Should().NotBeNull();

        var resultType = result!.GetType();
        var isSuccess = (bool?)resultType.GetProperty("IsSuccess")?.GetValue(result);
        var error = resultType.GetProperty("Error")?.GetValue(result);

        isSuccess.Should().BeFalse();
        error.Should().NotBeNull();
        error.Should().BeOfType<UtilityError>();

        if (error is not UtilityError utilityError)
        {
            throw new InvalidOperationException("Expected error to be of type UtilityError.");
        }

        utilityError.Message.Should().Contain("suspicious");
    }

    [Fact]
    public void ValidateFilePath_ShouldReject_SiblingPrefixPathsOutsideBaseDirectory()
    {
        var tempPath = Path.GetTempPath();
        var basePath = Path.GetFullPath("allowed", tempPath);
        var siblingPath = Path.GetFullPath(
            $"allowed2{Path.DirectorySeparatorChar}file.txt",
            tempPath);

        var result = InputValidator.ValidateFilePath(siblingPath, basePath);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("outside allowed directory");
    }
}
