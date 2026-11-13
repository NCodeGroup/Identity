// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using Moq;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public class DefaultOutboundHostCheckerTests : BaseTests
{
    private Mock<IOutboundHostResolver> MockHostResolver { get; }
    private DefaultOutboundHostChecker Subject { get; }

    public DefaultOutboundHostCheckerTests()
    {
        MockHostResolver = CreateStrictMock<IOutboundHostResolver>();
        Subject = new DefaultOutboundHostChecker(
            new DefaultOutboundHttpForgeryGuard([]),
            MockHostResolver.Object
        );
    }

    #region IsRefusedAsync Tests

    [Fact]
    public async Task IsRefusedAsync_WhenHostIsAPrivateLiteral_ReturnsTrue()
    {
        var result = await Subject.IsRefusedAsync("10.0.0.5", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRefusedAsync_WhenHostIsAPublicLiteral_ReturnsFalse()
    {
        var result = await Subject.IsRefusedAsync("8.8.8.8", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsRefusedAsync_WhenHostIsAnIPv6LoopbackLiteral_ReturnsTrue()
    {
        var result = await Subject.IsRefusedAsync("::1", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRefusedAsync_WhenTheNameDoesNotResolve_ReturnsFalse()
    {
        MockHostResolver
            .Setup(x => x.ResolveAsync("unresolvable.example", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SocketException((int)SocketError.HostNotFound))
            .Verifiable();

        var result = await Subject.IsRefusedAsync("unresolvable.example", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsRefusedAsync_WhenTheResolverRejectsTheName_ReturnsFalse()
    {
        MockHostResolver
            .Setup(x => x.ResolveAsync("malformed.example", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("The name is not acceptable."))
            .Verifiable();

        var result = await Subject.IsRefusedAsync("malformed.example", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsRefusedAsync_WhenTheNameResolvesOnlyToRefusedAddresses_ReturnsTrue()
    {
        MockHostResolver
            .Setup(x => x.ResolveAsync("internal.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync([IPAddress.Loopback, IPAddress.Parse("10.0.0.5")])
            .Verifiable();

        var result = await Subject.IsRefusedAsync("internal.example", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRefusedAsync_WhenTheNameResolvesToAPublicAddress_ReturnsFalse()
    {
        MockHostResolver
            .Setup(x => x.ResolveAsync("mixed.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync([IPAddress.Parse("10.0.0.5"), IPAddress.Parse("93.184.216.34")])
            .Verifiable();

        var result = await Subject.IsRefusedAsync("mixed.example", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task IsRefusedAsync_WhenTheNameResolvesToNoAddress_ReturnsFalse()
    {
        MockHostResolver
            .Setup(x => x.ResolveAsync("empty.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Verifiable();

        var result = await Subject.IsRefusedAsync("empty.example", CancellationToken.None);

        Assert.False(result);
    }

    #endregion
}
