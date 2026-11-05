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
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("acme");
    private static readonly SecretOwner OtherOwner = SecretOwner.ForTenant("globex");

    private readonly OwnerRootKeyOptions _rootKeyOptions = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<IMaterializedPlatformAccessor> _platformAccessor;
    private readonly Mock<IOwnerRootKeyManager> _manager;
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Mock<IOwnerRootKeyStore> _store;
    private readonly Mock<IStoreManager> _storeManager;
    private readonly Mock<IExpiringKeySet> _keySet;
    private readonly Mock<IEventPublisher> _publisher;
    private readonly OwnerRootKeyValidationService _service;
    private readonly List<AuditEvent> _published = [];

    public OwnerRootKeyValidationServiceTests()
    {
        _platformAccessor = CreateStrictMock<IMaterializedPlatformAccessor>();
        _manager = CreateStrictMock<IOwnerRootKeyManager>();
        _storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        _store = CreateStrictMock<IOwnerRootKeyStore>();
        _storeManager = CreateStrictMock<IStoreManager>();
        _keySet = CreateStrictMock<IExpiringKeySet>();
        _publisher = CreateStrictMock<IEventPublisher>();

        _service = new OwnerRootKeyValidationService(
            _platformAccessor.Object,
            _manager.Object,
            _storeManagerFactory.Object,
            _keySet.Object,
            _publisher.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { OwnerRootKeys = _rootKeyOptions }
            ),
            _time,
            CreateLooseMock<Microsoft.Extensions.Logging.ILogger<OwnerRootKeyValidationService>>().Object
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _service.Dispose();

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
            null,
            state,
            Now.AddDays(-1),
            Now,
            Now,
            failure is null ? null : Now,
            failure
        );

    private void SetupLease(bool acquired) =>
        _keySet
            .Setup(x =>
                x.TryAddAsync(
                    OwnerRootKeyValidationService.LeaseKey,
                    Now + _rootKeyOptions.RetryInterval / 2,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acquired)
            .Verifiable(Times.Once());

    private void SetupDuePage(params PersistedOwnerRootKey[] rows)
    {
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(_storeManager.Object))
            .Verifiable();
        _storeManager
            .Setup(x => x.GetStore<IOwnerRootKeyStore>())
            .Returns(_store.Object)
            .Verifiable();
        _storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        _store
            .Setup(x =>
                x.GetDuePageAsync(
                    Now - _rootKeyOptions.ValidationInterval,
                    Now - _rootKeyOptions.RetryInterval,
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
        _publisher
            .Setup(x => x.PublishAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEvent, CancellationToken>((e, _) => _published.Add(e))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupValidate(SecretOwner owner, OwnerRootKeyValidation validation) =>
        _manager
            .Setup(x => x.ValidateAsync(owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OwnerRootKeyValidation>(validation))
            .Verifiable();

    private static readonly MasterKeyRewrapResult NoRewrap = new(0, 0, 0, 0);

    [Fact]
    public async Task Run_WhenTheLeaseIsLost_RunsNothing()
    {
        SetupLease(acquired: false);

        await _service.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Run_ValidatesEachDueOwnerInItsOwnTryBlock()
    {
        SetupLease(acquired: true);
        SetupDuePage(Row(TenantOwner), Row(OtherOwner));
        _manager
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

        await _service.RunAsync(CancellationToken.None);

        Assert.Empty(_published);
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

        await _service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.IsType<RootKeyAuditEvent>(Assert.Single(_published));
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

        await _service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.IsType<RootKeyAuditEvent>(Assert.Single(_published));
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

        await _service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.IsType<MasterKeyAuditEvent>(Assert.Single(_published));
        Assert.Equal("master_key.rotated", auditEvent.EventName);
        Assert.Equal(MasterKeyChangeReasons.RootKeyChanged, auditEvent.Reason);
        Assert.Equal(active.KeyId, auditEvent.KeyId);
        Assert.Equal(retired.KeyId, auditEvent.RetiredKeyId);
        Assert.Equal(active.RootKeyReference, auditEvent.RootKeyReference);
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

        await _service.RunAsync(CancellationToken.None);

        Assert.Empty(_published);
    }

    [Fact]
    public async Task Run_WhenThePublisherFails_StillHandlesTheNextOwner()
    {
        SetupLease(acquired: true);
        SetupDuePage(Row(TenantOwner), Row(OtherOwner));
        _publisher
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

        await _service.RunAsync(CancellationToken.None);
    }
}
