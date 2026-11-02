// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class TenantChangeBroadcastHandlerTests : IDisposable
{
    private const string TenantId = "acme";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IOpenIdTenantCache> MockTenantCache { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private TenantChangeBroadcastHandler Handler { get; }

    public TenantChangeBroadcastHandlerTests()
    {
        MockTenantCache = Mocks.Create<IOpenIdTenantCache>();
        MockSecretProtectorProvider = Mocks.Create<ISecretProtectorProvider>();
        Handler = new TenantChangeBroadcastHandler(
            MockTenantCache.Object,
            MockSecretProtectorProvider.Object
        );
    }

    public void Dispose()
    {
        Mocks.Verify();
    }

    #region Channel Tests

    [Fact]
    public void Channel_IsTheTenantChangedChannel()
    {
        Assert.Equal("tenant-changed", Handler.Channel);
        Assert.Equal(TenantChangeBroadcastHandler.ChannelName, Handler.Channel);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_EvictsTheAnnouncedTenant()
    {
        MockSecretProtectorProvider
            .Setup(x => x.Evict(SecretOwner.ForTenant(TenantId)))
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync(TenantId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(TenantId, CancellationToken.None);
    }

    #endregion

    #region HandleMissedMessagesAsync Tests

    [Fact]
    public async Task HandleMissedMessagesAsync_EvictsEveryMasterKeyAndClearsTheCache()
    {
        MockSecretProtectorProvider.Setup(x => x.EvictAll()).Verifiable();
        MockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    #endregion
}
