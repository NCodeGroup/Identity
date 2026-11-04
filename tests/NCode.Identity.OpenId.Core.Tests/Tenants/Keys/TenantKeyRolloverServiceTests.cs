// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyRolloverServiceTests : BaseTests
{
    private const string ServerId = "default";
    private const string FirstTenantId = "acme";
    private const string SecondTenantId = "globex";
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<IMaterializedServerAccessor> MockServerAccessor { get; }
    private Mock<OpenIdServer> MockServer { get; }
    private Mock<ITenantKeyPolicy> MockPolicy { get; }
    private Mock<ITenantKeyRolloverPlanner> MockPlanner { get; }
    private Mock<ITenantKeyRollover> MockRollover { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private Mock<IExpiringKeySet> MockExpiringKeySet { get; }
    private Mock<IMediator> MockMediator { get; }
    private IReadOnlySettingCollection ServerSettings { get; }
    private ServiceProvider ServiceProvider { get; }
    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private TenantKeyRolloverService Service { get; }

    public TenantKeyRolloverServiceTests()
    {
        OpenIdOptions.Tenant.KeyRollover.Interval = Interval;
        MockServerAccessor = CreateStrictMock<IMaterializedServerAccessor>();
        MockServer = CreateStrictMock<OpenIdServer>();
        MockPolicy = CreateStrictMock<ITenantKeyPolicy>();
        MockPlanner = CreateStrictMock<ITenantKeyRolloverPlanner>();
        MockRollover = CreateStrictMock<ITenantKeyRollover>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();
        MockExpiringKeySet = CreateStrictMock<IExpiringKeySet>();
        MockMediator = CreateStrictMock<IMediator>();
        ServerSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;

        ServiceProvider = new ServiceCollection()
            .AddScoped(_ => MockMediator.Object)
            .BuildServiceProvider();

        Service = CreateService(TimeProvider);
    }

    private TenantKeyRolloverService CreateService(TimeProvider timeProvider) =>
        new(
            MockServerAccessor.Object,
            Options.Create(OpenIdOptions),
            MockPolicy.Object,
            MockPlanner.Object,
            MockRollover.Object,
            MockEffectiveSettingsResolver.Object,
            MockStoreManagerFactory.Object,
            MockExpiringKeySet.Object,
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            timeProvider,
            CreateLooseMock<ILogger<TenantKeyRolloverService>>().Object
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
        SetupServerSettings();
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

        await Service.RollOverAsync(ServerId, CancellationToken.None);

        MockStoreManager.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RollOverAsync_WhenAnotherReplicaHoldsTheLease_ReadsNoTenant()
    {
        // The store is strict with no setup: reading a tenant fails the test.
        SetupSpecifications();
        SetupLease(acquired: false);

        await Service.RollOverAsync(ServerId, CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenProvisioningIsDisabled_TakesNoLease()
    {
        // Every mock is strict with no setup: resolving the specifications or taking the lease fails the test.
        KeyProvisioning.Enabled = false;

        await Service.RollOverAsync(ServerId, CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenATenantHoldsNoKey_LeavesItToItsFirstMaterialization()
    {
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupServerSettings();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);

        // Strict mocks: no plan or announcement setup, so planning for a tenant without keys fails the test.
        SetupSecrets(FirstTenantId, secrets: null);
        SetupSecrets(SecondTenantId, secrets: []);

        await Service.RollOverAsync(ServerId, CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenTheSaveLosesARace_LeavesTheTenantToTheWinnerAndContinues()
    {
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupServerSettings();
        SetupStoreManager();
        SetupTenantPage(cursor: null, nextCursor: null, FirstTenantId, SecondTenantId);

        var first = CreateSecret("first");
        var second = CreateSecret("second");
        SetupTenantSettings(FirstTenantId);
        SetupSecrets(FirstTenantId, [first]);
        var firstPlan = SetupPlan([first], empty: false);
        SetupApply(FirstTenantId, [first], firstPlan);
        SetupTenantSettings(SecondTenantId);
        SetupSecrets(SecondTenantId, [second]);
        var secondPlan = SetupPlan([second], empty: false);
        SetupApply(SecondTenantId, [second], secondPlan);
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

        await Service.RollOverAsync(ServerId, CancellationToken.None);
    }

    [Fact]
    public async Task RollOverAsync_WhenATenantFails_ContinuesWithTheNextTenant()
    {
        SetupSpecifications();
        SetupLease(acquired: true);
        SetupServerSettings();
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

        await Service.RollOverAsync(ServerId, CancellationToken.None);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_RollsOverOnceTheServerIsMaterializedAndAgainAtEveryTick()
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
        MockServer.Setup(x => x.ServerId).Returns(ServerId).Verifiable();
        MockServerAccessor
            .Setup(x => x.GetServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockServer.Object)
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
                    $"{TenantKeyRolloverService.LeasePurpose}:{ServerId}",
                    Now + Interval,
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
        using var service = CreateService(mockTimeProvider.Object);

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
                    $"{TenantKeyRolloverService.LeasePurpose}:{ServerId}",
                    Now + Interval,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acquired)
            .Verifiable();

    private void SetupServerSettings() =>
        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServerSettings)
            .Verifiable();

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
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupSave() =>
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupTenantPage(string? cursor, string? nextCursor, params string[] tenantIds) =>
        MockTenantStore
            .Setup(x =>
                x.GetSettingsPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == cursor && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedTenantSettings>
                {
                    Items =
                    [
                        .. tenantIds.Select(tenantId => new PersistedTenantSettings
                        {
                            TenantId = tenantId,
                            Value = JsonElement.Parse($$"""{"tenant":"{{tenantId}}"}"""),
                        }),
                    ],
                    NextCursor = nextCursor,
                }
            )
            .Verifiable();

    private Dictionary<string, IReadOnlySettingCollection> TenantSettings { get; } = [];

    private void SetupTenantSettings(string tenantId)
    {
        var settings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        TenantSettings[tenantId] = settings;
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.Resolve(
                    ServerSettings,
                    OpenIdSettingScopes.Tenant,
                    It.Is<JsonElement>(e => IsTenantDocument(e, tenantId))
                )
            )
            .Returns(settings)
            .Verifiable();
    }

    private void SetupSecrets(string tenantId, IReadOnlyCollection<PersistedSecret>? secrets) =>
        MockTenantStore
            .Setup(x => x.GetSecretsOrDefaultAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                secrets is null
                    ? null
                    : new PersistedTenantSecrets { TenantId = tenantId, Value = secrets }
            )
            .Verifiable();

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
        TenantKeyRolloverPlan plan
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
            .ReturnsAsync([])
            .Verifiable();

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
