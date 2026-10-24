#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
