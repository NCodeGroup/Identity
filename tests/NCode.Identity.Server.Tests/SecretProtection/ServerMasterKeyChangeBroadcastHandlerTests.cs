// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.OpenId;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ServerMasterKeyChangeBroadcastHandlerTests : BaseTests
{
    private const string ServerId = "server-a";

    private (
        ServerMasterKeyChangeBroadcastHandler Handler,
        FakeRingProvider Provider,
        ServerSecretProtectorHolder Holder
    ) Create()
    {
        var provider = new FakeRingProvider();
        var holder = new ServerSecretProtectorHolder();
        var loader = new ServerSecretProtectorLoader(
            provider,
            new DataProtectionKeyRingReader(Options.Create(new KeyManagementOptions())),
            Options.Create(
                new OpenIdOptions { Server = new OpenIdServerOptions { ServerId = ServerId } }
            ),
            Options.Create(new SecretProtectionOptions()),
            new FakeTimeProvider(),
            holder,
            CreateLooseMock<ILogger<ServerSecretProtectorLoader>>().Object
        );

        return (new ServerMasterKeyChangeBroadcastHandler(loader, provider), provider, holder);
    }

    [Fact]
    public void Channel_IsTheMasterKeyChannel()
    {
        Assert.Equal("master-key-changed", Create().Handler.Channel);
    }

    [Fact]
    public async Task HandleAsync_ThisServersOwner_EvictsAndReloadsTheHolder()
    {
        var (handler, provider, holder) = Create();

        await handler.HandleAsync($"server:{ServerId}", CancellationToken.None);

        Assert.Single(provider.Evicted);
        Assert.NotNull(holder.Protector);
    }

    [Theory]
    [InlineData("tenant:acme")]
    [InlineData("server:other")]
    [InlineData("garbage")]
    public async Task HandleAsync_AnotherOwner_DoesNothing(string message)
    {
        var (handler, provider, holder) = Create();

        await handler.HandleAsync(message, CancellationToken.None);

        Assert.Empty(provider.Evicted);
        Assert.Null(holder.Protector);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_EvictsEverythingAndReloads()
    {
        var (handler, provider, holder) = Create();

        await handler.HandleMissedMessagesAsync(CancellationToken.None);

        Assert.Equal(1, provider.EvictAllCount);
        Assert.NotNull(holder.Protector);
    }
}
