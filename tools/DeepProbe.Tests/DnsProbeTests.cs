using NetworkDeepProbe.Diagnostics;

namespace NetworkDeepProbe.Tests;

public sealed class DnsProbeTests
{
    [Fact]
    public void BuildQueryCreatesARecursiveAQuestion()
    {
        var query = DnsProbe.BuildQuery(0x1234, "example.com");

        Assert.Equal(0x12, query[0]);
        Assert.Equal(0x34, query[1]);
        Assert.Equal(0x01, query[2]);
        Assert.Equal(1, query[5]);
        Assert.Contains((byte)7, query);
    }

    [Fact]
    public void SuccessfulResponseRequiresMatchingIdAndAnAnswer()
    {
        byte[] response = [0x12, 0x34, 0x81, 0x80, 0, 1, 0, 1, 0, 0, 0, 0];

        Assert.True(DnsProbe.IsSuccessfulResponse(response, 0x1234));
        Assert.False(DnsProbe.IsSuccessfulResponse(response, 0x9999));
        response[7] = 0;
        Assert.False(DnsProbe.IsSuccessfulResponse(response, 0x1234));
    }

    [Fact]
    public void ResolverConfigurationParserHandlesUnixSyntaxAndComments()
    {
        var resolvers = DnsProbe.ParseResolverConfiguration([
            "# generated resolver file",
            "nameserver 192.0.2.53 # local resolver",
            "  nameserver\t2001:db8::53  ",
            "search example.test",
            "nameserver not-an-address",
            "nameserver 192.0.2.53"
        ]);

        Assert.Equal(new[] { "192.0.2.53", "2001:db8::53" }, resolvers.Select(address => address.ToString()));
    }
}
