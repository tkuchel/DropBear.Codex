using DropBear.Codex.Serialization.ConfigurationPresets;
using DropBear.Codex.Serialization.Serializers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IO;

namespace DropBear.Codex.Serialization.Tests.Serializers;

/// <summary>
///     Tests for JsonSerializer functionality.
/// </summary>
public sealed class JsonSerializerTests
{
    #region Test Models

    private sealed record TestPerson(string Name, int Age, string? Email = null);

    #endregion

    #region SerializeAsync Tests

    [Fact]
    public async Task SerializeAsync_WithValidObject_ShouldReturnSerializedData()
    {
        // Arrange
        var serializer = CreateSerializer();
        var person = new TestPerson("John Doe", 30, "john@example.com");

        // Act
        var result = await serializer.SerializeAsync(person);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SerializeAsync_WithNullValue_ShouldReturnEmptyArray()
    {
        // Arrange
        var serializer = CreateSerializer();
        TestPerson? person = null;

        // Act
        var result = await serializer.SerializeAsync(person);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task SerializeAsync_WithSimpleTypes_ShouldSerialize()
    {
        // Arrange
        var serializer = CreateSerializer();
        const int number = 42;

        // Act
        var result = await serializer.SerializeAsync(number);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SerializeAsync_WithStringValue_ShouldSerialize()
    {
        // Arrange
        var serializer = CreateSerializer();
        const string text = "Hello, World!";

        // Act
        var result = await serializer.SerializeAsync(text);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SerializeAsync_WithCollection_ShouldSerialize()
    {
        // Arrange
        var serializer = CreateSerializer();
        var people = new List<TestPerson>
        {
            new("Alice", 25),
            new("Bob", 30),
            new("Charlie", 35)
        };

        // Act
        var result = await serializer.SerializeAsync(people);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    #endregion

    #region DeserializeAsync Tests

    [Fact]
    public async Task DeserializeAsync_WithValidData_ShouldReturnObject()
    {
        // Arrange
        var serializer = CreateSerializer();
        var person = new TestPerson("John Doe", 30, "john@example.com");
        var serializedResult = await serializer.SerializeAsync(person);
        var serializedData = serializedResult.Value!;

        // Act
        var result = await serializer.DeserializeAsync<TestPerson>(serializedData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("John Doe");
        result.Value.Age.Should().Be(30);
        result.Value.Email.Should().Be("john@example.com");
    }

    [Fact]
    public async Task DeserializeAsync_WithEmptyData_ShouldReturnFailure()
    {
        // Arrange
        var serializer = CreateSerializer();
        var emptyData = Array.Empty<byte>();

        // Act
        var result = await serializer.DeserializeAsync<TestPerson>(emptyData);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task DeserializeAsync_WithInvalidData_ShouldReturnFailure()
    {
        // Arrange
        var serializer = CreateSerializer();
        var invalidData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await serializer.DeserializeAsync<TestPerson>(invalidData);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task DeserializeAsync_WithCollection_ShouldDeserialize()
    {
        // Arrange
        var serializer = CreateSerializer();
        var people = new List<TestPerson>
        {
            new("Alice", 25),
            new("Bob", 30),
            new("Charlie", 35)
        };
        var serializedResult = await serializer.SerializeAsync(people);
        var serializedData = serializedResult.Value!;

        // Act
        var result = await serializer.DeserializeAsync<List<TestPerson>>(serializedData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);
        result.Value![0].Name.Should().Be("Alice");
        result.Value[1].Name.Should().Be("Bob");
        result.Value[2].Name.Should().Be("Charlie");
    }

    [Fact]
    public async Task DeserializeAsync_WithSimpleType_ShouldDeserialize()
    {
        // Arrange
        var serializer = CreateSerializer();
        const int number = 42;
        var serializedResult = await serializer.SerializeAsync(number);
        var serializedData = serializedResult.Value!;

        // Act
        var result = await serializer.DeserializeAsync<int>(serializedData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    #endregion

    #region RoundTrip Tests

    [Fact]
    public async Task SerializeAndDeserialize_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var serializer = CreateSerializer();
        var original = new TestPerson("Jane Doe", 28, "jane@example.com");

        // Act
        var serializeResult = await serializer.SerializeAsync(original);
        var deserializeResult = await serializer.DeserializeAsync<TestPerson>(serializeResult.Value!);

        // Assert
        serializeResult.IsSuccess.Should().BeTrue();
        deserializeResult.IsSuccess.Should().BeTrue();
        deserializeResult.Value.Should().NotBeNull();
        deserializeResult.Value!.Name.Should().Be(original.Name);
        deserializeResult.Value.Age.Should().Be(original.Age);
        deserializeResult.Value.Email.Should().Be(original.Email);
    }

    [Fact]
    public async Task SerializeAndDeserialize_WithComplexObject_ShouldPreserveData()
    {
        // Arrange
        var serializer = CreateSerializer();
        var original = new
        {
            Name = "Test",
            Age = 42,
            IsActive = true,
            Tags = new[] { "tag1", "tag2", "tag3" },
            Metadata = new Dictionary<string, object>
            {
                ["key1"] = "value1",
                ["key2"] = 123
            }
        };

        // Act
        var serializeResult = await serializer.SerializeAsync(original);
        var deserializeResult = await serializer.DeserializeAsync<dynamic>(serializeResult.Value!);

        // Assert
        serializeResult.IsSuccess.Should().BeTrue();
        deserializeResult.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region GetCapabilities Tests

    [Fact]
    public void GetCapabilities_ShouldReturnValidCapabilities()
    {
        // Arrange
        var serializer = CreateSerializer();

        // Act
        var capabilities = serializer.GetCapabilities();

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.Should().ContainKey("SerializerType");
        capabilities["SerializerType"].Should().Be("JSON");
        capabilities.Should().ContainKey("WriteIndented");
        capabilities.Should().ContainKey("MaxDepth");
        capabilities.Should().ContainKey("IsThreadSafe");
        capabilities["IsThreadSafe"].Should().Be(true);
    }

    #endregion

    #region Helper Methods

    private static JsonSerializer CreateSerializer()
    {
        var config = new SerializationConfig
        {
            JsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            },
            RecyclableMemoryStreamManager = new RecyclableMemoryStreamManager(),
            BufferSize = 4096,
            EnableCaching = false,
            MaxCacheSize = 100
        };

        var logger = NullLogger<JsonSerializer>.Instance;
        return new JsonSerializer(config, logger);
    }

    #endregion
}
