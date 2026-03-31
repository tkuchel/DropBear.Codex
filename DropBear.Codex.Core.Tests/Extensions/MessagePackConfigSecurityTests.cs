using DropBear.Codex.Core.Extensions;
using FluentAssertions;
using MessagePack.Resolvers;

namespace DropBear.Codex.Core.Tests.Extensions;

public sealed class MessagePackConfigSecurityTests
{
    [Fact]
    public void GetOptions_ShouldUseSafeDefaultResolverComposition()
    {
        var options = MessagePackConfig.GetOptions();

        options.Resolver.Should().NotBeSameAs(StandardResolver.Instance);
        options.Resolver.Should().NotBeSameAs(StandardResolverAllowPrivate.Instance);
        options.Resolver.Should().NotBeNull();
    }

    [Fact]
    public void CreateBuilder_ShouldUseSafeDefaultResolverComposition()
    {
        var options = MessagePackConfig.CreateBuilder().Build();

        options.Resolver.Should().NotBeSameAs(StandardResolver.Instance);
        options.Resolver.Should().NotBeSameAs(StandardResolverAllowPrivate.Instance);
        options.Resolver.Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldRejectPrivateMemberResolver()
    {
        var action = () => MessagePackConfig.CreateBuilder()
            .WithPrivateMemberResolver()
            .Build();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsafe MessagePack resolvers are not allowed*");
    }

    [Fact]
    public void Build_ShouldRejectStandardResolver()
    {
        var action = () => MessagePackConfig.CreateBuilder()
            .WithStandardResolver()
            .Build();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsafe MessagePack resolvers are not allowed*");
    }
}
