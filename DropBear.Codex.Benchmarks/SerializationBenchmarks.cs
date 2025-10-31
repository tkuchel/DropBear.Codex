using BenchmarkDotNet.Attributes;
using DropBear.Codex.Serialization.Factories;
using DropBear.Codex.Serialization.Serializers;
using MessagePack;
using CodexMessagePackSerializer = DropBear.Codex.Serialization.Serializers.MessagePackSerializer;

namespace DropBear.Codex.Benchmarks;

/// <summary>
///     Benchmarks for comparing serialization performance:
///     MessagePack vs JSON vs Encrypted serialization.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 50)]
public class SerializationBenchmarks
{
    private TestData _smallData = null!;
    private TestData _largeData = null!;
    private byte[] _smallDataMsgPackBytes = null!;
    private byte[] _largeDataMsgPackBytes = null!;
    private byte[] _smallDataJsonBytes = null!;
    private byte[] _largeDataJsonBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small object (few properties)
        _smallData = new TestData
        {
            Id = 123,
            Name = "Test",
            Value = 456.789
        };

        // Large object (many properties, nested data)
        _largeData = new TestData
        {
            Id = 123,
            Name = "Large Test Object with Longer Name",
            Value = 456.789,
            Description = new string('X', 1000), // 1KB string
            Tags = Enumerable.Range(0, 100).Select(i => $"Tag{i}").ToArray(),
            Metadata = Enumerable.Range(0, 50).ToDictionary(i => $"Key{i}", i => $"Value{i}")
        };

        // Pre-serialize for deserialization benchmarks - MessagePack
        var msgPackBuilder = new SerializationBuilder();
        msgPackBuilder
            .WithMessagePackSerializerOptions(MessagePack.MessagePackSerializerOptions.Standard)
            .WithSerializer<CodexMessagePackSerializer>();
        var msgPackSerializerResult = msgPackBuilder.Build();
        if (!msgPackSerializerResult.IsSuccess)
            throw new InvalidOperationException($"Failed to build MessagePack serializer: {msgPackSerializerResult.Error?.Message}");
        var msgPackSerializer = msgPackSerializerResult.Value!;

        var smallMsgPackResult = msgPackSerializer.SerializeAsync(_smallData).GetAwaiter().GetResult();
        if (!smallMsgPackResult.IsSuccess)
            throw new InvalidOperationException($"Failed to serialize small data with MessagePack: {smallMsgPackResult.Error?.Message}");
        _smallDataMsgPackBytes = smallMsgPackResult.Value!;

        var largeMsgPackResult = msgPackSerializer.SerializeAsync(_largeData).GetAwaiter().GetResult();
        if (!largeMsgPackResult.IsSuccess)
            throw new InvalidOperationException($"Failed to serialize large data with MessagePack: {largeMsgPackResult.Error?.Message}");
        _largeDataMsgPackBytes = largeMsgPackResult.Value!;

        // Pre-serialize for deserialization benchmarks - JSON
        var jsonBuilder = new SerializationBuilder();
        jsonBuilder.WithSerializer<JsonSerializer>();
        var jsonSerializerResult = jsonBuilder.Build();
        if (!jsonSerializerResult.IsSuccess)
            throw new InvalidOperationException($"Failed to build JSON serializer: {jsonSerializerResult.Error?.Message}");
        var jsonSerializer = jsonSerializerResult.Value!;

        var smallJsonResult = jsonSerializer.SerializeAsync(_smallData).GetAwaiter().GetResult();
        if (!smallJsonResult.IsSuccess)
            throw new InvalidOperationException($"Failed to serialize small data with JSON: {smallJsonResult.Error?.Message}");
        _smallDataJsonBytes = smallJsonResult.Value!;

        var largeJsonResult = jsonSerializer.SerializeAsync(_largeData).GetAwaiter().GetResult();
        if (!largeJsonResult.IsSuccess)
            throw new InvalidOperationException($"Failed to serialize large data with JSON: {largeJsonResult.Error?.Message}");
        _largeDataJsonBytes = largeJsonResult.Value!;
    }

    #region Serialization Benchmarks

    [Benchmark(Baseline = true, Description = "MessagePack: Serialize Small")]
    public async Task<byte[]?> MessagePack_Serialize_Small()
    {
        var serializerBuilder = new SerializationBuilder();
        serializerBuilder
            .WithMessagePackSerializerOptions(MessagePack.MessagePackSerializerOptions.Standard)
            .WithSerializer<CodexMessagePackSerializer>();
        var serializer = serializerBuilder.Build().Value!;

        var result = await serializer.SerializeAsync(_smallData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "JSON: Serialize Small")]
    public async Task<byte[]?> Json_Serialize_Small()
    {
        var serializerBuilder = new SerializationBuilder();
        serializerBuilder.WithSerializer<JsonSerializer>();
        var serializer = serializerBuilder.Build().Value!;

        var result = await serializer.SerializeAsync(_smallData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "MessagePack: Serialize Large")]
    public async Task<byte[]?> MessagePack_Serialize_Large()
    {
        var serializerBuilder = new SerializationBuilder();
        serializerBuilder
            .WithMessagePackSerializerOptions(MessagePack.MessagePackSerializerOptions.Standard)
            .WithSerializer<CodexMessagePackSerializer>();
        var serializer = serializerBuilder.Build().Value!;

        var result = await serializer.SerializeAsync(_largeData);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "JSON: Serialize Large")]
    public async Task<byte[]?> Json_Serialize_Large()
    {
        var serializerBuilder = new SerializationBuilder();
        serializerBuilder.WithSerializer<JsonSerializer>();
        var serializer = serializerBuilder.Build().Value!;

        var result = await serializer.SerializeAsync(_largeData);
        return result.IsSuccess ? result.Value : null;
    }

    #endregion

    #region Deserialization Benchmarks

    [Benchmark(Description = "MessagePack: Deserialize Small")]
    public async Task<TestData?> MessagePack_Deserialize_Small()
    {
        var serializerBuilder = new SerializationBuilder();
        serializerBuilder
            .WithMessagePackSerializerOptions(MessagePack.MessagePackSerializerOptions.Standard)
            .WithSerializer<CodexMessagePackSerializer>();
        var serializer = serializerBuilder.Build().Value!;

        var result = await serializer.DeserializeAsync<TestData>(_smallDataMsgPackBytes);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "JSON: Deserialize Small")]
    public async Task<TestData?> Json_Deserialize_Small()
    {
        var serializerBuilder = new SerializationBuilder();
        serializerBuilder.WithSerializer<JsonSerializer>();
        var serializer = serializerBuilder.Build().Value!;

        var result = await serializer.DeserializeAsync<TestData>(_smallDataJsonBytes);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "MessagePack: Deserialize Large")]
    public async Task<TestData?> MessagePack_Deserialize_Large()
    {
        var serializerBuilder = new SerializationBuilder();
        serializerBuilder
            .WithMessagePackSerializerOptions(MessagePack.MessagePackSerializerOptions.Standard)
            .WithSerializer<CodexMessagePackSerializer>();
        var serializer = serializerBuilder.Build().Value!;

        var result = await serializer.DeserializeAsync<TestData>(_largeDataMsgPackBytes);
        return result.IsSuccess ? result.Value : null;
    }

    [Benchmark(Description = "JSON: Deserialize Large")]
    public async Task<TestData?> Json_Deserialize_Large()
    {
        var serializerBuilder = new SerializationBuilder();
        serializerBuilder.WithSerializer<JsonSerializer>();
        var serializer = serializerBuilder.Build().Value!;

        var result = await serializer.DeserializeAsync<TestData>(_largeDataJsonBytes);
        return result.IsSuccess ? result.Value : null;
    }

    #endregion

    [MessagePackObject]
    public class TestData
    {
        [Key(0)]
        public int Id { get; set; }

        [Key(1)]
        public string Name { get; set; } = string.Empty;

        [Key(2)]
        public double Value { get; set; }

        [Key(3)]
        public string? Description { get; set; }

        [Key(4)]
        public string[]? Tags { get; set; }

        [Key(5)]
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
