// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Protected;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public sealed class EndedSessionTerminationServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 30, TimeSpan.Zero);

    private static readonly string LeaseKey =
        $"{EndedSessionTerminationService.LeasePurpose}:{Now.UtcTicks / TimeSpan.FromMinutes(1).Ticks}";

    private static readonly DateTimeOffset LeaseExpiresWhen = new(
        2026,
        10,
        23,
        12,
        1,
        0,
        TimeSpan.Zero
    );

    private Mock<IMaterializedServerAccessor> MockMaterializedServerAccessor { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private Mock<IOpenIdContextFactory> MockOpenIdContextFactory { get; }
    private Mock<ISessionService> MockSessionService { get; }
    private Mock<IExpiringKeySet> MockExpiringKeySet { get; }
    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private ServiceProvider RootServiceProvider { get; }
    private EndedSessionTerminationService Service { get; }

    public EndedSessionTerminationServiceTests()
    {
        MockMaterializedServerAccessor = CreateStrictMock<IMaterializedServerAccessor>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockGrantStore = CreateStrictMock<IGrantStore>();
        MockOpenIdContextFactory = CreateStrictMock<IOpenIdContextFactory>();
        MockSessionService = CreateStrictMock<ISessionService>();
        MockExpiringKeySet = CreateStrictMock<IExpiringKeySet>();
        RootServiceProvider = new ServiceCollection().BuildServiceProvider();
        Service = new EndedSessionTerminationService(
            MockMaterializedServerAccessor.Object,
            RootServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            MockStoreManagerFactory.Object,
            MockOpenIdContextFactory.Object,
            MockSessionService.Object,
            MockExpiringKeySet.Object,
            Options.Create(new EndedSessionTerminationOptions()),
            TimeProvider,
            NullLogger<EndedSessionTerminationService>.Instance
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Service.Dispose();
            RootServiceProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    private static PersistedGrant CreateGrant(string tenantId, string sessionId) =>
        new()
        {
            GrantType = OpenIdConstants.PersistedGrantTypes.Session,
            HashedKey = $"hashed-{sessionId}",
            GrantId = $"grant-{sessionId}",
            ConcurrencyToken = string.Empty,
            TenantId = tenantId,
            ClientId = null,
            SubjectId = "subject-1",
            SessionId = sessionId,
            RefreshTokenLineageId = null,
            CreatedWhen = Now.AddHours(-2),
            ExpiresWhen = Now.AddMinutes(-1),
            RevokedWhen = null,
            ConsumedWhen = null,
            ProtectedPayload = "protected-payload",
        };

    private Mock<OpenIdContext> CreateContext()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext
            .Protected()
            .Setup<ValueTask>("DisposeAsyncCore")
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        return mockContext;
    }

    private void SetupLease(bool acquired)
    {
        MockExpiringKeySet
            .Setup(x => x.TryAddAsync(LeaseKey, LeaseExpiresWhen, CancellationToken.None))
            .ReturnsAsync(acquired)
            .Verifiable();
    }

    private void SetupStores()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IGrantStore>())
            .Returns(MockGrantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupPage(string? cursor, string? nextCursor, params PersistedGrant[] grants) =>
        MockGrantStore
            .Setup(x =>
                x.GetUnrevokedExpiredPageAsync(
                    OpenIdConstants.PersistedGrantTypes.Session,
                    Now,
                    new PageRequest
                    {
                        Cursor = cursor,
                        Limit = EndedSessionTerminationService.PageSize,
                    },
                    CancellationToken.None
                )
            )
            .ReturnsAsync(
                new PagedResult<PersistedGrant> { Items = grants, NextCursor = nextCursor }
            )
            .Verifiable();

    private void SetupTenantContext(
        string tenantId,
        Mock<OpenIdContext> mockContext,
        params (string SessionId, bool Terminated)[] sessions
    )
    {
        MockOpenIdContextFactory
            .Setup(x =>
                x.CreateForBackgroundAsync(
                    It.IsAny<IServiceProvider>(),
                    tenantId,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(mockContext.Object)
            .Verifiable(Times.Once);

        foreach (var (sessionId, terminated) in sessions)
        {
            MockSessionService
                .Setup(x =>
                    x.TerminateIfEndedAsync(mockContext.Object, sessionId, CancellationToken.None)
                )
                .ReturnsAsync(terminated)
                .Verifiable(Times.Once);
        }
    }

    #region TerminateEndedSessionsAsync Tests

    [Fact]
    public async Task TerminateEndedSessionsAsync_WhenAnotherReplicaHoldsTheLease_ReadsNoSessions()
    {
        SetupLease(acquired: false);

        await Service.TerminateEndedSessionsAsync(CancellationToken.None);

        MockStoreManagerFactory.Verify(
            x => x.CreateAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task TerminateEndedSessionsAsync_TerminatesEachEndedSessionInItsTenantsContext()
    {
        SetupLease(acquired: true);
        SetupStores();
        SetupPage(
            cursor: null,
            nextCursor: null,
            CreateGrant("t1", "s1"),
            CreateGrant("t1", "s2"),
            CreateGrant("t2", "s3")
        );
        SetupTenantContext("t1", CreateContext(), ("s1", true), ("s2", false));
        SetupTenantContext("t2", CreateContext(), ("s3", true));

        await Service.TerminateEndedSessionsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TerminateEndedSessionsAsync_WhenATenantsContextCannotBeCreated_TerminatesTheOtherTenantsSessions()
    {
        SetupLease(acquired: true);
        SetupStores();
        SetupPage(cursor: null, nextCursor: null, CreateGrant("t1", "s1"), CreateGrant("t2", "s3"));
        MockOpenIdContextFactory
            .Setup(x =>
                x.CreateForBackgroundAsync(
                    It.IsAny<IServiceProvider>(),
                    "t1",
                    CancellationToken.None
                )
            )
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Verifiable(Times.Once);
        SetupTenantContext("t2", CreateContext(), ("s3", true));

        await Service.TerminateEndedSessionsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TerminateEndedSessionsAsync_WhenAPageHasANextCursor_ReadsTheNextPage()
    {
        SetupLease(acquired: true);
        SetupStores();
        SetupPage(cursor: null, nextCursor: "next", CreateGrant("t1", "s1"));
        SetupPage(cursor: "next", nextCursor: null, CreateGrant("t2", "s3"));
        SetupTenantContext("t1", CreateContext(), ("s1", true));
        SetupTenantContext("t2", CreateContext(), ("s3", true));

        await Service.TerminateEndedSessionsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TerminateEndedSessionsAsync_WhenTheStoreFails_DoesNotThrow()
    {
        SetupLease(acquired: true);
        SetupStores();
        MockGrantStore
            .Setup(x =>
                x.GetUnrevokedExpiredPageAsync(
                    OpenIdConstants.PersistedGrantTypes.Session,
                    Now,
                    new PageRequest
                    {
                        Cursor = null,
                        Limit = EndedSessionTerminationService.PageSize,
                    },
                    CancellationToken.None
                )
            )
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Verifiable(Times.Once);

        await Service.TerminateEndedSessionsAsync(CancellationToken.None);
    }

    #endregion
}
