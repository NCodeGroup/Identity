// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Setup;

public sealed class SetupRateLimiterTests : IDisposable
{
    private SetupRateLimiter RateLimiter { get; } = new();

    public void Dispose() => RateLimiter.Dispose();

    #region Helpers

    private static DefaultHttpContext CreateHttpContext(string? remoteAddress)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteAddress is null
            ? null
            : IPAddress.Parse(remoteAddress);
        return httpContext;
    }

    private async ValueTask ExhaustAsync(string? remoteAddress, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < SetupDefaults.RootAttemptsPerMinute; ++attempt)
        {
            Assert.True(
                await RateLimiter.TryAcquireAsync(
                    CreateHttpContext(remoteAddress),
                    cancellationToken
                )
            );
        }
    }

    #endregion

    #region TryAcquireAsync Tests

    [Fact]
    public async Task TryAcquireAsync_WhenTheAttemptsAreWithinTheLimit_ReturnsTrue()
    {
        await ExhaustAsync("203.0.113.5", CancellationToken.None);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenTheAddressHasUsedItsAttempts_ReturnsFalse()
    {
        await ExhaustAsync("203.0.113.5", CancellationToken.None);

        var acquired = await RateLimiter.TryAcquireAsync(
            CreateHttpContext("203.0.113.5"),
            CancellationToken.None
        );

        Assert.False(acquired);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAnotherAddressHasUsedItsAttempts_ReturnsTrue()
    {
        await ExhaustAsync("203.0.113.5", CancellationToken.None);

        var acquired = await RateLimiter.TryAcquireAsync(
            CreateHttpContext("203.0.113.6"),
            CancellationToken.None
        );

        Assert.True(acquired);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAnotherIPv6AddressOfTheSameSlash64HasUsedTheAttempts_ReturnsFalse()
    {
        await ExhaustAsync("2001:db8:1:2::1", CancellationToken.None);

        var acquired = await RateLimiter.TryAcquireAsync(
            CreateHttpContext("2001:db8:1:2:ffff:ffff:ffff:ffff"),
            CancellationToken.None
        );

        Assert.False(acquired);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAnIPv6AddressOfAnotherSlash64HasUsedTheAttempts_ReturnsTrue()
    {
        await ExhaustAsync("2001:db8:1:2::1", CancellationToken.None);

        var acquired = await RateLimiter.TryAcquireAsync(
            CreateHttpContext("2001:db8:1:3::1"),
            CancellationToken.None
        );

        Assert.True(acquired);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenTheRequestHasNoRemoteAddress_CountsItsAttemptsTogether()
    {
        await ExhaustAsync(null, CancellationToken.None);

        var acquired = await RateLimiter.TryAcquireAsync(
            CreateHttpContext(null),
            CancellationToken.None
        );

        Assert.False(acquired);
    }

    #endregion
}
