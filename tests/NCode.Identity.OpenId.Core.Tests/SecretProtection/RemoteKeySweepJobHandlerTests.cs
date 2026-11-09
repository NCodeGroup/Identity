// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class RemoteKeySweepJobHandlerTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DispatchContext Context = new()
    {
        DispatchId = Guid.NewGuid(),
        Attempt = 1,
        MaxAttempts = 5,
    };

    private const string ProviderName = "aws";
    private const string Deployment = "prod";
    private const string ControlPlaneTenantId = "control-plane";

    private readonly RemoteKeyOptions _remoteKeyOptions = new() { Deployment = Deployment };
    private readonly FakeTimeProvider _time = new(Now);
    private readonly Mock<IRemoteKeyProviderCatalog> _catalog;
    private readonly Mock<IRemoteKeyProvider> _provider;
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Mock<IStoreManager> _storeManager;
    private readonly Mock<IRemoteKeyReferenceStore> _store;
    private readonly Mock<IRemoteSecretDestroyer> _destroyer;
    private readonly Mock<IEventPublisher> _publisher;
    private readonly Mock<ITenantResolver> _tenantResolver;
    private readonly List<RemoteKeyAuditEvent> _published = [];

    public RemoteKeySweepJobHandlerTests()
    {
        _catalog = CreateStrictMock<IRemoteKeyProviderCatalog>();
        _provider = CreateStrictMock<IRemoteKeyProvider>();
        _storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        _storeManager = CreateStrictMock<IStoreManager>();
        _store = CreateStrictMock<IRemoteKeyReferenceStore>();
        _destroyer = CreateStrictMock<IRemoteSecretDestroyer>();
        _publisher = CreateStrictMock<IEventPublisher>();
        _tenantResolver = CreateStrictMock<ITenantResolver>();
    }

    private RemoteKeySweepJobHandler CreateHandler() =>
        new(
            _catalog.Object,
            _storeManagerFactory.Object,
            _destroyer.Object,
            _publisher.Object,
            _tenantResolver.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RemoteKeys = _remoteKeyOptions }
            ),
            _time,
            CreateLooseMock<ILogger<RemoteKeySweepJobHandler>>().Object
        );

    private static RemoteKeySweepJob Job() => new() { Provider = ProviderName };

    private static RemoteKeyListing Listing(string keyId, TimeSpan age) =>
        new()
        {
            KeyId = keyId,
            GroupName = "ncid-group",
            CreatedWhen = Now - age,
            OwnerId = "acme",
            SecretId = "secret-1",
        };

    private static async IAsyncEnumerable<RemoteKeyListing> ToAsyncEnumerable(
        params RemoteKeyListing[] listings
    )
    {
        await Task.Yield();
        foreach (var listing in listings)
        {
            yield return listing;
        }
    }

    // The registered provider, which lists the keys, for the tests that sweep.
    private void SetupProvider(params RemoteKeyListing[] listings)
    {
        IRemoteKeyProvider? provider = _provider.Object;
        _catalog
            .Setup(x => x.TryGetProvider(ProviderName, out provider))
            .Returns(true)
            .Verifiable();
        _provider.Setup(x => x.Name).Returns(ProviderName).Verifiable();
        _provider
            .Setup(x =>
                x.ListKeysAsync(
                    It.Is<RemoteKeyListRequest>(request => request.Deployment == Deployment),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ToAsyncEnumerable(listings))
            .Verifiable();
    }

    // The store manager that a page read opens, for the tests that read the references.
    private void SetupStore()
    {
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(_storeManager.Object))
            .Verifiable();
        _storeManager
            .Setup(x => x.GetStore<IRemoteKeyReferenceStore>())
            .Returns(_store.Object)
            .Verifiable();
        _storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupReferencePage(string? cursor, string? nextCursor, params string[] keyIds) =>
        _store
            .Setup(x =>
                x.GetKeyIdPageAsync(
                    ProviderName,
                    It.Is<PageRequest>(page => page.Cursor == cursor && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PagedResult<string>>(
                    new PagedResult<string> { Items = keyIds, NextCursor = nextCursor }
                )
            )
            .Verifiable();

    private void SetupDestroy(string keyId, bool destroyed) =>
        _destroyer
            .Setup(x => x.DestroyKeyAsync(new RemoteKeyReference(ProviderName, keyId)))
            .Returns(new ValueTask<bool>(destroyed))
            .Verifiable();

    private void SetupPublisher() =>
        _publisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<RemoteKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<RemoteKeyAuditEvent, CancellationToken>((e, _) => _published.Add(e))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupControlPlaneTenant() =>
        _tenantResolver
            .Setup(x => x.GetControlPlaneTenantId())
            .Returns(ControlPlaneTenantId)
            .Verifiable();

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_UnnamedKeyOlderThanTheGrace_DestroysAndAuditsIt()
    {
        var listing = Listing("orphan", TimeSpan.FromDays(2));
        SetupProvider(listing);
        SetupStore();
        SetupReferencePage(null, null);
        SetupDestroy("orphan", true);
        SetupPublisher();
        SetupControlPlaneTenant();

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        var auditEvent = Assert.Single(_published);
        Assert.Equal(ControlPlaneTenantId, auditEvent.TenantId);
        Assert.Equal(RemoteKeyChanges.Destroyed, auditEvent.Change);
        Assert.Equal(RemoteKeyChangeReasons.Orphaned, auditEvent.Reason);
        Assert.Equal(ProviderName, auditEvent.Provider);
        Assert.Equal("orphan", auditEvent.KeyId);
        Assert.Equal("ncid-group", auditEvent.GroupName);
        Assert.Equal("acme", auditEvent.OwnerId);
        Assert.Equal("secret-1", auditEvent.SecretId);
        Assert.Equal(listing.CreatedWhen, auditEvent.KeyCreatedWhen);
        Assert.Equal(AuditActor.System, auditEvent.Actor);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
    }

    [Fact]
    public async Task HandleAsync_NamedKey_KeepsIt()
    {
        SetupProvider(Listing("named", TimeSpan.FromDays(2)));
        SetupStore();
        SetupReferencePage(null, "next", "other");
        SetupReferencePage("next", null, "named");

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(_published);
    }

    [Fact]
    public async Task HandleAsync_KeyYoungerThanTheGrace_KeepsItAndReadsNoStore()
    {
        SetupProvider(Listing("young", TimeSpan.FromHours(1)));

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(_published);
    }

    [Fact]
    public async Task HandleAsync_DestroyerReturnsFalse_PublishesNothing()
    {
        SetupProvider(Listing("orphan", TimeSpan.FromDays(2)));
        SetupStore();
        SetupReferencePage(null, null);
        SetupDestroy("orphan", false);

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(_published);
    }

    [Fact]
    public async Task HandleAsync_UnregisteredProvider_IsSkipped()
    {
        IRemoteKeyProvider? provider = null;
        _catalog
            .Setup(x => x.TryGetProvider(ProviderName, out provider))
            .Returns(false)
            .Verifiable();

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(_published);
    }

    [Fact]
    public async Task HandleAsync_NoDeployment_IsSkipped()
    {
        _remoteKeyOptions.Deployment = null;

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(_published);
    }

    [Fact]
    public async Task HandleAsync_StoreFailure_ThrowsAndDestroysNothing()
    {
        SetupProvider(Listing("orphan", TimeSpan.FromDays(2)));
        SetupStore();
        _store
            .Setup(x =>
                x.GetKeyIdPageAsync(
                    ProviderName,
                    It.IsAny<PageRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None)
        );

        Assert.Empty(_published);
    }

    #endregion
}
