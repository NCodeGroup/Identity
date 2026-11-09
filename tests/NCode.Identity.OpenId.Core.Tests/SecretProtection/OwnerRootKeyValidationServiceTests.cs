// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class OwnerRootKeyValidationServiceTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static SecretOwner TenantOwner { get; } = SecretOwner.ForTenant("acme");
    private static SecretOwner OtherOwner { get; } = SecretOwner.ForTenant("globex");

    private OwnerRootKeyOptions RootKeyOptions { get; } = new();
    private FakeTimeProvider Time { get; } = new(Now);
    private Mock<IMaterializedPlatformAccessor> PlatformAccessor { get; }
    private Mock<IOwnerRootKeyManager> Manager { get; }
    private Mock<IStoreManagerFactory> StoreManagerFactory { get; }
    private Mock<IOwnerRootKeyStore> Store { get; }
    private Mock<IStoreManager> StoreManager { get; }
    private Mock<IExpiringKeySet> KeySet { get; }
    private Mock<IEventPublisher> Publisher { get; }
    private OwnerRootKeyValidationService Service { get; }
    private List<AuditEvent> Published { get; } = [];

    public OwnerRootKeyValidationServiceTests()
    {
        PlatformAccessor = CreateStrictMock<IMaterializedPlatformAccessor>();
        Manager = CreateStrictMock<IOwnerRootKeyManager>();
        StoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        Store = CreateStrictMock<IOwnerRootKeyStore>();
        StoreManager = CreateStrictMock<IStoreManager>();
        KeySet = CreateStrictMock<IExpiringKeySet>();
        Publisher = CreateStrictMock<IEventPublisher>();

        Service = new OwnerRootKeyValidationService(
            PlatformAccessor.Object,
            Manager.Object,
            StoreManagerFactory.Object,
            KeySet.Object,
            Publisher.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { OwnerRootKeys = RootKeyOptions }
            ),
            Time,
            CreateLooseMock<Microsoft.Extensions.Logging.ILogger<OwnerRootKeyValidationService>>().Object
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Service.Dispose();

        base.Dispose(disposing);
    }

    private static PersistedOwnerRootKey Row(SecretOwner owner) =>
        new()
        {
            OwnerType = owner.OwnerType,
            OwnerId = owner.OwnerId,
            KeyWrapperName = "aws-kms",
            KeyReference = "arn:aws:kms:us-west-2:111122223333:key/abcd",
            State = OwnerRootKeyStates.Active,
            CreatedWhen = Now.AddDays(-1),
            StateChangedWhen = Now.AddDays(-1),
        };

    private static OwnerRootKeyInfo Info(SecretOwner owner, string state, string? failure = null) =>
        new(
            owner,
            "aws-kms",
            "arn:aws:kms:us-west-2:111122223333:key/abcd",
            state,
            Now.AddDays(-1),
            Now,
            Now,
            failure is null ? null : Now,
            failure
        );

    private void SetupLease(bool acquired) =>
        KeySet
            .Setup(x =>
                x.TryAddAsync(
                    OwnerRootKeyValidationService.LeaseKey,
                    Now + RootKeyOptions.RetryInterval / 2,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acquired)
            .Verifiable(Times.Once());

    private void SetupDuePage(params PersistedOwnerRootKey[] rows)
    {
        StoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(StoreManager.Object))
            .Verifiable();
        StoreManager
            .Setup(x => x.GetStore<IOwnerRootKeyStore>())
            .Returns(Store.Object)
            .Verifiable();
        StoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        Store
            .Setup(x =>
                x.GetDuePageAsync(
                    Now - RootKeyOptions.ValidationInterval,
                    Now - RootKeyOptions.RetryInterval,
                    It.Is<PageRequest>(page => page.Cursor == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PagedResult<PersistedOwnerRootKey>>(
                    new PagedResult<PersistedOwnerRootKey> { Items = rows, NextCursor = null }
                )
            )
            .Verifiable();
    }

    private void SetupPublisher() =>
        Publisher
            .Setup(x => x.PublishAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEvent, CancellationToken>((e, _) => Published.Add(e))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupValidate(SecretOwner owner, OwnerRootKeyValidation validation) =>
        Manager
            .Setup(x => x.ValidateAsync(owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OwnerRootKeyValidation>(validation))
            .Verifiable();

    private static MasterKeyRewrapResult NoRewrap { get; } = new(0, 0, 0, 0);

    [Fact]
    public async Task Run_WhenTheLeaseIsLost_RunsNothing()
    {
        SetupLease(acquired: false);

        await Service.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Run_ValidatesEachDueOwnerInItsOwnTryBlock()
    {
        SetupLease(acquired: true);
        SetupDuePage(Row(TenantOwner), Row(OtherOwner));
        Manager
            .Setup(x => x.ValidateAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<OwnerRootKeyValidation>(
                    Task.FromException<OwnerRootKeyValidation>(
                        new InvalidOperationException("boom")
                    )
                )
            )
            .Verifiable();
        SetupValidate(
            OtherOwner,
            new OwnerRootKeyValidation(
                Info(OtherOwner, OwnerRootKeyStates.Active),
                null,
                null,
                NoRewrap
            )
        );

        await Service.RunAsync(CancellationToken.None);

        Assert.Empty(Published);
    }

    [Fact]
    public async Task Run_AnUnavailableChange_PublishesRootKeyUnavailableWithAFailureOutcome()
    {
        SetupLease(acquired: true);
        SetupDuePage(Row(TenantOwner));
        SetupPublisher();
        SetupValidate(
            TenantOwner,
            new OwnerRootKeyValidation(
                Info(TenantOwner, OwnerRootKeyStates.Unavailable, "AccessDenied: revoked"),
                RootKeyChanges.Unavailable,
                null,
                NoRewrap
            )
        );

        await Service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.IsType<RootKeyAuditEvent>(Assert.Single(Published));
        Assert.Equal("root_key.unavailable", auditEvent.EventName);
        Assert.Equal(AuditOutcome.Failure, auditEvent.Outcome);
        Assert.Equal(RootKeyChangeReasons.ValidationFailed, auditEvent.Reason);
        Assert.Equal("AccessDenied: revoked", auditEvent.Failure);
        Assert.Equal("acme", auditEvent.TenantId);
        Assert.Equal(AuditEventSources.OpenId, auditEvent.EventSource);
        Assert.Equal(AuditActor.System, auditEvent.Actor);
    }

    [Fact]
    public async Task Run_ARestoredChange_PublishesRootKeyRestored()
    {
        SetupLease(acquired: true);
        SetupDuePage(Row(TenantOwner));
        SetupPublisher();
        SetupValidate(
            TenantOwner,
            new OwnerRootKeyValidation(
                Info(TenantOwner, OwnerRootKeyStates.Active),
                RootKeyChanges.Restored,
                null,
                NoRewrap
            )
        );

        await Service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.IsType<RootKeyAuditEvent>(Assert.Single(Published));
        Assert.Equal("root_key.restored", auditEvent.EventName);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(RootKeyChangeReasons.ValidationSucceeded, auditEvent.Reason);
        Assert.Null(auditEvent.Failure);
    }

    [Fact]
    public async Task Run_ARotation_PublishesMasterKeyRotatedWithTheRootKeyChangedReason()
    {
        SetupLease(acquired: true);
        SetupDuePage(Row(TenantOwner));
        SetupPublisher();
        var retired = new MasterKeyInfo(
            Guid.NewGuid(),
            TenantOwner,
            1,
            Now.AddDays(-30),
            Now,
            "local",
            "root",
            null
        );
        var active = new MasterKeyInfo(
            Guid.NewGuid(),
            TenantOwner,
            2,
            Now,
            null,
            "aws-kms",
            "root",
            null
        )
        {
            RootKeyReference = "arn:aws:kms:us-west-2:111122223333:key/abcd",
            RootKeyAccessIdentity = "arn:aws:iam::111122223333:role/tenant-key-access",
        };
        SetupValidate(
            TenantOwner,
            new OwnerRootKeyValidation(
                Info(TenantOwner, OwnerRootKeyStates.Active),
                null,
                new MasterKeyRotation(TenantOwner, active, retired),
                NoRewrap
            )
        );

        await Service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.IsType<MasterKeyAuditEvent>(Assert.Single(Published));
        Assert.Equal("master_key.rotated", auditEvent.EventName);
        Assert.Equal(MasterKeyChangeReasons.RootKeyChanged, auditEvent.Reason);
        Assert.Equal(active, auditEvent.Key);
        Assert.Equal(retired, auditEvent.RetiredKey);
    }

    [Fact]
    public async Task Run_WithNoChange_PublishesNothing()
    {
        SetupLease(acquired: true);
        SetupDuePage(Row(TenantOwner));
        SetupValidate(
            TenantOwner,
            new OwnerRootKeyValidation(
                Info(TenantOwner, OwnerRootKeyStates.Active),
                null,
                null,
                NoRewrap
            )
        );

        await Service.RunAsync(CancellationToken.None);

        Assert.Empty(Published);
    }

    [Fact]
    public async Task Run_WhenThePublisherFails_StillHandlesTheNextOwner()
    {
        SetupLease(acquired: true);
        SetupDuePage(Row(TenantOwner), Row(OtherOwner));
        Publisher
            .Setup(x => x.PublishAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(Task.FromException(new InvalidOperationException("down"))))
            .Verifiable(Times.Once());
        SetupValidate(
            TenantOwner,
            new OwnerRootKeyValidation(
                Info(TenantOwner, OwnerRootKeyStates.Unavailable, "failure"),
                RootKeyChanges.Unavailable,
                null,
                NoRewrap
            )
        );
        SetupValidate(
            OtherOwner,
            new OwnerRootKeyValidation(
                Info(OtherOwner, OwnerRootKeyStates.Active),
                null,
                null,
                NoRewrap
            )
        );

        await Service.RunAsync(CancellationToken.None);
    }
}
