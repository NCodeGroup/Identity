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

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public sealed class DefaultServerSettingsReconcilerTests : BaseTests
{
    private const string ServerId = "default";
    private const string ConcurrencyToken = "token-1";

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private static SettingDescriptor<bool> FlagDescriptor { get; } =
        new() { Name = "flag", AppliesTo = ["server"] };

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IServerStore> MockServerStore { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IOpenIdEnvironmentProvider> MockOpenIdEnvironmentProvider { get; }
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }
    private Mock<ISettingSerializer> MockSettingSerializer { get; }
    private Mock<ISettingCollectionFactory> MockSettingCollectionFactory { get; }
    private Mock<IExpiringKeySet> MockExpiringKeySet { get; }
    private Mock<IMediator> MockMediator { get; }
    private IReadOnlySettingCollection Settings { get; }
    private string Fingerprint { get; }
    private string SettingsSnapshot { get; }
    private ServiceProvider ServiceProvider { get; }
    private DefaultServerSettingsReconciler Reconciler { get; }

    public DefaultServerSettingsReconcilerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockServerStore = CreateStrictMock<IServerStore>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockOpenIdEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
        MockSettingSerializer = CreateStrictMock<ISettingSerializer>();
        MockSettingCollectionFactory = CreateStrictMock<ISettingCollectionFactory>();
        MockExpiringKeySet = CreateStrictMock<IExpiringKeySet>();
        MockMediator = CreateStrictMock<IMediator>();

        Settings = new DefaultSettingCollection(
            CreateLooseMock<ISettingDescriptorCollectionProvider>().Object,
            [new Setting<bool>(FlagDescriptor, true)]
        );
        Fingerprint = ServerSettingsFingerprint.Compute(Settings, JsonOptions);
        SettingsSnapshot = ServerSettingsSnapshot.Write(Settings, JsonOptions).GetRawText();

        ServiceProvider = new ServiceCollection()
            .AddScoped(_ => MockMediator.Object)
            .BuildServiceProvider();

        Reconciler = new DefaultServerSettingsReconciler(
            MockStoreManagerFactory.Object,
            MockEffectiveSettingsResolver.Object,
            MockOpenIdEnvironmentProvider.Object,
            MockSettingSerializer.Object,
            MockSettingCollectionFactory.Object,
            MockExpiringKeySet.Object,
            ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(OpenIdOptions),
            TimeProvider.System,
            CreateLooseMock<ILogger<DefaultServerSettingsReconciler>>().Object
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
        SetupServer(Fingerprint);
        SetupSettings();

        // Strict mocks: no lease and no command, so taking either would fail the test.
        var result = await Reconciler.ReconcileAsync(ServerId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenAnotherReplicaHoldsTheLease_ReturnsFalse()
    {
        SetupStoreManager();
        SetupServer(reconciledFingerprint: null);
        SetupSettings();
        SetupLease(isAdded: false);

        var result = await Reconciler.ReconcileAsync(ServerId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheSettingsChanged_SendsTheCommandAndRecordsTheSettings()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var leaseDuration = TimeSpan.FromMinutes(7);
        OpenIdOptions.Server.ReconciliationLeaseDuration = leaseDuration;
        var earliestExpiry = DateTimeOffset.UtcNow + leaseDuration;
        DateTimeOffset? leaseExpiresWhen = null;
        SetupStoreManager();
        SetupServer(reconciledFingerprint: "stale");
        SetupSettings();
        MockExpiringKeySet
            .Setup(x =>
                x.TryAddAsync(
                    $"server-settings-reconciliation:{ServerId}:{ConcurrencyToken}:{Fingerprint}",
                    It.IsAny<DateTimeOffset>(),
                    cancellationToken
                )
            )
            .Callback(
                (string _, DateTimeOffset expiresWhen, CancellationToken _) =>
                    leaseExpiresWhen = expiresWhen
            )
            .Returns(ValueTask.FromResult(true))
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<ReconcileServerSettingsCommand>(command =>
                        command.ServerId == ServerId
                        && ReferenceEquals(command.Settings, Settings)
                        && command.PreviousSettings == null
                    ),
                    cancellationToken
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockServerStore
            .Setup(x =>
                x.UpdateReconciledSettingsAsync(
                    It.Is<PersistedServer>(server =>
                        server.ServerId == ServerId
                        && server.ConcurrencyToken == ConcurrencyToken
                        && server.ReconciledSettingsFingerprint == Fingerprint
                        && server.ReconciledSettings.HasValue
                        && server.ReconciledSettings.Value.GetRawText() == SettingsSnapshot
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

        var result = await Reconciler.ReconcileAsync(ServerId, cancellationToken);

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
        SetupServer(
            reconciledFingerprint: "stale",
            reconciledSettings: JsonElement.Parse("""{"flag":false}""")
        );
        SetupSettings();
        SetupLease(isAdded: true);
        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.Is<JsonElement>(element => element.GetRawText() == """{"flag":false}"""),
                    OpenIdSettingScopes.Server,
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
                    It.Is<ReconcileServerSettingsCommand>(command =>
                        command.ServerId == ServerId
                        && ReferenceEquals(command.Settings, Settings)
                        && ReferenceEquals(command.PreviousSettings, previousSettings)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupRecord();

        var result = await Reconciler.ReconcileAsync(ServerId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenThePreviousSettingsCannotBeRead_SendsNoPreviousSettings()
    {
        SetupStoreManager();
        SetupServer(
            reconciledFingerprint: "stale",
            reconciledSettings: JsonElement.Parse("""{"flag":false}""")
        );
        SetupSettings();
        SetupLease(isAdded: true);
        MockSettingSerializer
            .Setup(x =>
                x.DeserializeSettings(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Server,
                    JsonOptions
                )
            )
            .Throws(new JsonException("unreadable"))
            .Verifiable();
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<ReconcileServerSettingsCommand>(command =>
                        command.ServerId == ServerId
                        && ReferenceEquals(command.Settings, Settings)
                        && command.PreviousSettings == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupRecord();

        var result = await Reconciler.ReconcileAsync(ServerId, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheRecordLosesARace_ReturnsFalse()
    {
        SetupStoreManager();
        SetupServer(reconciledFingerprint: null);
        SetupSettings();
        SetupLease(isAdded: true);
        MockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ReconcileServerSettingsCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockServerStore
            .Setup(x =>
                x.UpdateReconciledSettingsAsync(
                    It.IsAny<PersistedServer>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.FromException(new StoreConcurrencyException("conflict")))
            .Verifiable();

        var result = await Reconciler.ReconcileAsync(ServerId, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheServerIsNotPersisted_ReturnsTrue()
    {
        SetupStoreManager();
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<PersistedServer?>(null))
            .Verifiable();

        var result = await Reconciler.ReconcileAsync(ServerId, CancellationToken.None);

        Assert.True(result);
    }

    #endregion

    #region Scaffolding

    private static bool HasVersion(JsonElement element, int version) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("v", out var value)
        && value.GetInt32() == version;

    private static PersistedServer CreatePersistedServer(
        string? reconciledFingerprint,
        JsonElement? reconciledSettings
    ) =>
        new()
        {
            ServerId = ServerId,
            DisplayName = "Default",
            ConcurrencyToken = ConcurrencyToken,
            Settings = new PersistedServerSettings
            {
                ServerId = ServerId,
                Value = JsonElement.Parse("""{"v":1}"""),
            },
            Secrets = new PersistedServerSecrets { ServerId = ServerId, Value = [] },
            ReconciledSettingsFingerprint = reconciledFingerprint,
            ReconciledSettings = reconciledSettings,
        };

    private void SetupStoreManager()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IServerStore>())
            .Returns(MockServerStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    // The store returns a new row per call, as a store does for each unit of work.
    private void SetupServer(
        string? reconciledFingerprint,
        JsonElement? reconciledSettings = null
    ) =>
        MockServerStore
            .Setup(x => x.GetOrDefaultAsync(ServerId, It.IsAny<CancellationToken>()))
            .Returns(() =>
                ValueTask.FromResult<PersistedServer?>(
                    CreatePersistedServer(reconciledFingerprint, reconciledSettings)
                )
            )
            .Verifiable();

    private void SetupRecord()
    {
        MockServerStore
            .Setup(x =>
                x.UpdateReconciledSettingsAsync(
                    It.IsAny<PersistedServer>(),
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
                x.ResolveServerAsync(
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
                    $"server-settings-reconciliation:{ServerId}:{ConcurrencyToken}:{Fingerprint}",
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.FromResult(isAdded))
            .Verifiable();

    #endregion
}
