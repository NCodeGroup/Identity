// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.RateLimiting;

public sealed class RemoteAddressPartitionTests
{
    #region GetValue Tests

    [Fact]
    public void GetValue_WithAnIPv4Address_ReturnsTheAddress()
    {
        var value = RemoteAddressPartition.GetValue(IPAddress.Parse("203.0.113.5"));

        Assert.Equal("203.0.113.5", value);
    }

    [Fact]
    public void GetValue_WithAnIPv4MappedAddress_ReturnsTheIPv4AddressItMaps()
    {
        // A dual-stack listener reports an IPv4 caller as ::ffff:a.b.c.d; both forms count in one partition.
        var value = RemoteAddressPartition.GetValue(IPAddress.Parse("::ffff:203.0.113.5"));

        Assert.Equal("203.0.113.5", value);
    }

    [Theory]
    [InlineData("2001:db8:1:2:3:4:5:6")]
    [InlineData("2001:db8:1:2:ffff:ffff:ffff:ffff")]
    [InlineData("2001:db8:1:2::")]
    public void GetValue_WithAnIPv6Address_ReturnsItsSlash64(string address)
    {
        var value = RemoteAddressPartition.GetValue(IPAddress.Parse(address));

        Assert.Equal("2001:db8:1:2::/64", value);
    }

    [Fact]
    public void GetValue_WithIPv6AddressesOfDifferentSlash64s_ReturnsDifferentValues()
    {
        var first = RemoteAddressPartition.GetValue(IPAddress.Parse("2001:db8:1:2::1"));
        var second = RemoteAddressPartition.GetValue(IPAddress.Parse("2001:db8:1:3::1"));

        Assert.NotEqual(first, second);
    }

    #endregion
}
