using Beep.KocAiCommunity.Infrastructure.Datasets;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class UrlImportGuardTests
{
    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]  // cloud metadata endpoint
    [InlineData("http://10.0.0.5/data.csv")]                  // RFC1918 10/8
    [InlineData("http://192.168.1.10/data.csv")]              // RFC1918 192.168/16
    [InlineData("http://172.16.4.4/data.csv")]                // RFC1918 172.16/12
    [InlineData("http://127.0.0.1/data.csv")]                 // loopback
    public void Blocks_private_and_loopback_addresses(string url)
    {
        UrlImportGuard.Check(url).Should().Be(UrlBlockReason.PrivateAddress);
    }

    [Theory]
    [InlineData("ftp://example.com/data.csv")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not-a-url")]
    public void Blocks_non_http_schemes(string url)
    {
        UrlImportGuard.Check(url).Should().Be(UrlBlockReason.BadScheme);
    }

    [Fact]
    public void Allows_a_public_ip_literal()
    {
        UrlImportGuard.Check("https://8.8.8.8/data.csv").Should().Be(UrlBlockReason.None);
    }
}
