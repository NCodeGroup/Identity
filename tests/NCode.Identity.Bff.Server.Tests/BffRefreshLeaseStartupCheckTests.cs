// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client.Options;
using Xunit;

namespace NCode.Identity.Bff.Server.Tests;

public class BffRefreshLeaseStartupCheckTests : BaseTests
{
    private static BffRefreshLeaseStartupCheck CreateCheck(
        BffOptions bffOptions,
        TimeSpan totalTimeout
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(bffOptions),
            Microsoft.Extensions.Options.Options.Create(
                new OpenIdClientOptions { TotalTimeout = totalTimeout }
            )
        );

    private static BffOptions CreateOptions(TimeSpan refreshLeaseDuration) =>
        new() { Tokens = new BffTokenOptions { RefreshLeaseDuration = refreshLeaseDuration } };

    #region StartingAsync Tests

    [Fact]
    public async Task StartingAsync_DefaultLeaseAndTimeout_Completes()
    {
        var check = CreateCheck(CreateOptions(TimeSpan.FromSeconds(40)), TimeSpan.FromSeconds(15));

        await check.StartingAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartingAsync_LeaseShorterThanTwiceTimeoutPlusMargin_Throws()
    {
        var check = CreateCheck(CreateOptions(TimeSpan.FromSeconds(30)), TimeSpan.FromSeconds(15));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            check.StartingAsync(CancellationToken.None)
        );

        Assert.Contains("Bff:Tokens:RefreshLeaseDuration", exception.Message);
        Assert.Contains("(00:00:30)", exception.Message);
        Assert.Contains("(00:00:40)", exception.Message);
    }

    [Fact]
    public async Task StartingAsync_LeaseEqualToMinimum_Completes()
    {
        var check = CreateCheck(CreateOptions(TimeSpan.FromSeconds(130)), TimeSpan.FromSeconds(60));

        await check.StartingAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartingAsync_WithoutTokens_Completes()
    {
        var check = CreateCheck(new BffOptions { Tokens = null! }, TimeSpan.FromSeconds(15));

        await check.StartingAsync(CancellationToken.None);
    }

    #endregion

    #region Other Lifecycle Tests

    [Fact]
    public async Task OtherLifecycleMethods_Always_Complete()
    {
        var check = CreateCheck(CreateOptions(TimeSpan.FromSeconds(1)), TimeSpan.FromSeconds(60));

        await check.StartAsync(CancellationToken.None);
        await check.StartedAsync(CancellationToken.None);
        await check.StoppingAsync(CancellationToken.None);
        await check.StopAsync(CancellationToken.None);
        await check.StoppedAsync(CancellationToken.None);
    }

    #endregion
}
