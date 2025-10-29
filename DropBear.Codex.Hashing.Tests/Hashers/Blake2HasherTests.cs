using System.Text;
using DropBear.Codex.Hashing.Hashers;
using FluentAssertions;

namespace DropBear.Codex.Hashing.Tests.Hashers;

/// <summary>
///     Tests for Blake2Hasher functionality.
/// </summary>
public sealed class Blake2HasherTests
{
    #region Hash Tests

    [Fact]
    public void Hash_WithValidInput_ShouldGenerateHash()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test input";

        // Act
        var result = hasher.Hash(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Hash_WithEmptyInput_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "";

        // Act
        var result = hasher.Hash(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public void Hash_WithSameInputAndHasher_ShouldProduceSameHash()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test input";

        // Act
        var result1 = hasher.Hash(input);
        var result2 = hasher.Hash(input);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        // Same hasher instance reuses salt, producing same hash
        result1.Value.Should().Be(result2.Value);
    }

    [Fact]
    public void Hash_WithDifferentHasherInstances_ShouldProduceDifferentHashes()
    {
        // Arrange
        var hasher1 = new Blake2Hasher();
        var hasher2 = new Blake2Hasher();
        const string input = "test input";

        // Act
        var result1 = hasher1.Hash(input);
        var result2 = hasher2.Hash(input);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        // Different hasher instances generate different salts, producing different hashes
        result1.Value.Should().NotBe(result2.Value);
    }

    [Fact]
    public async Task HashAsync_WithValidInput_ShouldGenerateHash()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test input";

        // Act
        var result = await hasher.HashAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HashAsync_WithEmptyInput_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "";

        // Act
        var result = await hasher.HashAsync(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task HashAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test input";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await hasher.HashAsync(input, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithMatchingHash_ShouldReturnSuccess()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test input";
        var hashResult = hasher.Hash(input);
        var hash = hashResult.Value!;

        // Act
        var result = hasher.Verify(input, hash);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithNonMatchingHash_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test input";
        const string wrongInput = "wrong input";
        var hashResult = hasher.Hash(input);
        var hash = hashResult.Value!;

        // Act
        var result = hasher.Verify(wrongInput, hash);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Verify_WithEmptyInput_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string expectedHash = "someHash";

        // Act
        var result = hasher.Verify("", expectedHash);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Verify_WithEmptyExpectedHash_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test";

        // Act
        var result = hasher.Verify(input, "");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Verify_WithInvalidBase64Hash_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test";
        const string invalidHash = "not-valid-base64!!!";

        // Act
        var result = hasher.Verify(input, invalidHash);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyAsync_WithMatchingHash_ShouldReturnSuccess()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test input";
        var hashResult = await hasher.HashAsync(input);
        var hash = hashResult.Value!;

        // Act
        var result = await hasher.VerifyAsync(input, hash);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_WithNonMatchingHash_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        const string input = "test input";
        const string wrongInput = "wrong input";
        var hashResult = await hasher.HashAsync(input);
        var hash = hashResult.Value!;

        // Act
        var result = await hasher.VerifyAsync(wrongInput, hash);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region EncodeToBase64Hash Tests

    [Fact]
    public void EncodeToBase64Hash_WithValidData_ShouldGenerateHash()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        var data = Encoding.UTF8.GetBytes("test data");

        // Act
        var result = hasher.EncodeToBase64Hash(data);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EncodeToBase64Hash_WithEmptyData_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        var data = Array.Empty<byte>();

        // Act
        var result = hasher.EncodeToBase64Hash(data);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void EncodeToBase64Hash_WithNullData_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var result = hasher.EncodeToBase64Hash(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void EncodeToBase64Hash_WithSameData_ShouldProduceSameHash()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        var data = Encoding.UTF8.GetBytes("test data");

        // Act
        var result1 = hasher.EncodeToBase64Hash(data);
        var result2 = hasher.EncodeToBase64Hash(data);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        // Should produce same hash for same data
        result1.Value.Should().Be(result2.Value);
    }

    [Fact]
    public async Task EncodeToBase64HashAsync_WithValidData_ShouldGenerateHash()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        var data = Encoding.UTF8.GetBytes("test data");

        // Act
        var result = await hasher.EncodeToBase64HashAsync(data);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EncodeToBase64HashAsync_WithEmptyData_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        var data = ReadOnlyMemory<byte>.Empty;

        // Act
        var result = await hasher.EncodeToBase64HashAsync(data);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    #endregion

    #region WithSalt Tests

    [Fact]
    public void WithSaltValidated_WithValidSalt_ShouldReturnSuccess()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        var salt = new byte[32];
        Random.Shared.NextBytes(salt);

        // Act
        var result = hasher.WithSaltValidated(salt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void WithSaltValidated_WithNullSalt_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var result = hasher.WithSaltValidated(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public void WithSaltValidated_WithEmptySalt_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        var salt = Array.Empty<byte>();

        // Act
        var result = hasher.WithSaltValidated(salt);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void WithSalt_WithValidSalt_ShouldConfigureSalt()
    {
        // Arrange
        var hasher = new Blake2Hasher();
        var salt = new byte[32];
        Random.Shared.NextBytes(salt);

        // Act
        var configuredHasher = hasher.WithSalt(salt);

        // Assert
        configuredHasher.Should().NotBeNull();
        configuredHasher.Should().BeSameAs(hasher);
    }

    [Fact]
    public void WithSalt_WithNullSalt_ShouldThrowArgumentException()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var act = () => hasher.WithSalt(null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region WithHashSize Tests

    [Fact]
    public void WithHashSizeValidated_WithValidSize_ShouldReturnSuccess()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var result = hasher.WithHashSizeValidated(64);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void WithHashSizeValidated_WithZeroSize_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var result = hasher.WithHashSizeValidated(0);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("at least 1 byte");
    }

    [Fact]
    public void WithHashSizeValidated_WithNegativeSize_ShouldReturnFailure()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var result = hasher.WithHashSizeValidated(-5);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void WithHashSize_WithValidSize_ShouldConfigureSize()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var configuredHasher = hasher.WithHashSize(64);

        // Assert
        configuredHasher.Should().NotBeNull();
        configuredHasher.Should().BeSameAs(hasher);
    }

    [Fact]
    public void WithHashSize_WithInvalidSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var act = () => hasher.WithHashSize(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region WithIterations Tests

    [Fact]
    public void WithIterationsValidated_ShouldReturnSuccessWithoutEffect()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var result = hasher.WithIterationsValidated(1000);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        // WithIterations is not applicable for Blake2, but should not fail
    }

    [Fact]
    public void WithIterations_ShouldNotThrow()
    {
        // Arrange
        var hasher = new Blake2Hasher();

        // Act
        var configuredHasher = hasher.WithIterations(1000);

        // Assert
        configuredHasher.Should().NotBeNull();
        configuredHasher.Should().BeSameAs(hasher);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Blake2Hasher_WithConfiguration_ShouldWorkEndToEnd()
    {
        // Arrange
        var salt = new byte[32];
        Random.Shared.NextBytes(salt);

        var hasher = new Blake2Hasher();
        hasher.WithSalt(salt).WithHashSize(32);

        const string input = "integration test";

        // Act
        var hashResult = hasher.Hash(input);
        var verifyResult = hasher.Verify(input, hashResult.Value!);

        // Assert
        hashResult.IsSuccess.Should().BeTrue();
        verifyResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Blake2Hasher_AsyncOperations_ShouldWorkEndToEnd()
    {
        // Arrange
        var salt = new byte[32];
        Random.Shared.NextBytes(salt);

        var hasher = new Blake2Hasher();
        hasher.WithSalt(salt);

        const string input = "async integration test";

        // Act
        var hashResult = await hasher.HashAsync(input);
        var verifyResult = await hasher.VerifyAsync(input, hashResult.Value!);

        // Assert
        hashResult.IsSuccess.Should().BeTrue();
        verifyResult.IsSuccess.Should().BeTrue();
    }

    #endregion
}
