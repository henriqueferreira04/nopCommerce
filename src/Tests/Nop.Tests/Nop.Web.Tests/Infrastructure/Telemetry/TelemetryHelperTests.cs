using FluentAssertions;
using Nop.Web.Infrastructure.Telemetry;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Infrastructure.Telemetry;

[TestFixture]
public class TelemetryHelperTests
{
    [TestCase(null, "unknown")]
    [TestCase("", "unknown")]
    [TestCase("Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X)", "mobile")]
    [TestCase("Mozilla/5.0 (iPod touch; CPU iPhone OS 16_0 like Mac OS X)", "mobile")]
    [TestCase("Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Mobile Safari/537.36", "mobile")]
    [TestCase("Mozilla/5.0 (iPad; CPU OS 16_0 like Mac OS X)", "tablet")]
    [TestCase("Mozilla/5.0 (Linux; Android 13; SM-T870) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36", "tablet")]
    [TestCase("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36", "desktop")]
    [TestCase("Mozilla/5.0 (Macintosh; Intel Mac OS X 13_0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36", "desktop")]
    public void GetDeviceType_ShouldReturnExpectedType(string userAgent, string expected)
    {
        var result = TelemetryHelper.GetDeviceType(userAgent);
        result.Should().Be(expected);
    }
}
