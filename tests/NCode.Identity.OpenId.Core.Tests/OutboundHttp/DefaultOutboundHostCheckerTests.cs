// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public class DefaultOutboundHostCheckerTests : BaseTests
{
    private DefaultOutboundHostChecker Subject { get; } =
        new(new DefaultOutboundHttpForgeryGuard([]));

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
    public async Task IsRefusedAsync_WhenHostResolvesOnlyToLoopback_ReturnsTrue()
    {
        var result = await Subject.IsRefusedAsync("localhost", CancellationToken.None);

        Assert.True(result);
    }

    #endregion
}
