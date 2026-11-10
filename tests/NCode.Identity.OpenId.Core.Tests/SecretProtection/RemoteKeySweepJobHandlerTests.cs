// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
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
    private static DateTimeOffset Now { get; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static DispatchContext Context { get; } =
        new()
        {
            DispatchId = Guid.NewGuid(),
            Attempt = 1,
            MaxAttempts = 5,
        };

    private const string ProviderName = "aws";
    private const string Deployment = "prod";
    private const string ControlPlaneTenantId = "control-plane";

    private RemoteKeyOptions RemoteKeyOptions { get; } = new() { Deployment = Deployment };
    private FakeTimeProvider Time { get; } = new(Now);
    private Mock<IRemoteKeyProviderCatalog> Catalog { get; }
    private Mock<IRemoteKeyDescriptorCatalog> DescriptorCatalog { get; }
    private Mock<IRemoteKeyProvider> Provider { get; }
    private Mock<IStoreManagerFactory> StoreManagerFactory { get; }
    private Mock<IStoreManager> StoreManager { get; }
    private Mock<IRemoteKeyReferenceStore> Store { get; }
    private Mock<IRemoteSecretDestroyer> Destroyer { get; }
    private Mock<IEventPublisher> Publisher { get; }
    private Mock<ITenantResolver> TenantResolver { get; }
    private List<RemoteKeyAuditEvent> Published { get; } = [];

    public RemoteKeySweepJobHandlerTests()
    {
        Catalog = CreateStrictMock<IRemoteKeyProviderCatalog>();
        DescriptorCatalog = CreateStrictMock<IRemoteKeyDescriptorCatalog>();
        Provider = CreateStrictMock<IRemoteKeyProvider>();
        StoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        StoreManager = CreateStrictMock<IStoreManager>();
        Store = CreateStrictMock<IRemoteKeyReferenceStore>();
        Destroyer = CreateStrictMock<IRemoteSecretDestroyer>();
        Publisher = CreateStrictMock<IEventPublisher>();
        TenantResolver = CreateStrictMock<ITenantResolver>();
    }

    private RemoteKeySweepJobHandler CreateHandler() =>
        new(
            Catalog.Object,
            DescriptorCatalog.Object,
            StoreManagerFactory.Object,
            Destroyer.Object,
            Publisher.Object,
            TenantResolver.Object,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RemoteKeys = RemoteKeyOptions }
            ),
            Time,
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
        IReadOnlyList<RemoteKeyListing> listings,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await Task.Yield();
        foreach (var listing in listings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return listing;
        }
    }

    // The registered provider and its descriptor, for the tests that reach the check of whether it creates keys.
    private void SetupRegisteredProvider(bool createsKeys)
    {
        IRemoteKeyProvider? provider = Provider.Object;
        Catalog.Setup(x => x.TryGetProvider(ProviderName, out provider)).Returns(true).Verifiable();
        DescriptorCatalog
            .Setup(x => x.Providers)
            .Returns([
                new RemoteKeyProviderDescriptor { Name = ProviderName, CreatesKeys = createsKeys },
            ])
            .Verifiable();
    }

    // The registered provider, which creates keys and lists them, for the tests that sweep.
    private void SetupProvider(params RemoteKeyListing[] listings)
    {
        SetupRegisteredProvider(createsKeys: true);
        Provider
            .Setup(x =>
                x.ListKeysAsync(
                    It.Is<RemoteKeyListRequest>(request => request.Deployment == Deployment),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (RemoteKeyListRequest _, CancellationToken cancellationToken) =>
                    ToAsyncEnumerable(listings, cancellationToken)
            )
            .Verifiable();
    }

    // The store manager that a page read opens, for the tests that read the references.
    private void SetupStore()
    {
        StoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(StoreManager.Object))
            .Verifiable();
        StoreManager
            .Setup(x => x.GetStore<IRemoteKeyReferenceStore>())
            .Returns(Store.Object)
            .Verifiable();
        StoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupReferencePage(string? cursor, string? nextCursor, params string[] keyIds) =>
        Store
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
        Destroyer
            .Setup(x => x.DestroyKeyAsync(new RemoteKeyReference(ProviderName, keyId)))
            .Returns(new ValueTask<bool>(destroyed))
            .Verifiable();

    private void SetupPublisher() =>
        Publisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<RemoteKeyAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback<RemoteKeyAuditEvent, CancellationToken>((e, _) => Published.Add(e))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupControlPlaneTenant() =>
        TenantResolver
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

        var auditEvent = Assert.Single(Published);
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

        Assert.Empty(Published);
    }

    [Fact]
    public async Task HandleAsync_KeyYoungerThanTheGrace_KeepsItAndReadsNoStore()
    {
        SetupProvider(Listing("young", TimeSpan.FromHours(1)));

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(Published);
    }

    [Fact]
    public async Task HandleAsync_DestroyerReturnsFalse_PublishesNothing()
    {
        SetupProvider(Listing("orphan", TimeSpan.FromDays(2)));
        SetupStore();
        SetupReferencePage(null, null);
        SetupDestroy("orphan", false);

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(Published);
    }

    [Fact]
    public async Task HandleAsync_UnregisteredProvider_IsSkipped()
    {
        IRemoteKeyProvider? provider = null;
        Catalog
            .Setup(x => x.TryGetProvider(ProviderName, out provider))
            .Returns(false)
            .Verifiable();

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(Published);
    }

    [Fact]
    public async Task HandleAsync_WhenTheProviderCreatesNoKeys_ListsNothing()
    {
        SetupRegisteredProvider(createsKeys: false);

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(Published);
    }

    [Fact]
    public async Task HandleAsync_NoDeployment_IsSkipped()
    {
        RemoteKeyOptions.Deployment = null;

        await CreateHandler().HandleAsync(Job(), Context, CancellationToken.None);

        Assert.Empty(Published);
    }

    [Fact]
    public async Task HandleAsync_StoreFailure_ThrowsAndDestroysNothing()
    {
        SetupProvider(Listing("orphan", TimeSpan.FromDays(2)));
        SetupStore();
        Store
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

        Assert.Empty(Published);
    }

    #endregion
}
