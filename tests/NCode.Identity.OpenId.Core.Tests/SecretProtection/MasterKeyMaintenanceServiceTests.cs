// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.OpenId.Tenants;
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
    private static DateTimeOffset Now { get; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static SecretOwner TenantOwner { get; } = SecretOwner.ForTenant("acme");
    private static SecretOwner OtherOwner { get; } = SecretOwner.ForTenant("globex");
    private static SecretOwner PlatformOwner { get; } = SecretOwner.Platform;
    private const string ControlPlaneTenantId = "control-plane";

    private readonly MasterKeyOptions _keyOptions = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<IMaterializedPlatformAccessor> _platformAccessor;
    private readonly Mock<IMasterKeyManager> _manager;
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Mock<IMasterKeyStore> _store;
    private readonly Mock<IStoreManager> _storeManager;
    private readonly Mock<IExpiringKeySet> _keySet;
    private readonly Mock<IEventPublisher> _publisher;
    private readonly Mock<ITenantResolver> _tenantResolver;
    private readonly MasterKeyMaintenanceService _service;
    private readonly Mock<OpenIdPlatform> _platform;
    private readonly List<MasterKeyAuditEvent> _published = [];

    public MasterKeyMaintenanceServiceTests()
    {
        _platformAccessor = CreateStrictMock<IMaterializedPlatformAccessor>();
        _manager = CreateStrictMock<IMasterKeyManager>();
        _storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        _store = CreateStrictMock<IMasterKeyStore>();
        _storeManager = CreateStrictMock<IStoreManager>();
        _keySet = CreateStrictMock<IExpiringKeySet>();
        _publisher = CreateStrictMock<IEventPublisher>();
        _tenantResolver = CreateStrictMock<ITenantResolver>();
        _platform = CreateStrictMock<OpenIdPlatform>();

        _service = CreateService(CreateLooseMock<ILogger<MasterKeyMaintenanceService>>().Object);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _service.Dispose();

        base.Dispose(disposing);
    }

    private MasterKeyMaintenanceService CreateService(
        ILogger<MasterKeyMaintenanceService> logger
    ) =>
        new(
            _platformAccessor.Object,
            _manager.Object,
            _storeManagerFactory.Object,
            _keySet.Object,
            _publisher.Object,
            _tenantResolver.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { MasterKeys = _keyOptions }
            ),
            _time,
            logger
        );

    // A strict logger that records the level and the event id of each entry; only the tests that check a log use it.
    private ILogger<MasterKeyMaintenanceService> CreateCapturingLogger(
        List<(LogLevel Level, int EventId)> entries
    )
    {
        var logger = CreateStrictMock<ILogger<MasterKeyMaintenanceService>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true).Verifiable();
#pragma warning disable CA1873 // The arguments are matchers of a mock setup, not log arguments.
        logger
            .Setup(x =>
                x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Callback(
                new InvocationAction(invocation =>
                {
                    if (invocation.Arguments is [LogLevel level, EventId eventId, ..])
                        entries.Add((level, eventId.Id));
                })
            )
            .Verifiable();
#pragma warning restore CA1873
        return logger.Object;
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

    // The store manager that a page read opens, for the tests that read a page.
    private void SetupStore()
    {
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(_storeManager.Object))
            .Verifiable();
        _storeManager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(_store.Object).Verifiable();
        _storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    // The publisher that records the audit events, for the tests that publish.
    private void SetupPublisher() =>
        _publisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<MasterKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<MasterKeyAuditEvent, CancellationToken>((e, _) => _published.Add(e))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    // The control-plane tenant that a platform event is recorded in, for the tests that publish one.
    private void SetupControlPlaneTenant() =>
        _tenantResolver
            .Setup(x => x.GetControlPlaneTenantId())
            .Returns(ControlPlaneTenantId)
            .Verifiable();

    private void SetupLease(bool acquired) =>
        _keySet
            .Setup(x =>
                x.TryAddAsync(
                    MasterKeyMaintenanceService.LeaseKey,
                    Now + _keyOptions.MaintenanceInterval / 2,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acquired)
            .Verifiable(Times.Once());

    private void SetupActivePage(
        string? cursor,
        string? nextCursor,
        params PersistedMasterKey[] rows
    ) =>
        _store
            .Setup(x =>
                x.GetActivePageAsync(
                    Now - _keyOptions.RotationPeriod,
                    It.Is<PageRequest>(page => page.Cursor == cursor),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PagedResult<PersistedMasterKey>>(
                    new PagedResult<PersistedMasterKey> { Items = rows, NextCursor = nextCursor }
                )
            )
            .Verifiable();

    private void SetupActivePage(params PersistedMasterKey[] rows) =>
        SetupActivePage(null, null, rows);

    private void SetupRetired(string? cursor, string? nextCursor, params SecretOwner[] owners) =>
        _store
            .Setup(x =>
                x.GetRetiredOwnerPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == cursor),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PagedResult<SecretOwner>>(
                    new PagedResult<SecretOwner> { Items = owners, NextCursor = nextCursor }
                )
            )
            .Verifiable();

    private void SetupRetired(params SecretOwner[] owners) => SetupRetired(null, null, owners);

    private void SetupRotate(SecretOwner owner, MasterKeyRotation rotation) =>
        _manager
            .Setup(x => x.RotateAsync(owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(rotation))
            .Verifiable(Times.Once());

    private void SetupRewrap(SecretOwner owner, ProtectedValueRewrapResult result) =>
        _manager
            .Setup(x => x.RewrapAsync(owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ProtectedValueRewrapResult>(result))
            .Verifiable(Times.Once());

    private void SetupDestroy(SecretOwner owner, params MasterKeyInfo[] destroyed) =>
        _manager
            .Setup(x => x.DestroyRetiredAsync(owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<MasterKeyInfo>>(destroyed))
            .Verifiable(Times.Once());

    private static MasterKeyRotation Rotation(SecretOwner owner) =>
        new(owner, Info(owner, 2, false), Info(owner, 1, true));

    #region RunAsync Tests

    [Fact]
    public async Task RunAsync_RotatesTheOverdueOwnersAndAuditsEachWithTheSystemActor()
    {
        SetupStore();
        SetupPublisher();
        SetupLease(true);
        SetupActivePage(Row(TenantOwner, 1, false), Row(PlatformOwner, 3, false));
        SetupRetired();
        var tenantRotation = new MasterKeyRotation(
            TenantOwner,
            Info(TenantOwner, 2, false),
            Info(TenantOwner, 1, true)
        );
        var platformRotation = new MasterKeyRotation(
            PlatformOwner,
            Info(PlatformOwner, 4, false),
            Info(PlatformOwner, 3, true)
        );
        SetupRotate(TenantOwner, tenantRotation);
        SetupRotate(PlatformOwner, platformRotation);
        SetupControlPlaneTenant();

        await _service.RunAsync(CancellationToken.None);

        Assert.Equal(2, _published.Count);
        var tenantEvent = _published[0];
        Assert.Equal("master_key.rotated", tenantEvent.EventName);
        Assert.Equal(MasterKeyChangeReasons.RotationPeriodElapsed, tenantEvent.Reason);
        Assert.Equal(AuditActor.System, tenantEvent.Actor);
        Assert.Equal(AuditEventSources.OpenId, tenantEvent.EventSource);
        Assert.Equal("acme", tenantEvent.TenantId);
        Assert.Equal(tenantRotation.ActiveKey, tenantEvent.Key);
        Assert.Equal(tenantRotation.RetiredKey, tenantEvent.RetiredKey);
        Assert.Equal(ControlPlaneTenantId, _published[1].TenantId);
    }

    [Fact]
    public async Task RunAsync_ARotatedKeyWithARootKeyReference_AuditsTheReference()
    {
        SetupStore();
        SetupPublisher();
        SetupLease(true);
        SetupActivePage(Row(TenantOwner, 1, false));
        SetupRetired();
        const string rootKeyReference = "owner-root-key-reference";
        var rotation = new MasterKeyRotation(
            TenantOwner,
            Info(TenantOwner, 2, false) with
            {
                RootKeyReference = rootKeyReference,
            },
            Info(TenantOwner, 1, true)
        );
        SetupRotate(TenantOwner, rotation);

        await _service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.Single(_published);
        Assert.Equal(rootKeyReference, auditEvent.Key.RootKeyReference);
        Assert.Equal("local", auditEvent.Key.RootKeyProvider);
    }

    [Fact]
    public async Task RunAsync_ARotatedKeyWithAnAccessIdentity_AuditsTheIdentity()
    {
        SetupStore();
        SetupPublisher();
        SetupLease(true);
        SetupActivePage(Row(TenantOwner, 1, false));
        SetupRetired();
        const string accessIdentity = "arn:aws:iam::111122223333:role/tenant-key-access";
        var rotation = new MasterKeyRotation(
            TenantOwner,
            Info(TenantOwner, 2, false) with
            {
                RootKeyAccessIdentity = accessIdentity,
            },
            Info(TenantOwner, 1, true)
        );
        SetupRotate(TenantOwner, rotation);

        await _service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.Single(_published);
        Assert.Equal(accessIdentity, auditEvent.Key.RootKeyAccessIdentity);
    }

    [Fact]
    public async Task RunAsync_WhenTheLeaseIsTaken_DoesNothing()
    {
        SetupLease(false);

        await _service.RunAsync(CancellationToken.None);

        Assert.Empty(_published);
        Assert.Empty(_storeManagerFactory.Invocations);
        Assert.Empty(_manager.Invocations);
    }

    [Fact]
    public async Task RunAsync_RewrapsAndDestroysTheRetiredOwnersAndAuditsEachDestroyedKey()
    {
        SetupStore();
        SetupPublisher();
        SetupLease(true);
        SetupActivePage();
        SetupRetired(TenantOwner, OtherOwner);
        var destroyed = Info(TenantOwner, 1, true);
        SetupRewrap(TenantOwner, new ProtectedValueRewrapResult(4, 0));
        SetupDestroy(TenantOwner, destroyed);
        SetupRewrap(OtherOwner, ProtectedValueRewrapResult.None);
        SetupDestroy(OtherOwner);

        await _service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.Single(_published);
        Assert.Equal("master_key.destroyed", auditEvent.EventName);
        Assert.Equal(MasterKeyChangeReasons.Unreferenced, auditEvent.Reason);
        Assert.Equal(destroyed, auditEvent.Key);
        Assert.Null(auditEvent.RetiredKey);
    }

    [Fact]
    public async Task RunAsync_APlatformKeyIsDestroyed_AuditsItInTheControlPlaneTenant()
    {
        SetupStore();
        SetupPublisher();
        SetupLease(true);
        SetupActivePage();
        SetupRetired(PlatformOwner);
        SetupRewrap(PlatformOwner, ProtectedValueRewrapResult.None);
        SetupDestroy(PlatformOwner, Info(PlatformOwner, 1, true));
        SetupControlPlaneTenant();

        await _service.RunAsync(CancellationToken.None);

        var auditEvent = Assert.Single(_published);
        Assert.Equal("master_key.destroyed", auditEvent.EventName);
        Assert.Equal(ControlPlaneTenantId, auditEvent.TenantId);
        Assert.Equal(SecretOwnerTypes.Platform, auditEvent.Key.Owner.OwnerType);
    }

    [Fact]
    public async Task RunAsync_OneFailingOwner_DoesNotStopTheOthers()
    {
        SetupStore();
        SetupLease(true);
        SetupActivePage();
        SetupRetired(TenantOwner, OtherOwner);
        _manager
            .Setup(x => x.RewrapAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("boom"))
            .Verifiable(Times.Once());
        SetupRewrap(OtherOwner, new ProtectedValueRewrapResult(1, 0));
        SetupDestroy(OtherOwner);
        var entries = new List<(LogLevel Level, int EventId)>();
        using var service = CreateService(CreateCapturingLogger(entries));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal(
            [(LogLevel.Warning, EventIds.MasterKeyMaintenanceOwnerFailed)],
            entries.Where(entry => entry.Level >= LogLevel.Warning)
        );
    }

    [Fact]
    public async Task RunAsync_ALostRotationRace_IsLeftToTheWinner()
    {
        SetupStore();
        SetupLease(true);
        SetupActivePage(Row(TenantOwner, 1, false));
        SetupRetired();
        _manager
            .Setup(x => x.RotateAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException())
            .Verifiable(Times.Once());
        var entries = new List<(LogLevel Level, int EventId)>();
        using var service = CreateService(CreateCapturingLogger(entries));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal([(LogLevel.Debug, EventIds.MasterKeyRotationLostRace)], entries);
        Assert.Empty(_published);
    }

    [Fact]
    public async Task RunAsync_ARotationThatFails_IsLoggedAndTheNextOwnerRotates()
    {
        SetupStore();
        SetupPublisher();
        SetupLease(true);
        SetupActivePage(Row(TenantOwner, 1, false), Row(OtherOwner, 1, false));
        SetupRetired();
        _manager
            .Setup(x => x.RotateAsync(TenantOwner, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("root key down"))
            .Verifiable(Times.Once());
        SetupRotate(OtherOwner, Rotation(OtherOwner));
        var entries = new List<(LogLevel Level, int EventId)>();
        using var service = CreateService(CreateCapturingLogger(entries));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal([(LogLevel.Warning, EventIds.MasterKeyMaintenanceOwnerFailed)], entries);
        Assert.Equal("globex", Assert.Single(_published).TenantId);
    }

    [Fact]
    public async Task RunAsync_TheAuditPublishFails_LogsItAndKeepsTheRotation()
    {
        SetupStore();
        SetupLease(true);
        SetupActivePage(Row(TenantOwner, 1, false));
        SetupRetired();
        SetupRotate(TenantOwner, Rotation(TenantOwner));
        _publisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<MasterKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Throws(new InvalidOperationException("the bus is down"))
            .Verifiable(Times.Once());
        var entries = new List<(LogLevel Level, int EventId)>();
        using var service = CreateService(CreateCapturingLogger(entries));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal([(LogLevel.Warning, EventIds.MasterKeyAuditPublishFailed)], entries);
    }

    [Fact]
    public async Task RunAsync_TheStoreFails_LogsThePassFailure()
    {
        SetupStore();
        SetupLease(true);
        _store
            .Setup(x =>
                x.GetActivePageAsync(
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<PageRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new InvalidOperationException("the store is down"))
            .Verifiable(Times.Once());
        var entries = new List<(LogLevel Level, int EventId)>();
        using var service = CreateService(CreateCapturingLogger(entries));

        await service.RunAsync(CancellationToken.None);

        Assert.Equal([(LogLevel.Warning, EventIds.MasterKeyMaintenancePassFailed)], entries);
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _keySet
            .Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new OperationCanceledException(cancelled.Token))
            .Verifiable(Times.Once());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _service.RunAsync(cancelled.Token)
        );
    }

    [Fact]
    public async Task RunAsync_ActiveKeysOnTwoPages_RotatesEveryOverdueOwner()
    {
        SetupStore();
        SetupPublisher();
        SetupLease(true);
        SetupActivePage(null, "next", Row(TenantOwner, 1, false));
        SetupActivePage("next", null, Row(OtherOwner, 1, false));
        SetupRetired();
        SetupRotate(TenantOwner, Rotation(TenantOwner));
        SetupRotate(OtherOwner, Rotation(OtherOwner));

        await _service.RunAsync(CancellationToken.None);

        Assert.Equal(2, _published.Count);
    }

    [Fact]
    public async Task RunAsync_RetiredOwnersOnSeveralPages_HandlesEachOwnerOnce()
    {
        SetupStore();
        SetupLease(true);
        SetupActivePage();
        SetupRetired(null, "next", TenantOwner);
        SetupRetired("next", null, TenantOwner, OtherOwner);
        SetupRewrap(TenantOwner, ProtectedValueRewrapResult.None);
        SetupDestroy(TenantOwner);
        SetupRewrap(OtherOwner, ProtectedValueRewrapResult.None);
        SetupDestroy(OtherOwner);

        await _service.RunAsync(CancellationToken.None);

        Assert.Empty(_published);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_EveryMaintenanceInterval_RunsAPassUntilStopped()
    {
        _platformAccessor
            .Setup(x => x.GetPlatformAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OpenIdPlatform>(_platform.Object))
            .Verifiable(Times.Once());

        // The lease answers false, so a pass takes the lease and does nothing else.
        using var leased = new SemaphoreSlim(0);
        _keySet
            .Setup(x =>
                x.TryAddAsync(
                    MasterKeyMaintenanceService.LeaseKey,
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => leased.Release())
            .ReturnsAsync(false)
            .Verifiable(Times.AtLeast(2));

        await _service.StartAsync(CancellationToken.None);

        // The service may create its timer after StartAsync returns, so the clock advances until the second pass.
        Assert.True(await leased.WaitAsync(TimeSpan.FromSeconds(10)));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        var ticked = false;
        while (!ticked && DateTime.UtcNow < deadline)
        {
            _time.Advance(_keyOptions.MaintenanceInterval);
            ticked = await leased.WaitAsync(TimeSpan.FromMilliseconds(50));
        }

        Assert.True(ticked);

        await _service.StopAsync(CancellationToken.None);

        Assert.True(_service.ExecuteTask is { IsCompleted: true, IsFaulted: false });
    }

    #endregion
}
