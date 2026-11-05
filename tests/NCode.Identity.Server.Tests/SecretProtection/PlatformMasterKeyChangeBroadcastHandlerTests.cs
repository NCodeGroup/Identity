// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class PlatformMasterKeyChangeBroadcastHandlerTests : BaseTests
{
    private (
        PlatformMasterKeyChangeBroadcastHandler Handler,
        FakeRingProvider Provider,
        PlatformSecretProtectorHolder Holder
    ) Create()
    {
        var provider = new FakeRingProvider();
        var holder = new PlatformSecretProtectorHolder();
        var loader = new PlatformSecretProtectorLoader(
            provider,
            new DataProtectionKeyRingReader(Options.Create(new KeyManagementOptions())),
            Options.Create(new SecretProtectionOptions()),
            new FakeTimeProvider(),
            holder,
            CreateLooseMock<ILogger<PlatformSecretProtectorLoader>>().Object
        );

        return (new PlatformMasterKeyChangeBroadcastHandler(loader, provider), provider, holder);
    }

    #region Channel Tests

    [Fact]
    public void Channel_IsTheMasterKeyChannel()
    {
        Assert.Equal("master-key-changed", Create().Handler.Channel);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_ThePlatformsOwner_EvictsAndReloadsTheHolder()
    {
        var (handler, provider, holder) = Create();

        await handler.HandleAsync(SecretOwner.Platform.ToString(), CancellationToken.None);

        Assert.Single(provider.Evicted);
        Assert.NotNull(holder.Protector);
    }

    [Theory]
    [InlineData("tenant:acme")]
    [InlineData("tenant:platform")]
    [InlineData("garbage")]
    public async Task HandleAsync_AnotherOwner_DoesNothing(string message)
    {
        var (handler, provider, holder) = Create();

        await handler.HandleAsync(message, CancellationToken.None);

        Assert.Empty(provider.Evicted);
        Assert.Null(holder.Protector);
    }

    #endregion

    #region HandleMissedMessagesAsync Tests

    [Fact]
    public async Task HandleMissedMessagesAsync_EvictsEverythingAndReloads()
    {
        var (handler, provider, holder) = Create();

        await handler.HandleMissedMessagesAsync(CancellationToken.None);

        Assert.Equal(1, provider.EvictAllCount);
        Assert.NotNull(holder.Protector);
    }

    #endregion
}
