// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class DefaultTenantKeyRolloverTests : BaseTests
{
    private const string TenantId = "acme";
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<ISecretProtector> MockSecretProtector { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private DefaultTenantKeyRollover Rollover { get; }

    public DefaultTenantKeyRolloverTests()
    {
        MockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        MockSecretProtectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        MockSecretProtector = CreateStrictMock<ISecretProtector>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockTenantStore = CreateStrictMock<ITenantStore>();

        Rollover = new DefaultTenantKeyRollover(
            MockSecretGenerator.Object,
            MockSecretProtectorProvider.Object,
            MockCryptoService.Object
        );
    }

    private static TenantKeySpecification Rs256 => TenantKeyTestData.Rs256;

    private static TenantKeySpecification RemoteRs256 => TenantKeyTestData.RemoteRs256;

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_WhenThePlanGeneratesAKey_GeneratesItAsManagedWithItsLifecycleAndDescribesIt()
    {
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Generations.Add(
            new TenantKeyGeneration
            {
                Specification = Rs256,
                ActivatesWhen = Now.AddDays(2),
                RetiresWhen = Now.AddDays(92),
                ExpiresWhen = Now.AddDays(99),
                Reason = TenantKeyChangeReasons.Successor,
            }
        );
        var requests = SetupGeneration();
        var added = SetupAddSecret();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [],
            plan,
            CancellationToken.None
        );

        var request = Assert.Single(requests);
        Assert.Equal(Rs256.SecretType, request.SecretType);
        Assert.Equal(Rs256.KeySizeBits, request.GenerationParameters.KeySizeBits);
        Assert.Equal(Rs256.Use, request.Use);
        Assert.Equal(Rs256.Algorithm, request.Algorithm);
        Assert.Equal(Now, request.CreatedWhen);
        Assert.Equal(Now.AddDays(2), request.ActivatesWhen);
        Assert.Equal(Now.AddDays(92), request.RetiresWhen);
        Assert.Equal(Now.AddDays(99), request.ExpiresWhen);
        Assert.Equal(SecretManagers.TenantKeyRollover, request.ManagedBy);
        var generated = Assert.Single(added);
        Assert.Equal([generated], result.Secrets);

        var auditEvent = Assert.Single(result.AuditEvents);
        Assert.Equal(TenantKeyChanges.Generated, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.Successor, auditEvent.Reason);
        Assert.Equal(generated.SecretId, auditEvent.SecretId);
        Assert.Equal(TenantId, auditEvent.TenantId);
        Assert.Equal(AuditActor.System, auditEvent.Actor);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(AuditEventSources.OpenId, auditEvent.EventSource);
        Assert.Equal(Now, auditEvent.EventTime);
        Assert.Equal(generated.ActivatesWhen, auditEvent.ActivatesWhen);
        Assert.Equal(generated.RetiresWhen, auditEvent.RetiresWhen);
        Assert.Equal(generated.ExpiresWhen, auditEvent.ExpiresWhen);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanGeneratesSeveralKeys_LoadsTheTenantsProtectorOnce()
    {
        // The protector setup is verified once: a second load fails the test.
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Generations.Add(CreateGeneration());
        plan.Generations.Add(CreateGeneration());
        SetupGeneration();
        var added = SetupAddSecret();

        await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [],
            plan,
            CancellationToken.None
        );

        Assert.Equal(2, added.Count);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanGeneratesARemoteKey_GeneratesItThroughTheRemoteSeamAndNeverLoadsTheProtector()
    {
        // The protector provider and the local generator are strict with no setup: a remote key is created in its
        // key management service and never touches the master key ring.
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Generations.Add(CreateRemoteGeneration());
        var requests = SetupRemoteGeneration();
        var added = SetupAddSecret();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [],
            plan,
            CancellationToken.None
        );

        var request = Assert.Single(requests);
        Assert.Equal(SecretOwner.ForTenant(TenantId), request.Owner);
        Assert.Equal(TenantKeyTestData.RemoteProviderName, request.Provider);
        Assert.Equal(RemoteRs256.SecretType, request.SecretType);
        Assert.Equal(RemoteRs256.KeySizeBits, request.KeySizeBits);
        Assert.Equal(RemoteRs256.Curve, request.Curve);
        Assert.Equal(RemoteRs256.Algorithm, request.Algorithm);
        Assert.Equal(Now, request.CreatedWhen);
        Assert.Equal(Now, request.ActivatesWhen);
        Assert.Equal(Now.AddDays(90), request.RetiresWhen);
        Assert.Equal(Now.AddDays(97), request.ExpiresWhen);
        Assert.Equal(SecretManagers.TenantKeyRollover, request.ManagedBy);
        var generated = Assert.Single(added);
        Assert.Equal([generated], result.Secrets);
        Assert.Empty(result.RemovedSecrets);

        var auditEvent = Assert.Single(result.AuditEvents);
        Assert.Equal(TenantKeyChanges.Generated, auditEvent.Change);
        Assert.Equal(generated.SecretId, auditEvent.SecretId);
        Assert.Equal(TenantKeyTestData.RemoteProviderName, auditEvent.RemoteKeyProvider);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanGeneratesLocalAndRemoteKeys_LoadsTheTenantsProtectorOnce()
    {
        // The protector setup is verified once: a remote generation never loads it, and the local ones share one load.
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Generations.Add(CreateRemoteGeneration());
        plan.Generations.Add(CreateGeneration());
        plan.Generations.Add(CreateRemoteGeneration());
        plan.Generations.Add(CreateGeneration());
        var remoteRequests = SetupRemoteGeneration();
        var localRequests = SetupGeneration();
        var added = SetupAddSecret();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [],
            plan,
            CancellationToken.None
        );

        Assert.Equal(2, remoteRequests.Count);
        Assert.Equal(2, localRequests.Count);
        Assert.Equal(4, added.Count);
        Assert.Equal(
            [
                TenantKeyTestData.RemoteProviderName,
                null,
                TenantKeyTestData.RemoteProviderName,
                null,
            ],
            result.AuditEvents.Select(auditEvent => auditEvent.RemoteKeyProvider)
        );
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanRetiresAKey_UpdatesItsLifecycleAndDescribesIt()
    {
        // The protector provider is strict with no setup: retiring a key never loads the master key.
        var held = CreateSecret("held", Now.AddDays(-30), Now.AddDays(60));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Retirements.Add(
            new TenantKeyRetirement
            {
                Secret = held,
                ActivatesWhen = held.ActivatesWhen,
                RetiresWhen = Now,
                ExpiresWhen = Now.AddDays(7),
                Reason = TenantKeyChangeReasons.UnusedAlgorithm,
            }
        );
        var updated = SetupUpdateSecret();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [held],
            plan,
            CancellationToken.None
        );

        var retired = Assert.Single(updated);
        Assert.Equal(held.SecretId, retired.SecretId);
        Assert.Equal(held.ConcurrencyToken, retired.ConcurrencyToken);
        Assert.Equal(held.EncodedValue, retired.EncodedValue);
        Assert.Equal(held.ManagedBy, retired.ManagedBy);
        Assert.Equal(held.ActivatesWhen, retired.ActivatesWhen);
        Assert.Equal(Now, retired.RetiresWhen);
        Assert.Equal(Now.AddDays(7), retired.ExpiresWhen);
        Assert.Equal([retired], result.Secrets);

        var auditEvent = Assert.Single(result.AuditEvents);
        Assert.Equal(TenantKeyChanges.Retired, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.UnusedAlgorithm, auditEvent.Reason);
        Assert.Equal(Now, auditEvent.RetiresWhen);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanRetiresAKeyForACustodyChange_RecordsTheRetirementReason()
    {
        var held = CreateSecret("held", Now.AddDays(-30), Now.AddDays(60));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Retirements.Add(
            new TenantKeyRetirement
            {
                Secret = held,
                ActivatesWhen = held.ActivatesWhen,
                RetiresWhen = Now.AddDays(2),
                ExpiresWhen = Now.AddDays(9),
                Reason = TenantKeyChangeReasons.CustodyChanged,
            }
        );
        SetupUpdateSecret();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [held],
            plan,
            CancellationToken.None
        );

        var auditEvent = Assert.Single(result.AuditEvents);
        Assert.Equal(TenantKeyChanges.Retired, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.CustodyChanged, auditEvent.Reason);
        Assert.Equal(Now.AddDays(2), auditEvent.RetiresWhen);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanExtendsAKey_MovesItsExpiryAndDescribesIt()
    {
        var held = CreateSecret("held", Now.AddDays(-30), Now.AddDays(60));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Extensions.Add(
            new TenantKeyExtension { Secret = held, ExpiresWhen = Now.AddDays(90) }
        );
        var updated = SetupUpdateSecret();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [held],
            plan,
            CancellationToken.None
        );

        var extended = Assert.Single(updated);
        Assert.Equal(held.ActivatesWhen, extended.ActivatesWhen);
        Assert.Equal(held.RetiresWhen, extended.RetiresWhen);
        Assert.Equal(Now.AddDays(90), extended.ExpiresWhen);
        Assert.Equal([extended], result.Secrets);

        var auditEvent = Assert.Single(result.AuditEvents);
        Assert.Equal(TenantKeyChanges.Extended, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.RetirementGraceRaised, auditEvent.Reason);
        Assert.Equal(Now.AddDays(90), auditEvent.ExpiresWhen);
    }

    [Fact]
    public async Task ApplyAsync_WhenAChangeLeavesTheLifecycleOutOfOrder_ThrowsBeforeWriting()
    {
        // The store is strict with no setup: a record out of order is never written.
        var held = CreateSecret("held", Now.AddDays(2), Now.AddDays(92));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Retirements.Add(
            new TenantKeyRetirement
            {
                Secret = held,
                ActivatesWhen = held.ActivatesWhen,
                RetiresWhen = Now,
                ExpiresWhen = Now.AddDays(7),
                Reason = TenantKeyChangeReasons.UnusedAlgorithm,
            }
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Rollover.ApplyAsync(
                MockTenantStore.Object,
                TenantId,
                [held],
                plan,
                CancellationToken.None
            )
        );

        Assert.Contains("cannot retire", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanActivatesAKey_MovesItsActivationAndDescribesIt()
    {
        var held = CreateSecret(
            "held",
            Now.AddDays(1),
            Now.AddDays(91),
            createdWhen: Now.AddDays(-1)
        );
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Activations.Add(new TenantKeyActivation { Secret = held, ActivatesWhen = Now });
        var updated = SetupUpdateSecret();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [held],
            plan,
            CancellationToken.None
        );

        var activated = Assert.Single(updated);
        Assert.Equal(Now, activated.ActivatesWhen);
        Assert.Equal(held.RetiresWhen, activated.RetiresWhen);
        Assert.Equal(held.ExpiresWhen, activated.ExpiresWhen);
        Assert.Equal([activated], result.Secrets);

        var auditEvent = Assert.Single(result.AuditEvents);
        Assert.Equal(TenantKeyChanges.Activated, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.NoActiveKey, auditEvent.Reason);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanActivatesAndExtendsOneKey_KeepsTheActivationInTheExtension()
    {
        // The extension is written after the activation, from the key as the activation left it: one written from
        // the record the plan read would put the future activation back and leave the algorithm without a key.
        var held = CreateSecret(
            "held",
            Now.AddDays(1),
            Now.AddDays(91),
            createdWhen: Now.AddDays(-1)
        );
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Activations.Add(new TenantKeyActivation { Secret = held, ActivatesWhen = Now });
        plan.Extensions.Add(
            new TenantKeyExtension { Secret = held, ExpiresWhen = Now.AddDays(121) }
        );
        var updated = SetupUpdateSecret();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [held],
            plan,
            CancellationToken.None
        );

        Assert.Collection(
            updated,
            activated =>
            {
                Assert.Equal(Now, activated.ActivatesWhen);
                Assert.Equal(held.ExpiresWhen, activated.ExpiresWhen);
            },
            extended =>
            {
                Assert.Equal(Now, extended.ActivatesWhen);
                Assert.Equal(held.RetiresWhen, extended.RetiresWhen);
                Assert.Equal(Now.AddDays(121), extended.ExpiresWhen);
            }
        );
        var stored = Assert.Single(result.Secrets);
        Assert.Equal(Now, stored.ActivatesWhen);
        Assert.Equal(Now.AddDays(121), stored.ExpiresWhen);
        Assert.Collection(
            result.AuditEvents,
            activation => Assert.Equal(TenantKeyChanges.Activated, activation.Change),
            extension =>
            {
                Assert.Equal(TenantKeyChanges.Extended, extension.Change);
                Assert.Equal(Now, extension.ActivatesWhen);
                Assert.Equal(Now.AddDays(121), extension.ExpiresWhen);
            }
        );
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanRemovesAKey_RemovesItFromTheStoreAndDescribesIt()
    {
        var kept = CreateSecret("kept", Now.AddDays(-30), Now.AddDays(60));
        var expired = CreateSecret("expired", Now.AddDays(-200), Now.AddDays(-110));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Removals.Add(expired);
        MockTenantStore
            .Setup(x => x.RemoveSecretAsync(TenantId, "expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [kept, expired],
            plan,
            CancellationToken.None
        );

        Assert.Equal([kept], result.Secrets);
        Assert.Equal([expired], result.RemovedSecrets);
        var auditEvent = Assert.Single(result.AuditEvents);
        Assert.Equal(TenantKeyChanges.Removed, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.Expired, auditEvent.Reason);
        Assert.Equal("expired", auditEvent.SecretId);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanRemovesARemoteKey_ReportsItWithoutDestroyingItAndNamesItsProvider()
    {
        // The generator is strict with no setup: the rollover only reports the removed key.
        var expired = CreateRemoteSecret("expired", Now.AddDays(-200), Now.AddDays(-110));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Removals.Add(expired);
        MockTenantStore
            .Setup(x => x.RemoveSecretAsync(TenantId, "expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [expired],
            plan,
            CancellationToken.None
        );

        Assert.Empty(result.Secrets);
        Assert.Equal([expired], result.RemovedSecrets);
        var auditEvent = Assert.Single(result.AuditEvents);
        Assert.Equal(TenantKeyChanges.Removed, auditEvent.Change);
        Assert.Equal(TenantKeyTestData.RemoteProviderName, auditEvent.RemoteKeyProvider);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanIsEmpty_ChangesNothingAndDescribesNothing()
    {
        // Every mock is strict with no setup: any store call or generation fails the test.
        var held = CreateSecret("held", Now.AddDays(-30), Now.AddDays(60));

        var result = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [held],
            new TenantKeyRolloverPlan { Now = Now },
            CancellationToken.None
        );

        Assert.Equal([held], result.Secrets);
        Assert.Empty(result.AuditEvents);
        Assert.Empty(result.RemovedSecrets);
    }

    #endregion

    #region Helpers

    private static TenantKeyGeneration CreateGeneration() =>
        new()
        {
            Specification = Rs256,
            ActivatesWhen = Now,
            RetiresWhen = Now.AddDays(90),
            ExpiresWhen = Now.AddDays(97),
            Reason = TenantKeyChangeReasons.NoActiveKey,
        };

    private static TenantKeyGeneration CreateRemoteGeneration() =>
        CreateGeneration() with
        {
            Specification = RemoteRs256,
        };

    private static PersistedSecret CreateRemoteSecret(
        string secretId,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset? createdWhen = null
    ) =>
        CreateSecret(secretId, activatesWhen, retiresWhen, createdWhen) with
        {
            SecretEncoding = SecretEncodings.Remote,
            EncodedValue = $"{TenantKeyTestData.RemoteProviderName}:{secretId}",
        };

    private static PersistedSecret CreateSecret(
        string secretId,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen,
        DateTimeOffset? createdWhen = null
    ) =>
        TenantKeyTestData.CreateSecret(
            secretId,
            createdWhen ?? activatesWhen,
            activatesWhen,
            retiresWhen,
            retiresWhen.AddDays(7)
        );

    private List<GenerateSecretRequest> SetupGeneration()
    {
        var count = 0;
        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(() => $"generated-{++count}")
            .Verifiable();

        MockSecretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant(TenantId),
                    It.Is<IReadOnlyCollection<string>>(values => values.Count == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(MockSecretProtector.Object)
            .Verifiable(Times.Once);

        var requests = new List<GenerateSecretRequest>();
        MockSecretGenerator
            .Setup(x =>
                x.GenerateSecret(MockSecretProtector.Object, It.IsAny<GenerateSecretRequest>())
            )
            .Returns(
                (ISecretProtector _, GenerateSecretRequest request) =>
                {
                    requests.Add(request);
                    return new GeneratedSecret
                    {
                        Secret = TenantKeyTestData.CreateSecret(
                            request.SecretId,
                            request.CreatedWhen,
                            request.ActivatesWhen ?? request.CreatedWhen,
                            request.RetiresWhen ?? request.ExpiresWhen,
                            request.ExpiresWhen
                        ),
                        SecretMaterial = null,
                    };
                }
            )
            .Verifiable();
        return requests;
    }

    private List<GenerateRemoteSecretRequest> SetupRemoteGeneration()
    {
        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(() => $"remote-{Guid.NewGuid():N}")
            .Verifiable();

        var requests = new List<GenerateRemoteSecretRequest>();
        MockSecretGenerator
            .Setup(x =>
                x.GenerateRemoteSecretAsync(
                    It.IsAny<GenerateRemoteSecretRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (GenerateRemoteSecretRequest request, CancellationToken _) =>
                {
                    requests.Add(request);
                    return CreateRemoteSecret(
                        request.SecretId,
                        request.ActivatesWhen ?? request.CreatedWhen,
                        request.RetiresWhen ?? request.ExpiresWhen,
                        request.CreatedWhen
                    );
                }
            )
            .Verifiable();
        return requests;
    }

    private List<PersistedSecret> SetupAddSecret()
    {
        var added = new List<PersistedSecret>();
        MockTenantStore
            .Setup(x =>
                x.AddSecretAsync(
                    TenantId,
                    It.IsAny<PersistedSecret>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((string _, PersistedSecret secret, CancellationToken _) => added.Add(secret))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        return added;
    }

    private List<PersistedSecret> SetupUpdateSecret()
    {
        var updated = new List<PersistedSecret>();
        MockTenantStore
            .Setup(x =>
                x.UpdateSecretAsync(
                    TenantId,
                    It.IsAny<PersistedSecret>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (string _, PersistedSecret secret, CancellationToken _) => updated.Add(secret)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        return updated;
    }

    #endregion
}
