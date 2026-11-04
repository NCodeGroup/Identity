// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class MasterKeyChangeBroadcastHandlerTests : IDisposable
{
    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<ISecretProtectorProvider> _provider;
    private readonly MasterKeyChangeBroadcastHandler _handler;

    public MasterKeyChangeBroadcastHandlerTests()
    {
        _provider = _mocks.Create<ISecretProtectorProvider>();
        _handler = new MasterKeyChangeBroadcastHandler(_provider.Object);
    }

    public void Dispose() => _mocks.Verify();

    [Fact]
    public void Channel_IsTheMasterKeyChannel()
    {
        Assert.Equal("master-key-changed", _handler.Channel);
    }

    [Fact]
    public async Task HandleAsync_EvictsTheAnnouncedOwner()
    {
        _provider.Setup(x => x.Evict(SecretOwner.ForTenant("acme")));

        await _handler.HandleAsync("tenant:acme", CancellationToken.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("acme")]
    [InlineData(":acme")]
    public async Task HandleAsync_AMessageThatNamesNoOwner_EvictsNothing(string message)
    {
        await _handler.HandleAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_EvictsEveryOwner()
    {
        _provider.Setup(x => x.EvictAll());

        await _handler.HandleMissedMessagesAsync(CancellationToken.None);
    }
}
