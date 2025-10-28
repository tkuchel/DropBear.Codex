using DropBear.Codex.Files.Builders;
using DropBear.Codex.Files.Models;
using FluentAssertions;

namespace DropBear.Codex.Files.Tests.Builders;

/// <summary>
///     Tests for DropBearFileBuilder functionality.
/// </summary>
public sealed class DropBearFileBuilderTests
{
    #region WithFileName Tests

    [Fact]
    public void WithFileName_WithValidName_ShouldSetFileName()
    {
        // Arrange
        var builder = new DropBearFileBuilder();
        const string fileName = "test.dbf";

        // Act
        var result = builder.WithFileName(fileName);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithFileName_WithNullName_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new DropBearFileBuilder();

        // Act
        var act = () => builder.WithFileName(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*File name cannot be null or empty*");
    }

    [Fact]
    public void WithFileName_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new DropBearFileBuilder();

        // Act
        var act = () => builder.WithFileName(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*File name cannot be null or empty*");
    }

    #endregion

    #region WithVersion Tests

    [Fact]
    public void WithVersion_WithValidVersion_ShouldSetVersion()
    {
        // Arrange
        var builder = new DropBearFileBuilder();
        const string versionLabel = "1.0.0";
        var versionDate = DateTimeOffset.UtcNow;

        // Act
        var result = builder.WithVersion(versionLabel, versionDate);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithVersion_WithNullLabel_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new DropBearFileBuilder();
        var versionDate = DateTimeOffset.UtcNow;

        // Act
        var act = () => builder.WithVersion(null!, versionDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Version label cannot be null or empty*");
    }

    [Fact]
    public void WithVersion_WithEmptyLabel_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new DropBearFileBuilder();
        var versionDate = DateTimeOffset.UtcNow;

        // Act
        var act = () => builder.WithVersion(string.Empty, versionDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Version label cannot be null or empty*");
    }

    #endregion

    #region AddMetadata Tests

    [Fact]
    public void AddMetadata_WithValidKeyValue_ShouldAddMetadata()
    {
        // Arrange
        var builder = new DropBearFileBuilder();
        const string key = "Author";
        const string value = "Test User";

        // Act
        var result = builder.AddMetadata(key, value);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddMetadata_WithMultipleEntries_ShouldAddAll()
    {
        // Arrange
        var builder = new DropBearFileBuilder();

        // Act
        var result = builder
            .AddMetadata("Author", "Test User")
            .AddMetadata("Description", "Test File")
            .AddMetadata("Tags", "test,file,example");

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddMetadata_WithNullKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new DropBearFileBuilder();

        // Act
        var act = () => builder.AddMetadata(null!, "value");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddMetadata_WithNullValue_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new DropBearFileBuilder();

        // Act
        var act = () => builder.AddMetadata("key", null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
