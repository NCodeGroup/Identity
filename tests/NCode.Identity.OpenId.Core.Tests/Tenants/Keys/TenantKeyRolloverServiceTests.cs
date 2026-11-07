// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyRolloverServiceTests : BaseTests
{
    private const string FirstTenantId = "acme";
    private const string SecondTenantId = "globex";
    private const string TeamId = "team-a";
    private const string OtherTeamId = "team-b";
    private const string DisabledTeamId = "team-off";
    private const string ChildTeamId = "acme-web";
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan Lease = Interval / 2;
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<IMaterializedPlatformAccessor> MockPlatformAccessor { get; }
    private Mock<OpenIdPlatform> MockPlatform { get; }
    private Mock<ITenantKeyPolicy> MockPolicy { get; }
    private Mock<ITenantKeyRolloverPlanner> MockPlanner { get; }
    private Mock<ITenantKeyRollover> MockRollover { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private Mock<IExpiringKeySet> MockExpiringKeySet { get; }
    private Mock<IEventPublisher> MockEventPublisher { get; }
    private Mock<IMediator> MockMediator { get; }
    private IReadOnlySettingCollection TeamSettings { get; }
    private ServiceProvider ServiceProvider { get; }
    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private FakeLogger<TenantKeyRolloverService> Logger { get; } = new();
    private TenantKeyRolloverService Service { get; }

    public TenantKeyRolloverServiceTests()
    {
        OpenIdOptions.Tenant.KeyRollover.Interval = Interval;
        MockPlatformAccessor = CreateStrictMock<IMaterializedPlatformAccessor>();
        MockPlatform = CreateStrictMock<OpenIdPlatform>();
        MockPolicy = CreateStrictMock<ITenantKeyPolicy>();
        MockPlanner = CreateStrictMock<ITenantKeyRolloverPlanner>();
        MockRollover = CreateStrictMock<ITenantKeyRollover>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();
        MockTeamStore = CreateStrictMock<ITeamStore>();
        MockExpiringKeySet = CreateStrictMock<IExpiringKeySet>();
        MockEventPublisher = CreateStrictMock<IEventPublisher>();
        MockMediator = CreateStrictMock<IMediator>();
        TeamSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;

        ServiceProvider = new ServiceCollection()
            .AddScoped(_ => MockMediator.Object)
            .BuildServiceProvider();

        Service = CreateService(TimeProvider, MockExpiringKeySet.Object);
    }

    private TenantKeyRolloverService CreateService(
        TimeProvider timeProvider,
        IExpiringKeySet expiringKeySet
    ) =>
        new(
            MockPlatformAccessor.Object,
            Options.Create(OpenIdOptions),
            MockPolicy.Object,
            MockPlanner.Object,
            MockRollover.Object,
            MockEffectiveSettingsResolver.Object,
            MockStoreManagerFactory.Object,
            expiringKeySet,
            MockEventPublisher.Object,
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            Logger
        );

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Service.Dispose();
            ServiceProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    private OpenIdTenantKeyProvisioningOptions KeyProvisioning =>
        OpenIdOptions.Tenant.KeyProvisioning;

    #region RollOverAsync Tests

    [Fact]
    public async Task RollOverAsync_WhenATenantsPlanChangesKeys_AppliesItInItsOwnUnitOfWorkAndAnnouncesTheTenant()
    {
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupTeam();
        SetupStoreManager();

        // Two pages, so the pass follows the cursor rather than stopping after the first page.
        SetupTenantPage(cursor: null, nextCursor: "next", FirstTenantId);
        SetupTenantPage(cursor: "next", nextCursor: null, SecondTenantId);

        // The first tenant's plan changes a key; the second's is empty, so it is neither saved nor announced.
        var first = CreateSecret("first");
        var second = CreateSecret("second");
        SetupTenantSettings(FirstTenantId);
        SetupSecrets(FirstTenantId, [first]);
        var plan = SetupPlan([first], empty: false);
        SetupApply(FirstTenantId, [first], plan);
        SetupSave();
        SetupAnnouncement(FirstTenantId);
        SetupTenantSettings(SecondTenantId);
        SetupSecrets(SecondTenantId, [second]);
        SetupPlan([second], empty: true);

        await Service.RollOverAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenATenantsChangeIsCommitted_PublishesItsAuditEventsAfterTheSave()
    {
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupTeam();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId);

        var first = CreateSecret("first");
        SetupTenantSettings(FirstTenantId);
        SetupSecrets(FirstTenantId, [first]);
        var plan = SetupPlan([first], empty: false);
        var auditEvent = CreateAuditEvent("first");
        SetupApply(FirstTenantId, [first], plan, auditEvent);
        var saved = false;
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                saved = true;
                return ValueTask.CompletedTask;
            })
            .Verifiable();
        MockEventPublisher
            .Setup(x => x.PublishAsync(auditEvent, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Assert.True(
                    saved,
                    "The audit event was published before the change was committed."
                );
                return ValueTask.CompletedTask;
            })
            .Verifiable(Times.Once);
        SetupAnnouncement(FirstTenantId);

        await Service.RollOverAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenCalledAgainOneIntervalLater_TakesTheLeaseAgain()
    {
        // The lease lasts half the interval, so the same replica's next tick, one interval after the pass started,
        // finds it expired; one that lasted the whole interval would still be held by the moments the pass took to
        // start, and every other pass would be skipped. The page is read exactly once per pass that holds the lease.
        var keySet = new FakeExpiringKeySet(TimeProvider);
        using var service = CreateService(TimeProvider, keySet);
        SetupSpecifications();
        SetupTeam();
        SetupStoreManager();
        MockTenantStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    TeamId,
                    It.IsAny<PageRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTenantSettings> { Items = [], NextCursor = null }
            )
            .Verifiable(Times.Exactly(2));

        await service.RollOverAsync(CancellationToken.None);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(5));
        await service.RollOverAsync(CancellationToken.None);
        TimeProvider.Advance(Interval - TimeSpan.FromMilliseconds(5));
        await service.RollOverAsync(CancellationToken.None);

        Assert.Equal(2, keySet.Taken);
    }

    [Fact]
    public async Task RollOverAsync_WhenATenantsChangeIsCommitted_PublishesAndAnnouncesOnATokenOfTheirOwn()
    {
        // The change is committed, so neither the publishing nor the announcement runs on the pass's token: a host
        // that stops after the commit cannot leave the change unrecorded or unannounced.
        using var stopping = new CancellationTokenSource();
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupTeam();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId);

        var first = CreateSecret("first");
        SetupTenantSettings(FirstTenantId);
        SetupSecrets(FirstTenantId, [first]);
        var plan = SetupPlan([first], empty: false);
        var auditEvent = CreateAuditEvent("first");
        SetupApply(FirstTenantId, [first], plan, auditEvent);
        SetupSave();
        CancellationToken? publishToken = null;
        MockEventPublisher
            .Setup(x => x.PublishAsync(auditEvent, It.IsAny<CancellationToken>()))
            .Callback((TenantKeyAuditEvent _, CancellationToken token) => publishToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);
        CancellationToken? announceToken = null;
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<TenantChangedCommand>(command =>
                        command.TenantId == FirstTenantId && !command.SettingsChanged
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((TenantChangedCommand _, CancellationToken token) => announceToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.RollOverAsync(stopping.Token);

        Assert.NotNull(publishToken);
        Assert.NotEqual(stopping.Token, publishToken.Value);
        Assert.NotNull(announceToken);
        Assert.NotEqual(stopping.Token, announceToken.Value);
    }

    [Fact]
    public async Task RollOverAsync_WhenPublishingTheAuditEventsFails_StillAnnouncesTheTenantAndContinues()
    {
        // The change is committed whatever the publisher did, so every replica must still evict the tenant, and the
        // next tenant is still rolled over.
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupTeam();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);

        var first = CreateSecret("first");
        var second = CreateSecret("second");
        SetupTenantSettings(FirstTenantId);
        SetupSecrets(FirstTenantId, [first]);
        var firstPlan = SetupPlan([first], empty: false);
        var firstEvent = CreateAuditEvent("first");
        SetupApply(FirstTenantId, [first], firstPlan, firstEvent);
        MockEventPublisher
            .Setup(x => x.PublishAsync(firstEvent, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sink unavailable"))
            .Verifiable();
        SetupAnnouncement(FirstTenantId);
        SetupTenantSettings(SecondTenantId);
        SetupSecrets(SecondTenantId, [second]);
        var secondPlan = SetupPlan([second], empty: false);
        SetupApply(SecondTenantId, [second], secondPlan);
        SetupAnnouncement(SecondTenantId);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));

        await Service.RollOverAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenAnotherReplicaHoldsTheLease_ReadsNoTenant()
    {
        // The store is strict with no setup: reading a tenant fails the test.
        SetupSpecifications();
        SetupLease(acquired: false);

        await Service.RollOverAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenProvisioningIsDisabled_TakesNoLease()
    {
        // Every mock is strict with no setup: resolving the specifications or taking the lease fails the test.
        KeyProvisioning.Enabled = false;

        await Service.RollOverAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenATenantHoldsNoKey_LeavesItToItsFirstMaterialization()
    {
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupTeam();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);

        // Strict mocks: no plan or announcement setup, so planning for a tenant without keys fails the test.
        SetupSecrets(FirstTenantId, secrets: null);
        SetupSecrets(SecondTenantId, secrets: []);

        await Service.RollOverAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenTheSaveLosesARace_LeavesTheTenantToTheWinnerAndPublishesNothingForIt()
    {
        // The publisher is strict: an event for the discarded change fails the test.
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupTeam();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);

        var first = CreateSecret("first");
        var second = CreateSecret("second");
        SetupTenantSettings(FirstTenantId);
        SetupSecrets(FirstTenantId, [first]);
        var firstPlan = SetupPlan([first], empty: false);
        SetupApply(FirstTenantId, [first], firstPlan, CreateAuditEvent("first"));
        SetupTenantSettings(SecondTenantId);
        SetupSecrets(SecondTenantId, [second]);
        var secondPlan = SetupPlan([second], empty: false);
        var secondEvent = CreateAuditEvent("second");
        SetupApply(SecondTenantId, [second], secondPlan, secondEvent);
        MockEventPublisher
            .Setup(x => x.PublishAsync(secondEvent, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);
        var saves = 0;
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
                ++saves == 1
                    ? ValueTask.FromException(new StoreConcurrencyException("lost"))
                    : ValueTask.CompletedTask
            )
            .Verifiable(Times.Exactly(2));
        SetupAnnouncement(SecondTenantId);

        await Service.RollOverAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenATenantFails_ContinuesWithTheNextTenant()
    {
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupTeam();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);

        var second = CreateSecret("second");
        MockTenantStore
            .Setup(x => x.GetSecretsOrDefaultAsync(FirstTenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable();
        SetupTenantSettings(SecondTenantId);
        SetupSecrets(SecondTenantId, [second]);
        var plan = SetupPlan([second], empty: false);
        SetupApply(SecondTenantId, [second], plan);
        SetupSave();
        SetupAnnouncement(SecondTenantId);

        await Service.RollOverAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WithADisabledTeam_SkipsItsTenantsAndJudgesEachEnabledTeamsTenantsByItsOwnSettings()
    {
        // The disabled team has no settings, tenant page or secrets setup on the strict mocks, and the log holds no
        // failure, so no work ran for it.
        var firstTeamSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var otherTeamSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupStoreManager();
        SetupTeams(
            (TeamId, false, firstTeamSettings),
            (DisabledTeamId, true, CreateStrictMock<IReadOnlySettingCollection>().Object),
            (OtherTeamId, false, otherTeamSettings)
        );
        SetupTeamTenantPage(TeamId, cursor: null, nextCursor: null, FirstTenantId);
        SetupTeamTenantPage(OtherTeamId, cursor: null, nextCursor: null, SecondTenantId);

        var first = CreateSecret("first");
        var second = CreateSecret("second");
        SetupTenantSettings(FirstTenantId, firstTeamSettings);
        SetupSecrets(FirstTenantId, [first]);
        var plan = SetupPlan([first], empty: false);
        SetupApply(FirstTenantId, [first], plan);
        SetupSave();
        SetupAnnouncement(FirstTenantId);
        SetupTenantSettings(SecondTenantId, otherTeamSettings);
        SetupSecrets(SecondTenantId, [second]);
        SetupPlan([second], empty: true);

        await Service.RollOverAsync(CancellationToken.None);

        Assert.DoesNotContain(
            Logger.Collector.GetSnapshot(),
            record => record.Level >= LogLevel.Warning
        );
    }

    [Fact]
    public async Task RollOverAsync_WhenAnAncestorTeamIsDisabled_SkipsTheTeam()
    {
        // The child team has no settings, tenant page or secrets setup on the strict mocks, and the log holds no
        // failure, so no work ran for it.
        SetupSpecifications();
        SetupLease(acquired: true);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockTeamStore
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTeam>
                {
                    Items = [CreateTeam(ChildTeamId, isDisabled: false, ["acme"])],
                    NextCursor = null,
                }
            )
            .Verifiable();
        MockTeamStore
            .Setup(x =>
                x.GetManyAsync(
                    It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "acme" })),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateTeam("acme", isDisabled: true, [])])
            .Verifiable();

        await Service.RollOverAsync(CancellationToken.None);

        Assert.DoesNotContain(
            Logger.Collector.GetSnapshot(),
            record => record.Level >= LogLevel.Warning
        );
    }

    [Fact]
    public async Task RollOverAsync_WhenTwoTeamsShareAnEnabledAncestor_ReadsTheAncestorsOnceAndRollsBothOver()
    {
        const string SecondChildTeamId = "acme-api";
        var firstChildSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var secondChildSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupStoreManager();
        MockTeamStore
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTeam>
                {
                    Items =
                    [
                        CreateTeam(ChildTeamId, isDisabled: false, ["acme"]),
                        CreateTeam(SecondChildTeamId, isDisabled: false, ["acme"]),
                    ],
                    NextCursor = null,
                }
            )
            .Verifiable();
        MockTeamStore
            .Setup(x =>
                x.GetManyAsync(
                    It.Is<IReadOnlyCollection<string>>(ids => ids.SequenceEqual(new[] { "acme" })),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateTeam("acme", isDisabled: false, [])])
            .Verifiable();
        SetupTeamSettings(ChildTeamId, firstChildSettings);
        SetupTeamSettings(SecondChildTeamId, secondChildSettings);
        SetupTeamTenantPage(ChildTeamId, cursor: null, nextCursor: null, FirstTenantId);
        SetupTeamTenantPage(SecondChildTeamId, cursor: null, nextCursor: null, SecondTenantId);

        var first = CreateSecret("first");
        var second = CreateSecret("second");
        SetupTenantSettings(FirstTenantId, firstChildSettings);
        SetupSecrets(FirstTenantId, [first]);
        var firstPlan = SetupPlan([first], empty: false);
        SetupApply(FirstTenantId, [first], firstPlan);
        SetupTenantSettings(SecondTenantId, secondChildSettings);
        SetupSecrets(SecondTenantId, [second]);
        var secondPlan = SetupPlan([second], empty: false);
        SetupApply(SecondTenantId, [second], secondPlan);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        SetupAnnouncement(FirstTenantId);
        SetupAnnouncement(SecondTenantId);

        await Service.RollOverAsync(CancellationToken.None);

        MockTeamStore.Verify(
            x =>
                x.GetManyAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
    }

    [Fact]
    public async Task RollOverAsync_WhenATeamFails_ContinuesWithTheNextTeam()
    {
        var otherTeamSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupStoreManager();
        SetupTeamPage((TeamId, false), (OtherTeamId, false));
        SetupTeamSettings(OtherTeamId, otherTeamSettings);

        // The first team's settings cannot be resolved, so its tenants are not read.
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync(TeamId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable();
        SetupTeamTenantPage(OtherTeamId, cursor: null, nextCursor: null, SecondTenantId);

        var second = CreateSecret("second");
        SetupTenantSettings(SecondTenantId, otherTeamSettings);
        SetupSecrets(SecondTenantId, [second]);
        var plan = SetupPlan([second], empty: false);
        SetupApply(SecondTenantId, [second], plan);
        SetupSave();
        SetupAnnouncement(SecondTenantId);

        await Service.RollOverAsync(CancellationToken.None);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_RollsOverOnceThePlatformIsMaterializedAndAgainAtEveryTick()
    {
        var timerCreated = new TaskCompletionSource<(TimerCallback Callback, object? State)>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var mockTimer = CreateStrictMock<ITimer>();
        mockTimer.Setup(x => x.Dispose()).Verifiable();
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider
            .Setup(x =>
                x.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object?>(), Interval, Interval)
            )
            .Callback(
                (TimerCallback callback, object? state, TimeSpan _, TimeSpan _) =>
                    timerCreated.SetResult((callback, state))
            )
            .Returns(mockTimer.Object)
            .Verifiable();
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockPlatformAccessor
            .Setup(x => x.GetPlatformAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockPlatform.Object)
            .Verifiable();
        SetupSpecifications();

        // The first pass, at start, fails to take the lease; the second, at the tick, fails outright; the third
        // runs: a loop that ends after one pass or on a failed pass fails the test.
        var passes = 0;
        var passed = new[]
        {
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        MockExpiringKeySet
            .Setup(x =>
                x.TryAddAsync(
                    TenantKeyRolloverService.LeasePurpose,
                    Now + Lease,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                var pass = passes++;
                passed[pass].SetResult();
                return pass switch
                {
                    0 => ValueTask.FromResult(false),
                    1 => ValueTask.FromException<bool>(new InvalidOperationException("boom")),
                    _ => ValueTask.FromResult(false),
                };
            })
            .Verifiable(Times.Exactly(3));
        using var service = CreateService(mockTimeProvider.Object, MockExpiringKeySet.Object);

        await service.StartAsync(CancellationToken.None);
        await passed[0].Task.WaitAsync(WaitTimeout);
        var (tick, state) = await timerCreated.Task.WaitAsync(WaitTimeout);
        tick(state);
        await passed[1].Task.WaitAsync(WaitTimeout);
        tick(state);
        await passed[2].Task.WaitAsync(WaitTimeout);
        await service.StopAsync(CancellationToken.None);

        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    #endregion

    #region Scaffolding

    private static bool IsTenantDocument(JsonElement element, string tenantId) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("tenant", out var value)
        && value.GetString() == tenantId;

    private static PersistedSecret CreateSecret(string secretId) =>
        TenantKeyTestData.CreateSecret(
            secretId,
            Now.AddDays(-30),
            Now.AddDays(-30),
            Now.AddDays(60),
            Now.AddDays(67)
        );

    private static PersistedTeam CreateTeam(
        string teamId,
        bool isDisabled,
        IReadOnlyList<string> ancestorTeamIds
    ) =>
        new PersistedTeamBuilder
        {
            TeamId = teamId,
            IsDisabled = isDisabled,
            AncestorTeamIds = ancestorTeamIds,
            ConcurrencyToken = "token",
            SettingsConcurrencyToken = "token",
        }.Build();

    private void SetupSpecifications() =>
        MockPolicy
            .Setup(x => x.ResolveSpecifications(KeyProvisioning))
            .Returns(
                new TenantKeySpecificationResolution
                {
                    Specifications = [TenantKeyTestData.Rs256],
                    Errors = [],
                }
            )
            .Verifiable();

    private void SetupLease(bool acquired) =>
        MockExpiringKeySet
            .Setup(x =>
                x.TryAddAsync(
                    TenantKeyRolloverService.LeasePurpose,
                    Now + Lease,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acquired)
            .Verifiable();

    // The pass's one enabled team, whose effective settings are TeamSettings.
    private void SetupTeam() => SetupTeams((TeamId, false, TeamSettings));

    // Pages the teams, each with its disabled flag, and resolves the effective settings of each enabled one.
    private void SetupTeams(
        params (string TeamId, bool IsDisabled, IReadOnlySettingCollection Settings)[] teams
    )
    {
        SetupTeamPage(teams.Select(team => (team.TeamId, team.IsDisabled)).ToArray());
        foreach (var team in teams.Where(team => !team.IsDisabled))
            SetupTeamSettings(team.TeamId, team.Settings);
    }

    private void SetupTeamSettings(string teamId, IReadOnlySettingCollection settings) =>
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings)
            .Verifiable();

    // Pages the teams only, each with its disabled flag.
    private void SetupTeamPage(params (string TeamId, bool IsDisabled)[] teams)
    {
        PersistedTeam[] items =
        [
            .. teams.Select(team => CreateTeam(team.TeamId, team.IsDisabled, [])),
        ];
        MockTeamStore
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new PagedResult<PersistedTeam> { Items = items, NextCursor = null })
            .Verifiable();
    }

    private void SetupStoreManager()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    // Every test that saves commits one tenant's change exactly once.
    private void SetupSave() =>
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

    private void SetupTenantPage(string? cursor, string? nextCursor, params string[] tenantIds) =>
        SetupTeamTenantPage(TeamId, cursor, nextCursor, tenantIds);

    private void SetupTeamTenantPage(
        string teamId,
        string? cursor,
        string? nextCursor,
        params string[] tenantIds
    ) =>
        MockTenantStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    teamId,
                    It.Is<PageRequest>(page => page.Cursor == cursor && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTenantSettings>
                {
                    Items = [.. tenantIds.Select(CreateTenantSettings)],
                    NextCursor = nextCursor,
                }
            )
            .Verifiable();

    private static PersistedTenantSettings CreateTenantSettings(string tenantId) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            Settings = JsonElement.Parse($$"""{"tenant":"{{tenantId}}"}"""),
        }.BuildSettings();

    private Dictionary<string, IReadOnlySettingCollection> TenantSettings { get; } = [];

    private void SetupTenantSettings(string tenantId, IReadOnlySettingCollection? parent = null)
    {
        var settings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        TenantSettings[tenantId] = settings;
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    parent ?? TeamSettings,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => IsTenantDocument(e, tenantId))
                )
            )
            .Returns(settings)
            .Verifiable();
    }

    private void SetupSecrets(string tenantId, IReadOnlyCollection<PersistedSecret>? secrets)
    {
        var tenantSecrets = secrets is null
            ? null
            : new PersistedTenantBuilder { TenantId = tenantId, Secrets = secrets }.BuildSecrets();
        MockTenantStore
            .Setup(x => x.GetSecretsOrDefaultAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantSecrets)
            .Verifiable();
    }

    private TenantKeyRolloverPlan SetupPlan(IReadOnlyCollection<PersistedSecret> held, bool empty)
    {
        var plan = new TenantKeyRolloverPlan { Now = Now };
        if (!empty)
        {
            plan.Removals.Add(held.First());
        }

        MockPlanner
            .Setup(x =>
                x.Plan(
                    It.Is<IReadOnlyList<TenantKeySpecification>>(specifications =>
                        specifications.SequenceEqual(new[] { TenantKeyTestData.Rs256 })
                    ),
                    held,
                    It.IsAny<IReadOnlySettingCollection>(),
                    Now
                )
            )
            .Returns(plan)
            .Verifiable();
        return plan;
    }

    private void SetupApply(
        string tenantId,
        IReadOnlyCollection<PersistedSecret> held,
        TenantKeyRolloverPlan plan,
        params TenantKeyAuditEvent[] auditEvents
    ) =>
        MockRollover
            .Setup(x =>
                x.ApplyAsync(
                    MockTenantStore.Object,
                    tenantId,
                    held,
                    plan,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new TenantKeyRolloverResult { Secrets = [], AuditEvents = [.. auditEvents] }
            )
            .Verifiable();

    private static TenantKeyAuditEvent CreateAuditEvent(string secretId) =>
        new()
        {
            EventId = Guid.CreateVersion7(Now),
            EventTime = Now,
            EventSource = AuditEventSources.OpenId,
            TenantId = FirstTenantId,
            Actor = AuditActor.System,
            Outcome = AuditOutcome.Success,
            Change = TenantKeyChanges.Removed,
            Reason = TenantKeyChangeReasons.Expired,
            SecretId = secretId,
            KeyId = secretId,
            SecretType = SecretKeyTypes.Rsa,
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            ActivatesWhen = Now.AddDays(-30),
            RetiresWhen = Now.AddDays(60),
            ExpiresWhen = Now.AddDays(67),
        };

    // The contract of the shared-state port's add-if-absent: an entry refuses a second add until it has expired on
    // the clock it is judged by.
    private sealed class FakeExpiringKeySet(TimeProvider timeProvider) : IExpiringKeySet
    {
        private TimeProvider TimeProvider { get; } = timeProvider;
        private Dictionary<string, DateTimeOffset> Entries { get; } = [];

        public int Taken { get; private set; }

        public ValueTask<bool> TryAddAsync(
            string key,
            DateTimeOffset expiresWhen,
            CancellationToken cancellationToken
        )
        {
            var now = TimeProvider.GetUtcNow();
            if (Entries.TryGetValue(key, out var existing) && existing > now)
                return ValueTask.FromResult(false);

            Entries[key] = expiresWhen;
            Taken++;
            return ValueTask.FromResult(true);
        }

        public ValueTask<bool> ContainsAsync(string key, CancellationToken cancellationToken) =>
            ValueTask.FromResult(
                Entries.TryGetValue(key, out var existing) && existing > TimeProvider.GetUtcNow()
            );
    }

    private void SetupAnnouncement(string tenantId) =>
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<TenantChangedCommand>(command =>
                        command.TenantId == tenantId && !command.SettingsChanged
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    #endregion
}
