using DropBear.Codex.Blazor.Helpers;
using FluentAssertions;

namespace DropBear.Codex.Blazor.Tests.Helpers;

public sealed class HtmlSanitizationHelperTests
{
    [Fact]
    public void Sanitize_ShouldEncode_UntrustedHtml()
    {
        var result = HtmlSanitizationHelper.Sanitize("<script>alert('xss')</script><b>bold</b>");

        result.Value.Should().Contain("&lt;script&gt;");
        result.Value.Should().Contain("&lt;b&gt;bold&lt;/b&gt;");
    }
}
