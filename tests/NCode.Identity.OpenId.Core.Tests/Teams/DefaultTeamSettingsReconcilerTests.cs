// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Teams;

public sealed class DefaultTeamSettingsReconcilerTests : BaseTests
{
    private const string TeamId = "acme";
    private const string ConcurrencyToken = "token-1";

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private static SettingDescriptor<bool> FlagDescriptor { get; } =
        new() { Name = "flag", AppliesTo = ["team"] };

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IOpenIdEnvironmentProvider> MockOpenIdEnvironmentProvider { get; }
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingCollectionFactory> MockSettingCollectionFactory { get; }
    private Mock<IExpiringKeySet> MockExpiringKeySet { get; }
    private Mock<IMediator> MockMediator { get; }
    private IReadOnlySettingCollection Settings { get; }
    private string Fingerprint { get; }
    private string SnapshotText { get; }
    private ServiceProvider ServiceProvider { get; }
    private DefaultTeamSettingsReconciler Reconciler { get; }

    public DefaultTeamSettingsReconcilerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTeamStore = CreateStrictMock<ITeamStore>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockOpenIdEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
        MockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        MockSettingCollectionFactory = CreateStrictMock<ISettingCollectionFactory>();
        MockExpiringKeySet = CreateStrictMock<IExpiringKeySet>();
        MockMediator = CreateStrictMock<IMediator>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(
            CreateLooseMock<ISettingDescriptorCollectionProvider>().Object
        );
        serviceCollection.AddSettingsLibrary();
        serviceCollection.AddScoped(_ => MockMediator.Object);
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Settings = ServiceProvider
            .GetRequiredService<ISettingCollectionFactory>()
            .Create([new Setting<bool>(FlagDescriptor, true)]);
        Fingerprint = SettingsFingerprint.Compute(Settings, JsonOptions);
        SnapshotText = SettingsSnapshot.Write(Settings, JsonOptions).GetRawText();

        Reconciler = new DefaultTeamSettingsReconciler(
            MockStoreManagerFactory.Object,
            MockEffectiveSettingsResolver.Object,
            MockOpenIdEnvironmentProvider.Object,
            MockSettingSerializer.Object,
            MockSettingCollectionFactory.Object,
            MockExpiringKeySet.Object,
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(OpenIdOptions),
            TimeProvider.System,
            CreateLooseMock<ILogger<DefaultTeamSettingsReconciler>>().Object
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ServiceProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    #region ReconcileAsync Tests

    [Fact]
    public async Task ReconcileAsync_WhenTheFingerprintIsRecorded_ReturnsTrueWithoutAPass()
    {
        SetupStoreManager();
        SetupTeam(Fingerprint);
        SetupSettings();

        // Strict mocks: no lease and no command, so taking either would fail the test.
        var result = await Reconciler.ReconcileAsync(TeamId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenAnotherReplicaHoldsTheLease_ReturnsFalse()
    {
        SetupStoreManager();
        SetupTeam(reconciledFingerprint: null);
        SetupSettings();
        SetupLease(isAdded: false);

        var result = await Reconciler.ReconcileAsync(TeamId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheSettingsChanged_SendsTheCommandAndRecordsTheSettings()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var leaseDuration = TimeSpan.FromMinutes(7);
        OpenIdOptions.Team.ReconciliationLeaseDuration = leaseDuration;
        var earliestExpiry = DateTimeOffset.UtcNow + leaseDuration;
        DateTimeOffset? leaseExpiresWhen = null;
        SetupStoreManager();
        SetupTeam(reconciledFingerprint: "stale");
        SetupSettings();
        MockExpiringKeySet
            .Setup(x =>
                x.TryAddAsync(
                    $"team-settings-reconciliation:{TeamId}:{ConcurrencyToken}:{Fingerprint}",
                    It.IsAny<DateTimeOffset>(),
                    cancellationToken
                )
            )
            .Callback(
                (string _, DateTimeOffset expiresWhen, CancellationToken _) =>
                    leaseExpiresWhen = expiresWhen
            )
            .ReturnsAsync(true)
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<ReconcileTeamSettingsCommand>(command =>
                        command.TeamId == TeamId
                        && ReferenceEquals(command.Settings, Settings)
                        && command.PreviousSettings == null
                    ),
                    cancellationToken
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTeamStore
            .Setup(x =>
                x.UpdateReconciledSettingsAsync(
                    It.Is<PersistedTeam>(team =>
                        team.TeamId == TeamId
                        && team.ConcurrencyToken == ConcurrencyToken
                        && team.ReconciledSettingsFingerprint == Fingerprint
                        && team.ReconciledSettings.HasValue
                        && team.ReconciledSettings.Value.GetRawText() == SnapshotText
                    ),
                    cancellationToken
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Reconciler.ReconcileAsync(TeamId, cancellationToken);

        Assert.True(result);
        Assert.NotNull(leaseExpiresWhen);
        Assert.True(leaseExpiresWhen >= earliestExpiry);
    }

    [Fact]
    public async Task ReconcileAsync_WhenSettingsWereReconciledBefore_SendsThemAsThePreviousSettings()
    {
        Setting[] previousList = [new Setting<bool>(FlagDescriptor, false)];
        var previousSettings = CreateStrictMock<ISettingCollection>().Object;
        SetupStoreManager();
        SetupTeam(
            reconciledFingerprint: "stale",
            reconciledSettings: JsonElement.Parse("""{"flag":false}""")
        );
        SetupSettings();
        SetupLease(isAdded: true);
        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.Is<JsonElement>(element => element.GetRawText() == """{"flag":false}"""),
                    OpenIdSettingScopes.Team,
                    JsonOptions
                )
            )
            .Returns(previousList)
            .Verifiable();
        MockSettingCollectionFactory
            .Setup(x => x.Create(previousList))
            .Returns(previousSettings)
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<ReconcileTeamSettingsCommand>(command =>
                        command.TeamId == TeamId
                        && ReferenceEquals(command.Settings, Settings)
                        && ReferenceEquals(command.PreviousSettings, previousSettings)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupRecord();

        var result = await Reconciler.ReconcileAsync(TeamId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenThePreviousSettingsCannotBeRead_SendsNoPreviousSettings()
    {
        SetupStoreManager();
        SetupTeam(
            reconciledFingerprint: "stale",
            reconciledSettings: JsonElement.Parse("""{"flag":false}""")
        );
        SetupSettings();
        SetupLease(isAdded: true);
        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Team,
                    JsonOptions
                )
            )
            .Throws(new JsonException("unreadable"))
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<ReconcileTeamSettingsCommand>(command =>
                        command.TeamId == TeamId
                        && ReferenceEquals(command.Settings, Settings)
                        && command.PreviousSettings == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupRecord();

        var result = await Reconciler.ReconcileAsync(TeamId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheRecordLosesARace_ReturnsFalse()
    {
        SetupStoreManager();
        SetupTeam(reconciledFingerprint: null);
        SetupSettings();
        SetupLease(isAdded: true);
        MockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<ReconcileTeamSettingsCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTeamStore
            .Setup(x =>
                x.UpdateReconciledSettingsAsync(
                    It.IsAny<PersistedTeam>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Verifiable();

        var result = await Reconciler.ReconcileAsync(TeamId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheTeamIsNotPersisted_ReturnsTrue()
    {
        SetupStoreManager();
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedTeam?)null)
            .Verifiable();

        var result = await Reconciler.ReconcileAsync(TeamId, CancellationToken.None);

        Assert.True(result);
    }

    #endregion

    #region Scaffolding

    private static bool HasVersion(JsonElement element, int version) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("v", out var value)
        && value.GetInt32() == version;

    private static PersistedTeam CreatePersistedTeam(
        string? reconciledFingerprint,
        JsonElement? reconciledSettings
    ) =>
        new PersistedTeamBuilder
        {
            TeamId = TeamId,
            DisplayName = "Acme",
            ConcurrencyToken = ConcurrencyToken,
            Settings = JsonElement.Parse("""{"v":1}"""),
            ReconciledSettingsFingerprint = reconciledFingerprint,
            ReconciledSettings = reconciledSettings,
        }.Build();

    private void SetupStoreManager()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITeamStore>())
            .Returns(MockTeamStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    // The store returns a new row per call, as a store does for each unit of work.
    private void SetupTeam(string? reconciledFingerprint, JsonElement? reconciledSettings = null) =>
        MockTeamStore
            .Setup(x => x.GetOrDefaultAsync(TeamId, It.IsAny<CancellationToken>()))
            .Returns(() =>
                ValueTask.FromResult<PersistedTeam?>(
                    CreatePersistedTeam(reconciledFingerprint, reconciledSettings)
                )
            )
            .Verifiable();

    private void SetupRecord()
    {
        MockTeamStore
            .Setup(x =>
                x.UpdateReconciledSettingsAsync(
                    It.IsAny<PersistedTeam>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private void SetupSettings()
    {
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveTeamAsync(
                    TeamId,
                    It.Is<JsonElement>(element => HasVersion(element, 1)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Settings)
            .Verifiable();
        MockOpenIdEnvironmentProvider
            .Setup(x => x.Get())
            .Returns(MockOpenIdEnvironment.Object)
            .Verifiable();
        MockOpenIdEnvironment.Setup(x => x.JsonSerializerOptions).Returns(JsonOptions).Verifiable();
    }

    private void SetupLease(bool isAdded) =>
        MockExpiringKeySet
            .Setup(x =>
                x.TryAddAsync(
                    $"team-settings-reconciliation:{TeamId}:{ConcurrencyToken}:{Fingerprint}",
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(isAdded)
            .Verifiable();

    #endregion
}
