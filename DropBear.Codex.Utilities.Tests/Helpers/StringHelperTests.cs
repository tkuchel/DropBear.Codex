using DropBear.Codex.Utilities.Helpers;
using FluentAssertions;

namespace DropBear.Codex.Utilities.Tests.Helpers;

/// <summary>
///     Tests for StringHelper utility methods.
/// </summary>
public sealed class StringHelperTests
{
    #region FirstCharToUpper Tests

    [Fact]
    public void FirstCharToUpper_WithValidString_ShouldCapitalizeFirstChar()
    {
        // Arrange
        const string input = "hello world";

        // Act
        var result = StringHelper.FirstCharToUpper(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello world");
    }

    [Fact]
    public void FirstCharToUpper_WithAlreadyCapitalized_ShouldRemainCapitalized()
    {
        // Arrange
        const string input = "Hello World";

        // Act
        var result = StringHelper.FirstCharToUpper(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello World");
    }

    [Fact]
    public void FirstCharToUpper_WithSingleChar_ShouldCapitalize()
    {
        // Arrange
        const string input = "a";

        // Act
        var result = StringHelper.FirstCharToUpper(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("A");
    }

    [Fact]
    public void FirstCharToUpper_WithEmptyString_ShouldReturnFailure()
    {
        // Arrange
        const string input = "";

        // Act
        var result = StringHelper.FirstCharToUpper(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public void FirstCharToUpper_WithNumber_ShouldRemainUnchanged()
    {
        // Arrange
        const string input = "123abc";

        // Act
        var result = StringHelper.FirstCharToUpper(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("123abc");
    }

    #endregion

    #region ToSha256 Tests

    [Fact]
    public void ToSha256_WithValidString_ShouldGenerateHash()
    {
        // Arrange
        const string input = "hello";

        // Act
        var result = StringHelper.ToSha256(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().HaveLength(64); // SHA256 produces 32 bytes = 64 hex chars
    }

    [Fact]
    public void ToSha256_WithSameInput_ShouldProduceSameHash()
    {
        // Arrange
        const string input = "test";

        // Act
        var result1 = StringHelper.ToSha256(input);
        var result2 = StringHelper.ToSha256(input);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Should().Be(result2.Value);
    }

    [Fact]
    public void ToSha256_WithDifferentInputs_ShouldProduceDifferentHashes()
    {
        // Arrange
        const string input1 = "hello";
        const string input2 = "world";

        // Act
        var result1 = StringHelper.ToSha256(input1);
        var result2 = StringHelper.ToSha256(input2);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Should().NotBe(result2.Value);
    }

    [Fact]
    public void ToSha256_WithEmptyString_ShouldReturnFailure()
    {
        // Arrange
        const string input = "";

        // Act
        var result = StringHelper.ToSha256(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public void ToSha256_WithUnicodeCharacters_ShouldGenerateHash()
    {
        // Arrange
        const string input = "Hello 世界 🌍";

        // Act
        var result = StringHelper.ToSha256(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().HaveLength(64);
    }

    [Fact]
    public void ToSha256_ShouldProduceUppercaseHex()
    {
        // Arrange
        const string input = "test";

        // Act
        var result = StringHelper.ToSha256(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().MatchRegex("^[A-F0-9]{64}$");
    }

    #endregion

    #region LimitTo Tests

    [Fact]
    public void LimitTo_WithStringLongerThanLimit_ShouldTruncate()
    {
        // Arrange
        const string input = "Hello World";
        const int limit = 5;

        // Act
        var result = StringHelper.LimitTo(input, limit);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello");
    }

    [Fact]
    public void LimitTo_WithStringShorterThanLimit_ShouldReturnOriginal()
    {
        // Arrange
        const string input = "Hi";
        const int limit = 10;

        // Act
        var result = StringHelper.LimitTo(input, limit);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hi");
    }

    [Fact]
    public void LimitTo_WithStringEqualToLimit_ShouldReturnOriginal()
    {
        // Arrange
        const string input = "Hello";
        const int limit = 5;

        // Act
        var result = StringHelper.LimitTo(input, limit);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello");
    }

    [Fact]
    public void LimitTo_WithEmptyString_ShouldReturnFailure()
    {
        // Arrange
        const string input = "";
        const int limit = 10;

        // Act
        var result = StringHelper.LimitTo(input, limit);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public void LimitTo_WithZeroLimit_ShouldReturnEmptyString()
    {
        // Arrange
        const string input = "Hello";
        const int limit = 0;

        // Act
        var result = StringHelper.LimitTo(input, limit);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void LimitTo_WithNegativeLimit_ShouldReturnFailure()
    {
        // Arrange
        const string input = "Hello";
        const int limit = -5;

        // Act
        var result = StringHelper.LimitTo(input, limit);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("Failed to limit string length");
    }

    [Fact]
    public void LimitTo_WithUnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        const string input = "Hello 世界🌍";
        const int limit = 8;

        // Act
        var result = StringHelper.LimitTo(input, limit);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Length.Should().BeLessThanOrEqualTo(limit);
    }

    #endregion
}
