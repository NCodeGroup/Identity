// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class MasterKeyMaintenanceServiceTests : BaseTests
{
    private const string ServerId = "default";
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("acme");
    private static readonly SecretOwner OtherOwner = SecretOwner.ForTenant("globex");
    private static readonly SecretOwner ServerOwner = SecretOwner.ForServer(ServerId);

    private readonly MasterKeyOptions _keyOptions = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<IMaterializedServerAccessor> _serverAccessor;
    private readonly Mock<IMasterKeyManager> _manager;
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Mock<IMasterKeyStore> _store;
    private readonly Mock<IStoreManager> _storeManager;
    private readonly Mock<IExpiringKeySet> _keySet;
    private readonly Mock<IEventPublisher> _publisher;
    private readonly MasterKeyMaintenanceService _service;
    private readonly List<MasterKeyAuditEvent> _published = [];

    public MasterKeyMaintenanceServiceTests()
    {
        _serverAccessor = CreateStrictMock<IMaterializedServerAccessor>();
        _manager = CreateStrictMock<IMasterKeyManager>();
        _storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        _store = CreateStrictMock<IMasterKeyStore>();
        _storeManager = CreateStrictMock<IStoreManager>();
        _keySet = CreateStrictMock<IExpiringKeySet>();
        _publisher = CreateStrictMock<IEventPublisher>();

        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IStoreManager>(_storeManager.Object));
        _storeManager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(_store.Object);
        _storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _publisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<MasterKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<MasterKeyAuditEvent, CancellationToken>((e, _) => _published.Add(e))
            .Returns(ValueTask.CompletedTask);

        _service = new MasterKeyMaintenanceService(
            _serverAccessor.Object,
            _manager.Object,
            _storeManagerFactory.Object,
            _keySet.Object,
            _publisher.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { MasterKeys = _keyOptions }
            ),
            _time,
            CreateLooseMock<ILogger<MasterKeyMaintenanceService>>().Object
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _service.Dispose();

        base.Dispose(disposing);
    }

    private static MasterKeyInfo Info(SecretOwner owner, int version, bool retired) =>
        new(
            Guid.NewGuid(),
            owner,
            version,
            Now.AddDays(-400),
            retired ? Now.AddDays(-40) : null,
            "local",
            "root",
            null
        );

    private static PersistedMasterKey Row(SecretOwner owner, int version, bool retired) =>
        new()
        {
            KeyId = Guid.NewGuid(),
            OwnerType = owner.OwnerType,
            OwnerId = owner.OwnerId,
            Version = version,
            CreatedWhen = Now.AddDays(-400),
            RetiredWhen = retired ? Now.AddDays(-40) : null,
            RootKeyProvider = "local",
            RootKeyId = "root",
            RootWrappedKey = "wrapped",
            KeyCheck = "check",
        };

    private void SetupLease(bool acquired) =>
        _keySet
            .Setup(x =>
                x.TryAddAsync(
                    $"{MasterKeyMaintenanceService.LeasePurpose}:{ServerId}",
                    Now + _keyOptions.MaintenanceInterval / 2,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acquired);

    private void SetupActivePage(params PersistedMasterKey[] rows) =>
        _store
            .Setup(x =>
                x.GetActivePageAsync(
                    Now - _keyOptions.RotationPeriod,
                    It.IsAny<PageRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PagedResult<PersistedMasterKey>>(
                    new PagedResult<PersistedMasterKey> { Items = rows, NextCursor = null }
                )
            );

    private void SetupRetired(params PersistedMasterKey[] rows) =>
        _store
            .Setup(x => x.ListRetiredAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>(rows));

    #region RunAsync Tests

    [Fact]
    public async Task RunAsync_RotatesTheOverdueOwnersAndAuditsEachWithTheSystemActor()
    {
        SetupLease(true);
        SetupActivePage(Row(TenantOwner, 1, false), Row(ServerOwner, 3, false));
        SetupRetired();
        var tenantRotation = new MasterKeyRotation(
            TenantOwner,
            Info(TenantOwner, 2, false),
            Info(TenantOwner, 1, true)
        );
        var serverRotation = new MasterKeyRotation(
            ServerOwner,
            Info(ServerOwner, 4, false),
            Info(ServerOwner, 3, true)
        );
        _manager
            .Setup(x => x.RotateAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(tenantRotation));
        _manager
            .Setup(x => x.RotateAsync(ServerOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(serverRotation));

        await _service.RunAsync(ServerId, CancellationToken.None);

        Assert.Equal(2, _published.Count);
        var tenantEvent = _published[0];
        Assert.Equal("master_key.rotated", tenantEvent.EventName);
        Assert.Equal(MasterKeyChangeReasons.RotationPeriodElapsed, tenantEvent.Reason);
        Assert.Equal(AuditActor.System, tenantEvent.Actor);
        Assert.Equal(AuditEventSources.OpenId, tenantEvent.EventSource);
        Assert.Equal("acme", tenantEvent.TenantId);
        Assert.Equal(tenantRotation.ActiveKey.KeyId, tenantEvent.KeyId);
        Assert.Equal(tenantRotation.RetiredKey?.KeyId, tenantEvent.RetiredKeyId);
        Assert.Null(_published[1].TenantId);
    }

    [Fact]
    public async Task RunAsync_WhenTheLeaseIsTaken_DoesNothing()
    {
        SetupLease(false);

        await _service.RunAsync(ServerId, CancellationToken.None);

        Assert.Empty(_published);
    }

    [Fact]
    public async Task RunAsync_RewrapsAndDestroysTheRetiredOwnersAndAuditsEachDestroyedKey()
    {
        SetupLease(true);
        SetupActivePage();
        // Two retired rows of one owner and one of another: each owner is handled once.
        SetupRetired(
            Row(TenantOwner, 1, true),
            Row(TenantOwner, 2, true),
            Row(OtherOwner, 1, true)
        );
        var destroyed = Info(TenantOwner, 1, true);
        _manager
            .Setup(x => x.RewrapAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<ProtectedValueRewrapResult>(new ProtectedValueRewrapResult(4, 0))
            );
        _manager
            .Setup(x => x.DestroyRetiredAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<MasterKeyInfo>>([destroyed]));
        _manager
            .Setup(x => x.RewrapAsync(OtherOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ProtectedValueRewrapResult>(ProtectedValueRewrapResult.None));
        _manager
            .Setup(x => x.DestroyRetiredAsync(OtherOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<MasterKeyInfo>>([]));

        await _service.RunAsync(ServerId, CancellationToken.None);

        var auditEvent = Assert.Single(_published);
        Assert.Equal("master_key.destroyed", auditEvent.EventName);
        Assert.Equal(MasterKeyChangeReasons.Unreferenced, auditEvent.Reason);
        Assert.Equal(destroyed.KeyId, auditEvent.KeyId);
        Assert.Null(auditEvent.RetiredKeyId);
    }

    [Fact]
    public async Task RunAsync_OneFailingOwner_DoesNotStopTheOthers()
    {
        SetupLease(true);
        SetupActivePage();
        SetupRetired(Row(TenantOwner, 1, true), Row(OtherOwner, 1, true));
        _manager
            .Setup(x => x.RewrapAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("boom"));
        _manager
            .Setup(x => x.RewrapAsync(OtherOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<ProtectedValueRewrapResult>(new ProtectedValueRewrapResult(1, 0))
            );
        _manager
            .Setup(x => x.DestroyRetiredAsync(OtherOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<MasterKeyInfo>>([]));

        await _service.RunAsync(ServerId, CancellationToken.None);
    }

    [Fact]
    public async Task RunAsync_ALostRotationRace_IsLeftToTheWinner()
    {
        SetupLease(true);
        SetupActivePage(Row(TenantOwner, 1, false));
        SetupRetired();
        _manager
            .Setup(x => x.RotateAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException());

        await _service.RunAsync(ServerId, CancellationToken.None);

        Assert.Empty(_published);
    }

    #endregion
}
