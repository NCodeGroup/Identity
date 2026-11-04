// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.Protected;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
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

    private Mock<IMaterializedPlatformAccessor> MockMaterializedPlatformAccessor { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private Mock<IOpenIdContextFactory> MockOpenIdContextFactory { get; }
    private Mock<ISessionService> MockSessionService { get; }
    private Mock<IExpiringKeySet> MockExpiringKeySet { get; }
    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private FakeLogger<EndedSessionTerminationService> Logger { get; } = new();
    private ServiceProvider RootServiceProvider { get; }
    private EndedSessionTerminationService Service { get; }

    public EndedSessionTerminationServiceTests()
    {
        MockMaterializedPlatformAccessor = CreateStrictMock<IMaterializedPlatformAccessor>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockGrantStore = CreateStrictMock<IGrantStore>();
        MockOpenIdContextFactory = CreateStrictMock<IOpenIdContextFactory>();
        MockSessionService = CreateStrictMock<ISessionService>();
        MockExpiringKeySet = CreateStrictMock<IExpiringKeySet>();
        RootServiceProvider = new ServiceCollection().BuildServiceProvider();
        Service = new EndedSessionTerminationService(
            MockMaterializedPlatformAccessor.Object,
            RootServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            MockStoreManagerFactory.Object,
            MockOpenIdContextFactory.Object,
            MockSessionService.Object,
            MockExpiringKeySet.Object,
            Options.Create(new EndedSessionTerminationOptions()),
            TimeProvider,
            Logger
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

    private void SetupLease(bool acquired, CancellationToken cancellationToken = default)
    {
        MockExpiringKeySet
            .Setup(x => x.TryAddAsync(LeaseKey, LeaseExpiresWhen, cancellationToken))
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
        SetupPage(cursor, nextCursor, grants, CancellationToken.None);

    private void SetupPage(
        string? cursor,
        string? nextCursor,
        PersistedGrant[] grants,
        CancellationToken cancellationToken
    ) =>
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
                    cancellationToken
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
    public async Task TerminateEndedSessionsAsync_WhenASessionIsMalformed_LogsItAndTerminatesTheTenantsOtherSessions()
    {
        SetupLease(acquired: true);
        SetupStores();
        SetupPage(cursor: null, nextCursor: null, CreateGrant("t1", "s1"), CreateGrant("t1", "s2"));
        var mockContext = CreateContext();
        SetupTenantContext("t1", mockContext, ("s2", true));
        var exception = new JsonException("malformed");
        MockSessionService
            .Setup(x => x.TerminateIfEndedAsync(mockContext.Object, "s1", CancellationToken.None))
            .ThrowsAsync(exception)
            .Verifiable(Times.Once);

        await Service.TerminateEndedSessionsAsync(CancellationToken.None);

        var records = Logger.Collector.GetSnapshot();
        var failure = Assert.Single(records, x => x.Level == LogLevel.Warning);
        Assert.Equal(EventIds.EndedSessionTerminationFailedForSession, failure.Id.Id);
        Assert.Same(exception, failure.Exception);
        Assert.Contains(failure.StructuredState ?? [], x => x is { Key: "SessionId", Value: "s1" });
        Assert.Contains(failure.StructuredState ?? [], x => x is { Key: "TenantId", Value: "t1" });
        var terminated = Assert.Single(records, x => x.Level == LogLevel.Information);
        Assert.Equal(EventIds.EndedSessionsTerminated, terminated.Id.Id);
    }

    [Fact]
    public async Task TerminateEndedSessionsAsync_WhenCancelledWhileTerminatingASession_Throws()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetupLease(acquired: true, cancellationToken);
        SetupStores();
        SetupPage(cursor: null, nextCursor: null, [CreateGrant("t1", "s1")], cancellationToken);
        var mockContext = CreateContext();
        MockOpenIdContextFactory
            .Setup(x =>
                x.CreateForBackgroundAsync(It.IsAny<IServiceProvider>(), "t1", cancellationToken)
            )
            .ReturnsAsync(mockContext.Object)
            .Verifiable(Times.Once);
        MockSessionService
            .Setup(x => x.TerminateIfEndedAsync(mockContext.Object, "s1", cancellationToken))
            .Callback(cancellationTokenSource.Cancel)
            .ThrowsAsync(new OperationCanceledException(cancellationToken))
            .Verifiable(Times.Once);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Service.TerminateEndedSessionsAsync(cancellationToken)
        );

        Assert.Empty(Logger.Collector.GetSnapshot());
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
