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

    private static void AssertRetryDelay(TimeSpan? retryAfter)
    {
        Assert.NotNull(retryAfter);
        Assert.True(retryAfter > TimeSpan.Zero);
        Assert.True(retryAfter <= TimeSpan.FromMinutes(1));
    }

    private async ValueTask ExhaustAsync(string? remoteAddress, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < SetupDefaults.SetupAttemptsPerMinute; ++attempt)
        {
            Assert.Null(
                await RateLimiter.AcquireAsync(CreateHttpContext(remoteAddress), cancellationToken)
            );
        }
    }

    #endregion

    #region AcquireAsync Tests

    [Fact]
    public async Task AcquireAsync_WhenTheAttemptsAreWithinTheLimit_ReturnsNull()
    {
        await ExhaustAsync("203.0.113.5", CancellationToken.None);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheAddressHasUsedItsAttempts_ReturnsTheRetryDelay()
    {
        await ExhaustAsync("203.0.113.5", CancellationToken.None);

        var retryAfter = await RateLimiter.AcquireAsync(
            CreateHttpContext("203.0.113.5"),
            CancellationToken.None
        );

        AssertRetryDelay(retryAfter);
    }

    [Fact]
    public async Task AcquireAsync_WhenAnotherAddressHasUsedItsAttempts_ReturnsNull()
    {
        await ExhaustAsync("203.0.113.5", CancellationToken.None);

        var retryAfter = await RateLimiter.AcquireAsync(
            CreateHttpContext("203.0.113.6"),
            CancellationToken.None
        );

        Assert.Null(retryAfter);
    }

    [Fact]
    public async Task AcquireAsync_WhenAnotherIPv6AddressOfTheSameSlash64HasUsedTheAttempts_ReturnsTheRetryDelay()
    {
        await ExhaustAsync("2001:db8:1:2::1", CancellationToken.None);

        var retryAfter = await RateLimiter.AcquireAsync(
            CreateHttpContext("2001:db8:1:2:ffff:ffff:ffff:ffff"),
            CancellationToken.None
        );

        AssertRetryDelay(retryAfter);
    }

    [Fact]
    public async Task AcquireAsync_WhenAnIPv6AddressOfAnotherSlash64HasUsedTheAttempts_ReturnsNull()
    {
        await ExhaustAsync("2001:db8:1:2::1", CancellationToken.None);

        var retryAfter = await RateLimiter.AcquireAsync(
            CreateHttpContext("2001:db8:1:3::1"),
            CancellationToken.None
        );

        Assert.Null(retryAfter);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheRequestHasNoRemoteAddress_CountsItsAttemptsTogether()
    {
        await ExhaustAsync(null, CancellationToken.None);

        var retryAfter = await RateLimiter.AcquireAsync(
            CreateHttpContext(null),
            CancellationToken.None
        );

        AssertRetryDelay(retryAfter);
    }

    #endregion
}
