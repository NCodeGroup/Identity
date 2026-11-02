// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class TenantChangedHandlerTests : IDisposable
{
    private const string TenantId = "acme";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IOpenIdTenantCache> MockTenantCache { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<IReplicaBroadcaster> MockReplicaBroadcaster { get; }
    private Mock<ITenantProvisioner> MockTenantProvisioner { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private ServiceProvider ServiceProvider { get; }
    private TenantChangedHandler Handler { get; }

    public TenantChangedHandlerTests()
    {
        MockTenantCache = Mocks.Create<IOpenIdTenantCache>();
        MockSecretProtectorProvider = Mocks.Create<ISecretProtectorProvider>();
        MockReplicaBroadcaster = Mocks.Create<IReplicaBroadcaster>();
        MockTenantProvisioner = Mocks.Create<ITenantProvisioner>();
        MockTimeProvider = Mocks.Create<TimeProvider>();
        MockTimeProvider
            .Setup(CreateTimerExpression)
            .Returns(
                (TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
                    TimeProvider.System.CreateTimer(callback, state, dueTime, period)
            )
            .Verifiable();

        ServiceProvider = new ServiceCollection()
            .AddScoped(_ => MockTenantProvisioner.Object)
            .BuildServiceProvider();

        Handler = new TenantChangedHandler(
            MockTenantCache.Object,
            MockSecretProtectorProvider.Object,
            MockReplicaBroadcaster.Object,
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            MockTimeProvider.Object,
            NullLogger<TenantChangedHandler>.Instance
        );
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
        Mocks.Verify();
    }

    private static Expression<Func<TimeProvider, ITimer>> CreateTimerExpression =>
        x =>
            x.CreateTimer(
                It.IsAny<TimerCallback>(),
                It.IsAny<object?>(),
                PostCommitWork.Timeout,
                Timeout.InfiniteTimeSpan
            );

    private void SetupEviction() =>
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync(TenantId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupMasterKeyEviction() =>
        MockSecretProtectorProvider
            .Setup(x => x.Evict(SecretOwner.ForTenant(TenantId)))
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
        SetupMasterKeyEviction();
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
        SetupMasterKeyEviction();
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
        SetupMasterKeyEviction();
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
            .ThrowsAsync(new InvalidOperationException("The broadcast failed."))
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
        SetupMasterKeyEviction();
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

    [Fact]
    public async Task HandleAsync_WhenTheAnnouncementTimesOut_CompletesTheChange()
    {
        SetupEviction();
        SetupMasterKeyEviction();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TenantChangeBroadcastHandler.ChannelName,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new TaskCanceledException("The announcement timed out."))
            .Verifiable();

        await Handler.HandleAsync(
            new TenantChangedCommand(TenantId, SettingsChanged: false),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerCancels_StillEvictsAndAnnouncesTheTenant()
    {
        var evictionToken = CancellationToken.None;
        var announcementToken = CancellationToken.None;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        SetupMasterKeyEviction();
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync(TenantId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Callback((string _, IPropertyBag _, CancellationToken token) => evictionToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TenantChangeBroadcastHandler.ChannelName,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((string _, string _, CancellationToken token) => announcementToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(
            new TenantChangedCommand(TenantId, SettingsChanged: false),
            cancellation.Token
        );

        Assert.True(evictionToken.CanBeCanceled);
        Assert.False(evictionToken.IsCancellationRequested);
        Assert.True(announcementToken.CanBeCanceled);
        Assert.False(announcementToken.IsCancellationRequested);
    }

    [Fact]
    public async Task HandleAsync_WhenTheEvictionFails_StillAnnouncesTheChange()
    {
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync(TenantId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("The eviction failed."))
            .Verifiable();
        SetupBroadcast();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Handler.HandleAsync(
                new TenantChangedCommand(TenantId, SettingsChanged: false),
                CancellationToken.None
            )
        );

        Assert.Equal("The eviction failed.", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_BoundsTheEvictionAndTheAnnouncementByThePostCommitTimeout()
    {
        SetupEviction();
        SetupMasterKeyEviction();
        SetupBroadcast();

        await Handler.HandleAsync(
            new TenantChangedCommand(TenantId, SettingsChanged: false),
            CancellationToken.None
        );

        MockTimeProvider.Verify(CreateTimerExpression, Times.Exactly(2));
    }

    #endregion
}
