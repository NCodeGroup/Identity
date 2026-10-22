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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.SharedState;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class TenantChangedHandlerTests : IDisposable
{
    private const string TenantId = "acme";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IOpenIdTenantCache> MockTenantCache { get; }
    private Mock<IReplicaBroadcaster> MockReplicaBroadcaster { get; }
    private Mock<ITenantProvisioner> MockTenantProvisioner { get; }
    private ServiceProvider ServiceProvider { get; }
    private TenantChangedHandler Handler { get; }

    public TenantChangedHandlerTests()
    {
        MockTenantCache = Mocks.Create<IOpenIdTenantCache>();
        MockReplicaBroadcaster = Mocks.Create<IReplicaBroadcaster>();
        MockTenantProvisioner = Mocks.Create<ITenantProvisioner>();

        ServiceProvider = new ServiceCollection()
            .AddScoped(_ => MockTenantProvisioner.Object)
            .BuildServiceProvider();

        Handler = new TenantChangedHandler(
            MockTenantCache.Object,
            MockReplicaBroadcaster.Object,
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TenantChangedHandler>.Instance
        );
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
        Mocks.Verify();
    }

    private void SetupEviction() =>
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync(TenantId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupBroadcast() =>
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TenantChangeBroadcastHandler.ChannelName,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private static PersistedTenant CreatePersistedTenant() =>
        new()
        {
            TenantId = TenantId,
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = null!,
            Secrets = null!,
        };

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenTheSettingsChanged_EvictsSeedsAndAnnouncesTheTenant()
    {
        var calls = new List<string>();
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync(TenantId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Callback(() => calls.Add("evict"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantProvisioner
            .Setup(x => x.SeedAsync(TenantId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("seed"))
            .ReturnsAsync(CreatePersistedTenant())
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TenantChangeBroadcastHandler.ChannelName,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => calls.Add("broadcast"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(
            new TenantChangedCommand(TenantId, SettingsChanged: true),
            CancellationToken.None
        );

        Assert.Equal(["evict", "seed", "broadcast"], calls);
    }

    [Fact]
    public async Task HandleAsync_WhenAnythingElseChanged_EvictsAndAnnouncesWithoutSeeding()
    {
        SetupEviction();
        SetupBroadcast();

        // Strict mocks: no SeedAsync setup, so a seed would fail the test.
        await Handler.HandleAsync(
            new TenantChangedCommand(TenantId, SettingsChanged: false),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheAnnouncementFails_CompletesTheChange()
    {
        SetupEviction();
        MockTenantProvisioner
            .Setup(x => x.SeedAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePersistedTenant())
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TenantChangeBroadcastHandler.ChannelName,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                ValueTask.FromException(new InvalidOperationException("The broadcast failed."))
            )
            .Verifiable();

        await Handler.HandleAsync(
            new TenantChangedCommand(TenantId, SettingsChanged: true),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheSeedFails_StillAnnouncesTheChange()
    {
        SetupEviction();
        SetupBroadcast();
        MockTenantProvisioner
            .Setup(x => x.SeedAsync(TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The seed failed."))
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Handler.HandleAsync(
                new TenantChangedCommand(TenantId, SettingsChanged: true),
                CancellationToken.None
            )
        );

        Assert.Equal("The seed failed.", exception.Message);
    }

    #endregion
}
