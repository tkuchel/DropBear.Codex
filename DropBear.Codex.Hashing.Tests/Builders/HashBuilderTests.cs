using DropBear.Codex.Hashing;
using DropBear.Codex.Hashing.Hashers;
using DropBear.Codex.Hashing.Interfaces;
using FluentAssertions;

namespace DropBear.Codex.Hashing.Tests.Builders;

/// <summary>
///     Tests for HashBuilder functionality.
/// </summary>
public sealed class HashBuilderTests
{
    #region GetHasher Tests

    [Fact]
    public void GetHasher_WithValidKey_ShouldReturnHasher()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var hasher = builder.GetHasher("blake2");

        // Assert
        hasher.Should().NotBeNull();
        hasher.Should().BeAssignableTo<IHasher>();
        hasher.Should().BeOfType<Blake2Hasher>();
    }

    [Theory]
    [InlineData("argon2")]
    [InlineData("blake2")]
    [InlineData("blake3")]
    [InlineData("fnv1a")]
    [InlineData("murmur3")]
    [InlineData("siphash")]
    [InlineData("xxhash")]
    [InlineData("extended_blake3")]
    public void GetHasher_WithBuiltInKeys_ShouldReturnCorrectHasher(string key)
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var hasher = builder.GetHasher(key);

        // Assert
        hasher.Should().NotBeNull();
        hasher.Should().BeAssignableTo<IHasher>();
    }

    [Fact]
    public void GetHasher_WithInvalidKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.GetHasher("nonexistent");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*No hashing service registered*");
    }

    [Fact]
    public void GetHasher_WithNullKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.GetHasher(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*key cannot be null or empty*");
    }

    [Fact]
    public void GetHasher_WithEmptyKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.GetHasher(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*key cannot be null or empty*");
    }

    [Fact]
    public void GetHasher_WithWhitespaceKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.GetHasher("   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*key cannot be null or empty*");
    }

    #endregion

    #region TryGetHasher Tests

    [Fact]
    public void TryGetHasher_WithValidKey_ShouldReturnSuccess()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var result = builder.TryGetHasher("blake2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeOfType<Blake2Hasher>();
    }

    [Fact]
    public void TryGetHasher_WithInvalidKey_ShouldReturnFailure()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var result = builder.TryGetHasher("nonexistent");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("No hashing service registered");
    }

    [Fact]
    public void TryGetHasher_WithNullKey_ShouldReturnFailure()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var result = builder.TryGetHasher(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public void TryGetHasher_WithEmptyKey_ShouldReturnFailure()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var result = builder.TryGetHasher(string.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    #endregion

    #region RegisterHasher Tests

    [Fact]
    public void RegisterHasher_WithValidKeyAndFactory_ShouldRegisterSuccessfully()
    {
        // Arrange
        var builder = new HashBuilder();
        var customKey = "custom_blake2";

        // Act
        builder.RegisterHasher(customKey, () => new Blake2Hasher());
        var hasher = builder.GetHasher(customKey);

        // Assert
        hasher.Should().NotBeNull();
        hasher.Should().BeOfType<Blake2Hasher>();
    }

    [Fact]
    public void RegisterHasher_WithNullKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.RegisterHasher(null!, () => new Blake2Hasher());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterHasher_WithNullFactory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.RegisterHasher("custom", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterHasher_OverwritingExisting_ShouldReplaceHasher()
    {
        // Arrange
        var builder = new HashBuilder();
        var key = "custom";

        // Act
        builder.RegisterHasher(key, () => new Blake2Hasher());
        var firstHasher = builder.GetHasher(key);

        builder.RegisterHasher(key, () => new Blake3Hasher());
        var secondHasher = builder.GetHasher(key);

        // Assert
        firstHasher.Should().BeOfType<Blake2Hasher>();
        secondHasher.Should().BeOfType<Blake3Hasher>();
    }

    #endregion

    #region EnablePoolingForHasher Tests

    [Fact]
    public void EnablePoolingForHasher_WithValidKey_ShouldEnablePooling()
    {
        // Arrange
        var builder = new HashBuilder();
        var key = "blake2";

        // Act
        builder.EnablePoolingForHasher(key, maxPoolSize: 10);
        var hasher1 = builder.GetHasher(key);
        var hasher2 = builder.GetHasher(key);

        // Assert
        hasher1.Should().NotBeNull();
        hasher2.Should().NotBeNull();
        // Pooled hashers may or may not be the same instance depending on return timing
    }

    [Fact]
    public void EnablePoolingForHasher_WithInvalidKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.EnablePoolingForHasher("nonexistent");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*No hasher registered*");
    }

    [Fact]
    public void EnablePoolingForHasher_WithNullKey_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.EnablePoolingForHasher(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnablePoolingForHasher_WithZeroPoolSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.EnablePoolingForHasher("blake2", maxPoolSize: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Pool size must be positive*");
    }

    [Fact]
    public void EnablePoolingForHasher_WithNegativePoolSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.EnablePoolingForHasher("blake2", maxPoolSize: -5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Pool size must be positive*");
    }

    #endregion

    #region GetPooledHasher Tests

    [Fact]
    public void GetPooledHasher_WithValidKey_ShouldReturnPooledHasher()
    {
        // Arrange
        var builder = new HashBuilder();
        builder.EnablePoolingForHasher("blake2");

        // Act
        var result = builder.GetPooledHasher("blake2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Hasher.Should().NotBeNull();
        result.Value!.Hasher.Should().BeOfType<Blake2Hasher>();
    }

    [Fact]
    public void GetPooledHasher_WithInvalidKey_ShouldReturnFailure()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var result = builder.GetPooledHasher("nonexistent");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void GetPooledHasher_Dispose_ShouldReturnHasherToPool()
    {
        // Arrange
        var builder = new HashBuilder();
        builder.EnablePoolingForHasher("blake2");

        // Act
        var result = builder.GetPooledHasher("blake2");
        result.Value!.Dispose();

        // Assert - should not throw after disposal
        var secondResult = builder.GetPooledHasher("blake2");
        secondResult.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ReturnHasher Tests

    [Fact]
    public void ReturnHasher_WithValidPooledHasher_ShouldReturnToPool()
    {
        // Arrange
        var builder = new HashBuilder();
        builder.EnablePoolingForHasher("blake2");
        var hasher = builder.GetHasher("blake2");

        // Act
        builder.ReturnHasher("blake2", hasher);

        // Assert - should not throw
        var nextHasher = builder.GetHasher("blake2");
        nextHasher.Should().NotBeNull();
    }

    [Fact]
    public void ReturnHasher_WithNullKey_ShouldNotThrow()
    {
        // Arrange
        var builder = new HashBuilder();
        var hasher = builder.GetHasher("blake2");

        // Act
        var act = () => builder.ReturnHasher(null!, hasher);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ReturnHasher_WithNullHasher_ShouldNotThrow()
    {
        // Arrange
        var builder = new HashBuilder();

        // Act
        var act = () => builder.ReturnHasher("blake2", null!);

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
