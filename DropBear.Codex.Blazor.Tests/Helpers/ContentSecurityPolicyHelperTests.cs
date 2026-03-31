using DropBear.Codex.Blazor.Helpers;
using DropBear.Codex.Blazor.Options;
using FluentAssertions;

namespace DropBear.Codex.Blazor.Tests.Helpers;

public sealed class ContentSecurityPolicyHelperTests
{
    [Fact]
    public void StrictBlazorServerPreset_ShouldUseNoncePlaceholder_AndAvoidUnsafeInlineStyles()
    {
        var policy = ContentSecurityPolicyHelper.Presets.StrictBlazorServer;

        policy.Should().Contain($"'nonce-{ContentSecurityPolicyHelper.NoncePlaceholder}'");
        policy.Should().NotContain("style-src 'self' 'unsafe-inline'");
    }

    [Fact]
    public void ProductionOptions_ShouldEnableNonceCsp()
    {
        SecurityHeadersOptions.Production.UseNonceCsp.Should().BeTrue();
    }
}
