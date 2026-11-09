// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class MasterKeyChangeBroadcastHandlerTests : BaseTests
{
    private Mock<ISecretProtectorProvider> Provider { get; }
    private MasterKeyChangeBroadcastHandler Handler { get; }

    public MasterKeyChangeBroadcastHandlerTests()
    {
        Provider = CreateStrictMock<ISecretProtectorProvider>();
        Handler = new MasterKeyChangeBroadcastHandler(Provider.Object);
    }

    #region Channel Tests

    [Fact]
    public void Channel_IsTheMasterKeyChannel()
    {
        Assert.Equal("master-key-changed", Handler.Channel);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_EvictsTheAnnouncedOwner()
    {
        Provider.Setup(x => x.Evict(SecretOwner.ForTenant("acme"))).Verifiable();

        await Handler.HandleAsync("tenant:acme", CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("acme")]
    [InlineData(":acme")]
    public async Task HandleAsync_AMessageThatNamesNoOwner_EvictsNothing(string message)
    {
        await Handler.HandleAsync(message, CancellationToken.None);

        Assert.Empty(Provider.Invocations);
    }

    #endregion

    #region HandleMissedMessagesAsync Tests

    [Fact]
    public async Task HandleMissedMessagesAsync_EvictsEveryOwner()
    {
        Provider.Setup(x => x.EvictAll()).Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    #endregion
}
