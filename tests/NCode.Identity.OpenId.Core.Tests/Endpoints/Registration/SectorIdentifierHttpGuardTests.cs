#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Net;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Logic;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Registration;

public class SectorIdentifierHttpGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")] // loopback
    [InlineData("0.0.0.0")] // "this" network
    [InlineData("10.1.2.3")] // private
    [InlineData("100.64.0.1")] // CGNAT
    [InlineData("169.254.1.1")] // link-local
    [InlineData("172.16.0.1")] // private
    [InlineData("172.31.255.255")] // private
    [InlineData("192.168.1.1")] // private
    [InlineData("192.0.2.1")] // TEST-NET-1
    [InlineData("198.18.0.1")] // benchmarking
    [InlineData("198.51.100.1")] // TEST-NET-2
    [InlineData("203.0.113.1")] // TEST-NET-3
    [InlineData("224.0.0.1")] // multicast
    [InlineData("255.255.255.255")] // broadcast
    [InlineData("::1")] // IPv6 loopback
    [InlineData("fe80::1")] // IPv6 link-local
    [InlineData("fc00::1")] // IPv6 unique-local
    [InlineData("fd12:3456::1")] // IPv6 unique-local
    [InlineData("::")] // IPv6 unspecified
    [InlineData("::ffff:127.0.0.1")] // IPv4-mapped loopback
    [InlineData("::ffff:10.0.0.1")] // IPv4-mapped private
    public void IsDisallowedAddress_PrivateOrReserved_ReturnsTrue(string address)
    {
        Assert.True(SectorIdentifierHttpGuard.IsDisallowedAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")] // example.com
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")] // public IPv6
    public void IsDisallowedAddress_PublicUnicast_ReturnsFalse(string address)
    {
        Assert.False(SectorIdentifierHttpGuard.IsDisallowedAddress(IPAddress.Parse(address)));
    }
}
