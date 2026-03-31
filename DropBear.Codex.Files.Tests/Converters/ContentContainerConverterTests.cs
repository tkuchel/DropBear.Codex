using System.Text.Json;
using System.Runtime.Versioning;
using DropBear.Codex.Files.Converters;
using DropBear.Codex.Files.Models;
using DropBear.Codex.Serialization.Providers;
using DropBear.Codex.Serialization.Serializers;
using FluentAssertions;
using SystemTextJsonSerializer = System.Text.Json.JsonSerializer;
using CodexJsonSerializer = DropBear.Codex.Serialization.Serializers.JsonSerializer;

namespace DropBear.Codex.Files.Tests.Converters;

[SupportedOSPlatform("windows")]
public sealed class ContentContainerConverterTests
{
    [Fact]
    public void TypeConverter_ShouldWriteRegisteredProviderIdentifier()
    {
        var converter = new TypeConverter();
        var options = new JsonSerializerOptions();
        options.Converters.Add(converter);

        var json = SystemTextJsonSerializer.Serialize(typeof(CodexJsonSerializer), options);

        json.Should().Be("\"serializer:json\"");
    }

    [Fact]
    public void TypeConverter_ShouldRejectUnregisteredTypeIdentifiers()
    {
        var converter = new TypeConverter();
        var options = new JsonSerializerOptions();
        options.Converters.Add(converter);

        var resolvedType = SystemTextJsonSerializer.Deserialize<Type>("\"System.String\"", options);

        resolvedType.Should().BeNull();
    }

    [Fact]
    public void ContentContainerConverter_ShouldRoundTripRegisteredProvidersUsingIdentifiers()
    {
        var container = new ContentContainer();
        container.AddProvider("Serializer", typeof(CodexJsonSerializer));
        container.AddProvider("CompressionProvider", typeof(GZipCompressionProvider));
        container.SetData("hello world");

        var options = new JsonSerializerOptions();
        options.Converters.Add(new ContentContainerConverter());

        var json = SystemTextJsonSerializer.Serialize(container, options);
        var roundTripped = SystemTextJsonSerializer.Deserialize<ContentContainer>(json, options);

        roundTripped.Should().NotBeNull();
        roundTripped!.GetProviderIdentifiers().Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Serializer"] = "serializer:json",
            ["CompressionProvider"] = "compression:gzip"
        });
    }

    [Fact]
    public void ContentContainerConverter_ShouldRejectUnknownProviderIdentifiers()
    {
        const string json =
            """
            {
              "flags": 0,
              "contentType": "text/plain",
              "data": "aGVsbG8=",
              "hash": null,
              "providers": {
                "Serializer": "serializer:not-allowed"
              }
            }
            """;

        var options = new JsonSerializerOptions();
        options.Converters.Add(new ContentContainerConverter());

        var act = () => SystemTextJsonSerializer.Deserialize<ContentContainer>(json, options);

        act.Should().Throw<JsonException>().WithMessage("*not allowed*");
    }
}
