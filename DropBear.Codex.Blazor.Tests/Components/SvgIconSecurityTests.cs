using DropBear.Codex.Blazor.Components.Icons;
using FluentAssertions;

namespace DropBear.Codex.Blazor.Tests.Components;

public sealed class SvgIconSecurityTests
{
    [Fact]
    public void RegisterCustomIcon_ShouldRejectSvgContainingScript()
    {
        var result = SvgIcon.RegisterCustomIcon(
            "unsafe-script",
            """<svg xmlns="http://www.w3.org/2000/svg"><script>alert('xss')</script></svg>""");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("unsafe script content");
    }

    [Fact]
    public void RegisterCustomIcon_ShouldRejectSvgContainingJavascriptProtocol()
    {
        var result = SvgIcon.RegisterCustomIcon(
            "unsafe-javascript",
            """<svg xmlns="http://www.w3.org/2000/svg"><a href="javascript:alert('xss')">X</a></svg>""");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("unsafe script content");
    }
}
