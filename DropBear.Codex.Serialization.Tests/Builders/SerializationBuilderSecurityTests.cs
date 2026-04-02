using System.Runtime.Versioning;
using DropBear.Codex.Serialization.Errors;
using DropBear.Codex.Serialization.Extensions;
using DropBear.Codex.Serialization.Factories;
using FluentAssertions;
using MessagePack;
using MessagePack.Resolvers;
using CodexMessagePackSerializer = DropBear.Codex.Serialization.Serializers.MessagePackSerializer;

namespace DropBear.Codex.Serialization.Tests.Builders;

[SupportedOSPlatform("windows")]
public sealed class SerializationBuilderSecurityTests
{
    [Fact]
    public void Build_ShouldRejectMessagePackOptionsUsingStandardResolver()
    {
        var builder = new SerializationBuilder()
            .WithSerializer<CodexMessagePackSerializer>()
            .WithMessagePackSerializerOptions(
                MessagePackSerializerOptions.Standard
                    .WithResolver(StandardResolver.Instance)
                    .WithSecurity(MessagePackSecurity.UntrustedData));

        var result = builder.Build();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("StandardResolver and StandardResolverAllowPrivate are not allowed");
    }

    [Fact]
    public void Build_ShouldRejectMessagePackOptionsUsingStandardResolverAllowPrivate()
    {
        var builder = new SerializationBuilder()
            .WithSerializer<CodexMessagePackSerializer>()
            .WithMessagePackSerializerOptions(
                MessagePackSerializerOptions.Standard
                    .WithResolver(StandardResolverAllowPrivate.Instance)
                    .WithSecurity(MessagePackSecurity.UntrustedData));

        var result = builder.Build();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("StandardResolver and StandardResolverAllowPrivate are not allowed");
    }

    [Fact]
    public void WithDefaultMessagePackSerializerOptions_ShouldUseSafeDefaultResolverComposition()
    {
        var builder = new SerializationBuilder()
            .WithSerializer<CodexMessagePackSerializer>()
            .WithDefaultMessagePackSerializerOptions();

        var result = builder.Build();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.GetCapabilities()["Resolver"].Should().NotBe("StandardResolver");
    }

    [Fact]
    public void WithDefaultMessagePackOptions_ShouldUseSafeDefaultResolverComposition()
    {
        var builder = new SerializationBuilder()
            .WithSerializer<CodexMessagePackSerializer>()
            .WithDefaultMessagePackOptions();

        var result = builder.Build();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.GetCapabilities()["Resolver"].Should().NotBe("StandardResolver");
    }
}
