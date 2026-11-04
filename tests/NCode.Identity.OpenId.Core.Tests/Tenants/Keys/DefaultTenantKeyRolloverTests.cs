// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants.Keys;
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
    private Mock<IEventPublisher> MockEventPublisher { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private List<TenantKeyAuditEvent> Published { get; } = [];
    private DefaultTenantKeyRollover Rollover { get; }

    public DefaultTenantKeyRolloverTests()
    {
        MockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        MockSecretProtectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        MockSecretProtector = CreateStrictMock<ISecretProtector>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockEventPublisher = CreateStrictMock<IEventPublisher>();
        MockTenantStore = CreateStrictMock<ITenantStore>();

        Rollover = new DefaultTenantKeyRollover(
            MockSecretGenerator.Object,
            MockSecretProtectorProvider.Object,
            MockCryptoService.Object,
            MockEventPublisher.Object,
            CreateLooseMock<ILogger<DefaultTenantKeyRollover>>().Object
        );
    }

    private static TenantKeySpecification Rs256 => TenantKeyTestData.Rs256;

    #region ApplyAsync Tests

    [Fact]
    public async Task ApplyAsync_WhenThePlanGeneratesAKey_GeneratesItWithItsLifecycleAndRecordsIt()
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
        SetupPublish();

        var secrets = await Rollover.ApplyAsync(
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
        var generated = Assert.Single(added);
        Assert.Equal([generated], secrets);

        var auditEvent = Assert.Single(Published);
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
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Generations.Add(CreateGeneration());
        plan.Generations.Add(CreateGeneration());
        SetupGeneration();
        SetupAddSecret();
        SetupPublish();

        await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [],
            plan,
            CancellationToken.None
        );

        MockSecretProtectorProvider.Verify(
            x =>
                x.GetProtectorAsync(
                    SecretOwner.ForTenant(TenantId),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanRetiresAKey_UpdatesItsLifecycleAndRecordsIt()
    {
        // The protector provider is strict with no setup: retiring a key never loads the master key.
        var held = CreateSecret("held", Now.AddDays(-30), Now.AddDays(60));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Retirements.Add(
            new TenantKeyRetirement
            {
                Secret = held,
                RetiresWhen = Now,
                ExpiresWhen = Now.AddDays(7),
            }
        );
        var updated = SetupUpdateSecret();
        SetupPublish();

        var secrets = await Rollover.ApplyAsync(
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
        Assert.Equal(held.ActivatesWhen, retired.ActivatesWhen);
        Assert.Equal(Now, retired.RetiresWhen);
        Assert.Equal(Now.AddDays(7), retired.ExpiresWhen);
        Assert.Equal([retired], secrets);

        var auditEvent = Assert.Single(Published);
        Assert.Equal(TenantKeyChanges.Retired, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.UnusedAlgorithm, auditEvent.Reason);
        Assert.Equal(Now, auditEvent.RetiresWhen);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanActivatesAKey_MovesItsActivationAndRecordsIt()
    {
        var held = CreateSecret("held", Now.AddDays(1), Now.AddDays(91));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Activations.Add(new TenantKeyActivation { Secret = held, ActivatesWhen = Now });
        var updated = SetupUpdateSecret();
        SetupPublish();

        var secrets = await Rollover.ApplyAsync(
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
        Assert.Equal([activated], secrets);

        var auditEvent = Assert.Single(Published);
        Assert.Equal(TenantKeyChanges.Activated, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.NoActiveKey, auditEvent.Reason);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanRemovesAKey_RemovesItFromTheStoreAndRecordsIt()
    {
        var kept = CreateSecret("kept", Now.AddDays(-30), Now.AddDays(60));
        var expired = CreateSecret("expired", Now.AddDays(-200), Now.AddDays(-110));
        var plan = new TenantKeyRolloverPlan { Now = Now };
        plan.Removals.Add(expired);
        MockTenantStore
            .Setup(x => x.RemoveSecretAsync(TenantId, "expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        SetupPublish();

        var secrets = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [kept, expired],
            plan,
            CancellationToken.None
        );

        Assert.Equal([kept], secrets);
        var auditEvent = Assert.Single(Published);
        Assert.Equal(TenantKeyChanges.Removed, auditEvent.Change);
        Assert.Equal(TenantKeyChangeReasons.Expired, auditEvent.Reason);
        Assert.Equal("expired", auditEvent.SecretId);
    }

    [Fact]
    public async Task ApplyAsync_WhenThePlanIsEmpty_ChangesNothingAndRecordsNothing()
    {
        // Every mock is strict with no setup: any store call, generation or event fails the test.
        var held = CreateSecret("held", Now.AddDays(-30), Now.AddDays(60));

        var secrets = await Rollover.ApplyAsync(
            MockTenantStore.Object,
            TenantId,
            [held],
            new TenantKeyRolloverPlan { Now = Now },
            CancellationToken.None
        );

        Assert.Equal([held], secrets);
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

    private static PersistedSecret CreateSecret(
        string secretId,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen
    ) =>
        TenantKeyTestData.CreateSecret(
            secretId,
            activatesWhen,
            activatesWhen,
            retiresWhen,
            retiresWhen.AddDays(7)
        );

    private void SetupPublish() =>
        MockEventPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<TenantKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (TenantKeyAuditEvent auditEvent, CancellationToken _) => Published.Add(auditEvent)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

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
            .Verifiable();

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
