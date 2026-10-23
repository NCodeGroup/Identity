#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultSessionServiceTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string SubjectId = "subject-1";
    private const string SessionId = "session-id";
    private const string ClientId = "client-1";

    private static readonly DateTimeOffset UtcNow = DateTimeOffset.Parse(
        "2026-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<ISessionEvictionPolicy> MockEvictionPolicy { get; }
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private static DefaultPersistedPayloadSerializer PayloadSerializer { get; } = new([]);
    private DefaultSessionService Service { get; }

    public DefaultSessionServiceTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockEvictionPolicy = CreateStrictMock<ISessionEvictionPolicy>();
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        Service = new DefaultSessionService(
            MockTimeProvider.Object,
            MockCryptoService.Object,
            MockPersistedGrantService.Object,
            PayloadSerializer,
            MockStoreManagerFactory.Object,
            MockEvictionPolicy.Object,
            MockAuditEventRecorder.Object,
            NullLogger<DefaultSessionService>.Instance
        );
    }

    #region Scaffolding

    private static PersistedGrantId CreateGrantId() =>
        new()
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.Session,
            GrantKey = SessionId,
        };

    private Mock<OpenIdContext> CreateContext()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        return mockContext;
    }

    private static PersistedGrant<SessionGrant> CreateSession(
        PersistedGrantStatus status,
        IReadOnlyList<string> clientIds
    ) =>
        new()
        {
            Status = status,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = SubjectId,
            SessionId = null,
            Payload = new SessionGrant
            {
                AuthTime = UtcNow,
                AuthenticationMethods = null,
                IdentityProvider = null,
                ClientIds = clientIds,
                LastActivityWhen = UtcNow,
            },
        };

    private async Task<(
        string Result,
        PersistedGrant<SessionGrant> Grant,
        DateTimeOffset CreatedWhen,
        TimeSpan? Lifetime
    )> CreateSessionAsync(
        CreateSessionRequest request,
        Action<Mock<OpenIdContext>>? arrangeContext = null
    )
    {
        var mockContext = CreateContext();
        arrangeContext?.Invoke(mockContext);
        var grantId = CreateGrantId();

        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<string>()))
            .Returns(SessionId)
            .Verifiable();
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.Session, SessionId)
            )
            .Returns(grantId)
            .Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        PersistedGrant<SessionGrant> capturedGrant = default;
        DateTimeOffset capturedCreatedWhen = default;
        TimeSpan? capturedLifetime = null;
        MockPersistedGrantService
            .Setup(x =>
                x.AddAsync(
                    mockContext.Object,
                    grantId,
                    It.IsAny<PersistedGrant<SessionGrant>>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    PersistedGrant<SessionGrant> grant,
                    DateTimeOffset createdWhen,
                    TimeSpan? lifetime,
                    CancellationToken _
                ) =>
                {
                    capturedGrant = grant;
                    capturedCreatedWhen = createdWhen;
                    capturedLifetime = lifetime;
                }
            )
            .Returns(ValueTask.FromResult("grant-public-id"))
            .Verifiable();

        MockAuditEventRecorder
            .Setup(x =>
                x.RecordSessionCreatedAsync(
                    mockContext.Object,
                    SubjectId,
                    SessionId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.CreateAsync(mockContext.Object, request, CancellationToken.None);

        return (result, capturedGrant, capturedCreatedWhen, capturedLifetime);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_Always_PersistsSessionGrantAndReturnsSid()
    {
        var request = new CreateSessionRequest
        {
            SubjectId = SubjectId,
            AuthTime = UtcNow,
            AuthenticationMethods = ["pwd"],
            IdentityProvider = "idp-1",
            AbsoluteLifetime = TimeSpan.FromHours(8),
            IdleTimeout = TimeSpan.FromHours(1),
        };

        var (result, capturedGrant, capturedCreatedWhen, capturedLifetime) =
            await CreateSessionAsync(request);

        Assert.Equal(SessionId, result);
        Assert.Equal(PersistedGrantStatus.Active, capturedGrant.Status);
        Assert.Equal(SubjectId, capturedGrant.SubjectId);
        Assert.Null(capturedGrant.ClientId);
        Assert.Equal(UtcNow, capturedGrant.Payload.AuthTime);
        Assert.Equal(new[] { "pwd" }, capturedGrant.Payload.AuthenticationMethods);
        Assert.Equal("idp-1", capturedGrant.Payload.IdentityProvider);
        Assert.Empty(capturedGrant.Payload.ClientIds);
        Assert.Equal(UtcNow.AddHours(8), capturedGrant.Payload.AbsoluteExpiresWhen);
        Assert.Equal(TimeSpan.FromHours(1), capturedGrant.Payload.IdleTimeout);
        Assert.Equal(UtcNow, capturedGrant.Payload.LastActivityWhen);
        Assert.Equal(UtcNow, capturedCreatedWhen);
        Assert.Equal(TimeSpan.FromHours(1), capturedLifetime);
    }

    [Fact]
    public async Task CreateAsync_WhenTheAbsoluteLifetimeIsShorterThanTheIdleTimeout_ExpiresAtTheAbsoluteEnd()
    {
        var request = new CreateSessionRequest
        {
            SubjectId = SubjectId,
            AuthTime = UtcNow,
            AbsoluteLifetime = TimeSpan.FromHours(1),
            IdleTimeout = TimeSpan.FromDays(3),
        };

        var (_, capturedGrant, _, capturedLifetime) = await CreateSessionAsync(request);

        Assert.Equal(UtcNow.AddHours(1), capturedGrant.Payload.AbsoluteExpiresWhen);
        Assert.Equal(TimeSpan.FromHours(1), capturedLifetime);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(60, 0)]
    public async Task CreateAsync_WhenALifetimeIsNotPositive_Throws(
        int absoluteSeconds,
        int idleSeconds
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var request = new CreateSessionRequest
        {
            SubjectId = SubjectId,
            AuthTime = UtcNow,
            AbsoluteLifetime = TimeSpan.FromSeconds(absoluteSeconds),
            IdleTimeout = TimeSpan.FromSeconds(idleSeconds),
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await Service.CreateAsync(mockContext.Object, request, CancellationToken.None)
        );
    }

    [Fact]
    public async Task CreateAsync_WhenTheSubjectIsAtItsSessionLimit_TerminatesTheEvictedSessionOnce()
    {
        var request = new CreateSessionRequest
        {
            SubjectId = SubjectId,
            AuthTime = UtcNow,
            AbsoluteLifetime = TimeSpan.FromHours(8),
            IdleTimeout = TimeSpan.FromHours(1),
            MaxConcurrentSessions = 1,
        };

        var (result, _, _, _) = await CreateSessionAsync(
            request,
            mockContext =>
            {
                SetupSessionEnumeration(
                    mockContext,
                    CreateEnvelope("old", UtcNow.AddMinutes(-10), clientIds: ["c1"])
                );
                MockEvictionPolicy
                    .Setup(x => x.SelectSessionsToEvict(It.IsAny<IReadOnlyList<SessionInfo>>(), 1))
                    .Returns((IReadOnlyList<SessionInfo> sessions, int _) => sessions)
                    .Verifiable();
                SetupSessionClaims(mockContext, ("old", true));

                var mockMediator = CreateStrictMock<IMediator>();
                mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
                mockMediator
                    .Setup(x =>
                        x.SendAsync(
                            It.Is<OnSessionTerminatedCommand>(c =>
                                c.Termination.Reason
                                    == SessionTerminationReasons.ConcurrentSessionLimit
                                && c.Termination.SessionId == "old"
                            ),
                            It.IsAny<CancellationToken>()
                        )
                    )
                    .Returns(ValueTask.CompletedTask)
                    .Verifiable();
            }
        );

        Assert.Equal(SessionId, result);
    }

    [Fact]
    public async Task CreateAsync_WhenAnotherCallerTerminatedTheEvictedSessionFirst_DoesNotDispatch()
    {
        var request = new CreateSessionRequest
        {
            SubjectId = SubjectId,
            AuthTime = UtcNow,
            AbsoluteLifetime = TimeSpan.FromHours(8),
            IdleTimeout = TimeSpan.FromHours(1),
            MaxConcurrentSessions = 1,
        };

        // No mediator is arranged; a strict mock would throw if the session were dispatched.
        var (result, _, _, _) = await CreateSessionAsync(
            request,
            mockContext =>
            {
                SetupSessionEnumeration(
                    mockContext,
                    CreateEnvelope("old", UtcNow.AddMinutes(-10), clientIds: ["c1"])
                );
                MockEvictionPolicy
                    .Setup(x => x.SelectSessionsToEvict(It.IsAny<IReadOnlyList<SessionInfo>>(), 1))
                    .Returns((IReadOnlyList<SessionInfo> sessions, int _) => sessions)
                    .Verifiable();
                SetupSessionClaims(mockContext, ("old", false));
            }
        );

        Assert.Equal(SessionId, result);
    }

    #endregion

    #region AddClientAsync Tests

    [Fact]
    public async Task AddClientAsync_WhenTheStoredSessionDoesNotListTheClient_AddsIt()
    {
        var mockContext = CreateContext();
        var grantId = CreateGrantId();

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.Session, SessionId)
            )
            .Returns(grantId)
            .Verifiable();

        Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?>? captured = null;
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync<SessionGrant>(
                    mockContext.Object,
                    grantId,
                    It.IsAny<
                        Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?>
                    >(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?> update,
                    CancellationToken _
                ) => captured = update
            )
            .Returns(ValueTask.FromResult(true))
            .Verifiable();

        var result = await Service.AddClientAsync(
            mockContext.Object,
            SessionId,
            ClientId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.NotNull(captured);
        var update = captured(CreateSession(PersistedGrantStatus.Active, []));
        Assert.NotNull(update);
        Assert.Equal(new[] { ClientId }, update.Value.Payload.ClientIds);
    }

    [Fact]
    public async Task AddClientAsync_WhenTheStoredSessionListsTheClient_MakesNoChange()
    {
        var mockContext = CreateContext();
        var grantId = CreateGrantId();

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.Session, SessionId)
            )
            .Returns(grantId)
            .Verifiable();

        Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?>? captured = null;
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync<SessionGrant>(
                    mockContext.Object,
                    grantId,
                    It.IsAny<
                        Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?>
                    >(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?> update,
                    CancellationToken _
                ) => captured = update
            )
            .Returns(ValueTask.FromResult(false))
            .Verifiable();

        var result = await Service.AddClientAsync(
            mockContext.Object,
            SessionId,
            ClientId,
            CancellationToken.None
        );

        Assert.False(result);
        Assert.NotNull(captured);
        var update = captured(CreateSession(PersistedGrantStatus.Active, [ClientId]));
        Assert.Null(update);
    }

    [Fact]
    public async Task AddClientAsync_WhenAConcurrentRequestAddedAnotherClient_KeepsBothClients()
    {
        var mockContext = CreateContext();
        var grantId = CreateGrantId();

        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.Session, SessionId)
            )
            .Returns(grantId)
            .Verifiable();

        Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?>? captured = null;
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync<SessionGrant>(
                    mockContext.Object,
                    grantId,
                    It.IsAny<
                        Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?>
                    >(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?> update,
                    CancellationToken _
                ) => captured = update
            )
            .Returns(ValueTask.FromResult(true))
            .Verifiable();

        var result = await Service.AddClientAsync(
            mockContext.Object,
            SessionId,
            ClientId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.NotNull(captured);
        var update = captured(CreateSession(PersistedGrantStatus.Active, ["client-b"]));
        Assert.NotNull(update);
        Assert.Equal(new[] { "client-b", ClientId }, update.Value.Payload.ClientIds);
    }

    #endregion

    #region ValidateAndTouchAsync / Revoke Tests

    private Mock<OpenIdContext> CreateContextWithMediator(out Mock<IMediator> mockMediator)
    {
        var mockContext = CreateContext();
        var mediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mediator.Object).Verifiable();
        mockMediator = mediator;
        return mockContext;
    }

    private static PersistedGrant<SessionGrant> CreateIdleSession(
        TimeSpan? idleTimeout,
        DateTimeOffset lastActivityWhen
    ) =>
        new()
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = null,
            SubjectId = SubjectId,
            SessionId = SessionId,
            ExpiresWhen = UtcNow.AddDays(30),
            Payload = new SessionGrant
            {
                AuthTime = UtcNow,
                ClientIds = ["client-a"],
                LastActivityWhen = lastActivityWhen,
                IdleTimeout = idleTimeout,
                AbsoluteExpiresWhen = UtcNow.AddDays(30),
            },
        };

    private void SetupTermination(Mock<IMediator> mockMediator, string reason) =>
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == reason && c.Termination.SessionId == SessionId
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private PersistedGrantId SetupGetSession(
        Mock<OpenIdContext> mockContext,
        PersistedGrant<SessionGrant>? session
    )
    {
        var grantId = CreateGrantId();
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.Session, SessionId)
            )
            .Returns(grantId)
            .Verifiable();
        MockPersistedGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<SessionGrant>(
                    mockContext.Object,
                    grantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(session)
            .Verifiable();
        return grantId;
    }

    private void SetupClaim(
        Mock<OpenIdContext> mockContext,
        PersistedGrantId grantId,
        bool claimed
    ) =>
        MockPersistedGrantService
            .Setup(x =>
                x.RevokeSessionOnceAsync(
                    mockContext.Object,
                    grantId,
                    UtcNow,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(claimed)
            .Verifiable();

    private void SetupTouch(
        Mock<OpenIdContext> mockContext,
        PersistedGrantId grantId,
        PersistedGrant<SessionGrant> stored,
        Action<PersistedGrantUpdate<SessionGrant>?> onTouch
    ) =>
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync<SessionGrant>(
                    mockContext.Object,
                    grantId,
                    It.IsAny<
                        Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?>
                    >(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?> update,
                    CancellationToken _
                ) => onTouch(update(stored))
            )
            .ReturnsAsync(true)
            .Verifiable();

    [Fact]
    public async Task ValidateAndTouchAsync_WhenNoIdleTimeout_ReturnsTrueWithoutWrite()
    {
        var mockContext = CreateContext();
        SetupGetSession(mockContext, CreateIdleSession(idleTimeout: null, UtcNow));
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenRevoked_ReturnsFalse()
    {
        var mockContext = CreateContext();
        SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromHours(1), UtcNow) with
            {
                Status = PersistedGrantStatus.Revoked,
            }
        );

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenPastTheAbsoluteEnd_TerminatesWithAbsoluteExpiry()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-30));
        var grantId = SetupGetSession(
            mockContext,
            session with
            {
                Status = PersistedGrantStatus.Expired,
                ExpiresWhen = UtcNow.AddMinutes(-1),
                Payload = session.Payload with { AbsoluteExpiresWhen = UtcNow.AddMinutes(-1) },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: true);
        SetupTermination(mockMediator, SessionTerminationReasons.AbsoluteExpiry);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenTheGrantExpiredBeforeItsAbsoluteEnd_TerminatesWithIdleTimeout()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-30));
        var grantId = SetupGetSession(
            mockContext,
            session with
            {
                Status = PersistedGrantStatus.Expired,
                ExpiresWhen = UtcNow.AddMinutes(-1),
                Payload = session.Payload with { AbsoluteExpiresWhen = UtcNow.AddDays(29) },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: true);
        SetupTermination(mockMediator, SessionTerminationReasons.IdleTimeout);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenStoredWithoutAnEnd_TerminatesWithAbsoluteExpiry()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-30));
        var grantId = SetupGetSession(
            mockContext,
            session with
            {
                ExpiresWhen = null,
                Payload = session.Payload with { AbsoluteExpiresWhen = null },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: true);
        SetupTermination(mockMediator, SessionTerminationReasons.AbsoluteExpiry);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenMissing_ReturnsFalse()
    {
        var mockContext = CreateContext();
        SetupGetSession(mockContext, session: null);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenIdleExpired_DispatchesTerminationAndReturnsFalse()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        var grantId = SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromMinutes(30), UtcNow.AddHours(-1))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: true);
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReasons.IdleTimeout
                        && c.Termination.SessionId == SessionId
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenAnotherCallerTerminatedTheEndedSessionFirst_ReturnsFalseWithoutDispatching()
    {
        var mockContext = CreateContext();
        var grantId = SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromMinutes(30), UtcNow.AddHours(-1))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: false);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenActiveBeyondGranularity_SlidesWindow()
    {
        var mockContext = CreateContext();
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-5));
        var grantId = SetupGetSession(mockContext, session);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        PersistedGrantUpdate<SessionGrant>? update = null;
        SetupTouch(mockContext, grantId, session, touch => update = touch);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.NotNull(update);
        Assert.Equal(UtcNow, update.Value.Payload.LastActivityWhen);
        Assert.Equal(UtcNow.AddDays(30), update.Value.Payload.AbsoluteExpiresWhen);
        Assert.Equal(UtcNow.AddHours(1), update.Value.ExpiresWhen);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenTheAbsoluteEndIsNearerThanTheIdleWindow_SlidesTheExpiryToTheAbsoluteEnd()
    {
        var mockContext = CreateContext();
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-5));
        var stored = session with
        {
            ExpiresWhen = UtcNow.AddMinutes(10),
            Payload = session.Payload with { AbsoluteExpiresWhen = UtcNow.AddMinutes(10) },
        };
        var grantId = SetupGetSession(mockContext, stored);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        PersistedGrantUpdate<SessionGrant>? update = null;
        SetupTouch(mockContext, grantId, stored, touch => update = touch);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.NotNull(update);
        Assert.Equal(UtcNow.AddMinutes(10), update.Value.ExpiresWhen);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenStoredWithoutAnAbsoluteEnd_StampsTheGrantsExpiryAsItsAbsoluteEnd()
    {
        var mockContext = CreateContext();
        var session = CreateIdleSession(idleTimeout: null, UtcNow.AddMinutes(-5));
        var stored = session with
        {
            ExpiresWhen = UtcNow.AddDays(1),
            Payload = session.Payload with { AbsoluteExpiresWhen = null },
        };
        var grantId = SetupGetSession(mockContext, stored);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        PersistedGrantUpdate<SessionGrant>? update = null;
        SetupTouch(mockContext, grantId, stored, touch => update = touch);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.NotNull(update);
        Assert.Equal(UtcNow.AddDays(1), update.Value.Payload.AbsoluteExpiresWhen);
        Assert.Equal(UtcNow.AddDays(1), update.Value.ExpiresWhen);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenAConcurrentRequestAddedAClient_TouchesTheSessionAsStored()
    {
        var mockContext = CreateContext();
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-5));
        var stored = session with
        {
            Payload = session.Payload with { ClientIds = ["client-a", "client-b"] },
        };
        var grantId = SetupGetSession(mockContext, session);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        PersistedGrantUpdate<SessionGrant>? update = null;
        SetupTouch(mockContext, grantId, stored, touch => update = touch);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.NotNull(update);
        Assert.Equal(new[] { "client-a", "client-b" }, update.Value.Payload.ClientIds);
        Assert.Equal(UtcNow, update.Value.Payload.LastActivityWhen);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenAnotherRequestTouchedTheSessionFirst_MakesNoChange()
    {
        var mockContext = CreateContext();
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-5));
        var stored = session with { Payload = session.Payload with { LastActivityWhen = UtcNow } };
        var grantId = SetupGetSession(mockContext, session);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        var applied = false;
        PersistedGrantUpdate<SessionGrant>? update = null;
        SetupTouch(
            mockContext,
            grantId,
            stored,
            touch =>
            {
                applied = true;
                update = touch;
            }
        );

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.True(applied);
        Assert.Null(update);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenEveryTouchLosesARace_ReturnsTrue()
    {
        var mockContext = CreateContext();
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-5));
        var grantId = SetupGetSession(mockContext, session);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync<SessionGrant>(
                    mockContext.Object,
                    grantId,
                    It.IsAny<
                        Func<PersistedGrant<SessionGrant>, PersistedGrantUpdate<SessionGrant>?>
                    >(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Verifiable();

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenWithinGranularity_DoesNotWrite()
    {
        var mockContext = CreateContext();
        SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddSeconds(-10))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        // No UpdatePayloadAsync setup exists; a strict mock would throw if the window were persisted.
        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task RevokeAsync_WhenThisCallClaimsTheSession_DispatchesItsTerminationAndReturnsTrue()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        var grantId = SetupGetSession(mockContext, CreateIdleSession(idleTimeout: null, UtcNow));
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: true);
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReasons.AdministrativeRevocation
                        && c.Termination.SessionId == SessionId
                        && c.Termination.SubjectId == SubjectId
                        && c.Termination.ParticipatingClientIds.Contains("client-a")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.RevokeAsync(
            mockContext.Object,
            SessionId,
            SessionTerminationReasons.AdministrativeRevocation,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task RevokeAsync_WhenAnotherCallerTerminatedTheSessionFirst_ReturnsFalseWithoutDispatching()
    {
        var mockContext = CreateContext();
        var grantId = SetupGetSession(mockContext, CreateIdleSession(idleTimeout: null, UtcNow));
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: false);

        var result = await Service.RevokeAsync(
            mockContext.Object,
            SessionId,
            SessionTerminationReasons.AdministrativeRevocation,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task RevokeAsync_WhenTheSessionIsMissing_ReturnsFalse()
    {
        var mockContext = CreateContext();
        SetupGetSession(mockContext, null);

        var result = await Service.RevokeAsync(
            mockContext.Object,
            SessionId,
            SessionTerminationReasons.AdministrativeRevocation,
            CancellationToken.None
        );

        Assert.False(result);
    }

    #endregion

    #region TerminateIfEndedAsync Tests

    [Fact]
    public async Task TerminateIfEndedAsync_WhenTheSessionWasIdleTooLong_TerminatesItAndReturnsTrue()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        var grantId = SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromMinutes(30), UtcNow.AddHours(-1))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: true);
        SetupTermination(mockMediator, SessionTerminationReasons.IdleTimeout);

        var result = await Service.TerminateIfEndedAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task TerminateIfEndedAsync_WhenStoredWithoutAnEnd_TerminatesItWithAbsoluteExpiry()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-30));
        var grantId = SetupGetSession(
            mockContext,
            session with
            {
                ExpiresWhen = null,
                Payload = session.Payload with { AbsoluteExpiresWhen = null },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: true);
        SetupTermination(mockMediator, SessionTerminationReasons.AbsoluteExpiry);

        var result = await Service.TerminateIfEndedAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task TerminateIfEndedAsync_WhenTheSessionHasNotEnded_ReturnsFalse()
    {
        var mockContext = CreateContext();
        SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-5))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        // No RevokeSessionOnceAsync setup exists; a strict mock would throw if the session were claimed.
        var result = await Service.TerminateIfEndedAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task TerminateIfEndedAsync_WhenTheSessionIsRevoked_ReturnsFalse()
    {
        var mockContext = CreateContext();
        SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromMinutes(30), UtcNow.AddHours(-1)) with
            {
                Status = PersistedGrantStatus.Revoked,
            }
        );

        var result = await Service.TerminateIfEndedAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task TerminateIfEndedAsync_WhenAnotherCallerTerminatedItFirst_ReturnsFalse()
    {
        var mockContext = CreateContext();
        var grantId = SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromMinutes(30), UtcNow.AddHours(-1))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: false);

        var result = await Service.TerminateIfEndedAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task TerminateIfEndedAsync_WhenTheDispatchFailsAfterTheClaim_Throws()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        var grantId = SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromMinutes(30), UtcNow.AddHours(-1))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupClaim(mockContext, grantId, claimed: true);
        mockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<OnSessionTerminatedCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.FromException(new InvalidOperationException("boom")))
            .Verifiable();

        // The claim has already revoked the grants of the session, so the failure leaves no token of it active.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Service.TerminateIfEndedAsync(
                mockContext.Object,
                SessionId,
                CancellationToken.None
            )
        );
    }

    #endregion

    #region GetSessionsAsync / RevokeAllAsync Tests

    private static readonly JsonSerializerOptions JsonOptions = new();

    private Mock<IGrantStore> SetupSessionEnumeration(
        Mock<OpenIdContext> mockContext,
        params PersistedGrant[] envelopes
    )
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        mockEnvironment.SetupGet(x => x.JsonSerializerOptions).Returns(JsonOptions).Verifiable();
        mockContext.SetupGet(x => x.Environment).Returns(mockEnvironment.Object).Verifiable();

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockGrantStore = CreateStrictMock<IGrantStore>();
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStoreManager.Object)
            .Verifiable();
        mockStoreManager
            .Setup(x => x.GetStore<IGrantStore>())
            .Returns(mockGrantStore.Object)
            .Verifiable();
        mockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        mockGrantStore
            .Setup(x =>
                x.GetPageAsync(
                    It.IsAny<GrantFilter>(),
                    It.IsAny<PageRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new PagedResult<PersistedGrant> { Items = envelopes, NextCursor = null })
            .Verifiable();
        return mockGrantStore;
    }

    private void SetupSessionClaims(
        Mock<OpenIdContext> mockContext,
        params (string SessionId, bool Claimed)[] claims
    )
    {
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(
                    TenantId,
                    OpenIdConstants.PersistedGrantTypes.Session,
                    It.IsAny<string>()
                )
            )
            .Returns(
                (string tenantId, string grantType, string grantKey) =>
                    new PersistedGrantId
                    {
                        TenantId = tenantId,
                        GrantType = grantType,
                        GrantKey = grantKey,
                    }
            )
            .Verifiable();

        foreach (var (sessionId, claimed) in claims)
        {
            MockPersistedGrantService
                .Setup(x =>
                    x.RevokeSessionOnceAsync(
                        mockContext.Object,
                        It.Is<PersistedGrantId>(id => id.GrantKey == sessionId),
                        UtcNow,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(claimed)
                .Verifiable();
        }
    }

    private void SetupSubjectRevocation() =>
        MockPersistedGrantService
            .Setup(x =>
                x.RevokeWhereAsync(
                    It.IsAny<OpenIdContext>(),
                    It.Is<GrantFilter>(f => f.SubjectId == SubjectId && f.SessionId == null),
                    UtcNow,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(0L)
            .Verifiable();

    private static PersistedGrant CreateEnvelope(
        string sessionId,
        DateTimeOffset createdWhen,
        DateTimeOffset? revokedWhen = null,
        TimeSpan? idleTimeout = null,
        DateTimeOffset? lastActivityWhen = null,
        IReadOnlyList<string>? clientIds = null,
        DateTimeOffset? absoluteExpiresWhen = null
    ) =>
        new()
        {
            GrantType = OpenIdConstants.PersistedGrantTypes.Session,
            HashedKey = "hk-" + sessionId,
            GrantId = "gid-" + sessionId,
            ConcurrencyToken = "ct",
            TenantId = TenantId,
            ClientId = null,
            SubjectId = SubjectId,
            SessionId = sessionId,
            RefreshTokenLineageId = null,
            CreatedWhen = createdWhen,
            ExpiresWhen = absoluteExpiresWhen ?? createdWhen.AddDays(30),
            RevokedWhen = revokedWhen,
            ConsumedWhen = null,
            PayloadJson = PayloadSerializer.Serialize(
                new SessionGrant
                {
                    AuthTime = createdWhen,
                    ClientIds = clientIds ?? [],
                    LastActivityWhen = lastActivityWhen ?? createdWhen,
                    IdleTimeout = idleTimeout,
                    AbsoluteExpiresWhen = absoluteExpiresWhen ?? createdWhen.AddDays(30),
                },
                JsonOptions
            ),
        };

    [Fact]
    public async Task GetSessionsAsync_ReturnsActiveNewestFirst_SkippingRevoked()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("older", UtcNow.AddMinutes(-20)),
            CreateEnvelope("revoked", UtcNow.AddMinutes(-10), revokedWhen: UtcNow),
            CreateEnvelope("newer", UtcNow.AddMinutes(-5))
        );

        var sessions = await Service.GetSessionsAsync(
            mockContext.Object,
            SubjectId,
            CancellationToken.None
        );

        Assert.Equal(["newer", "older"], sessions.Select(session => session.SessionId));
    }

    [Fact]
    public async Task GetSessionsAsync_SkipsIdleExpiredSessions()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope(
                "idle",
                UtcNow.AddHours(-2),
                idleTimeout: TimeSpan.FromMinutes(30),
                lastActivityWhen: UtcNow.AddHours(-1)
            ),
            CreateEnvelope("live", UtcNow.AddMinutes(-1))
        );

        var sessions = await Service.GetSessionsAsync(
            mockContext.Object,
            SubjectId,
            CancellationToken.None
        );

        Assert.Equal(["live"], sessions.Select(session => session.SessionId));
    }

    [Fact]
    public async Task GetSessionsAsync_SkipsSessionsPastTheirAbsoluteEnd()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope(
                "ended",
                UtcNow.AddHours(-2),
                absoluteExpiresWhen: UtcNow.AddMinutes(-1)
            ),
            CreateEnvelope("live", UtcNow.AddMinutes(-1))
        );

        var sessions = await Service.GetSessionsAsync(
            mockContext.Object,
            SubjectId,
            CancellationToken.None
        );

        Assert.Equal(["live"], sessions.Select(session => session.SessionId));
    }

    [Fact]
    public async Task GetSessionsAsync_SkipsSessionsStoredWithoutAnEnd()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        var withoutEnd = CreateEnvelope("without-end", UtcNow.AddMinutes(-5));
        withoutEnd.ExpiresWhen = null;
        withoutEnd.PayloadJson = PayloadSerializer.Serialize(
            new SessionGrant
            {
                AuthTime = UtcNow.AddMinutes(-5),
                ClientIds = [],
                LastActivityWhen = UtcNow.AddMinutes(-5),
            },
            JsonOptions
        );
        SetupSessionEnumeration(
            mockContext,
            withoutEnd,
            CreateEnvelope("live", UtcNow.AddMinutes(-1))
        );

        var sessions = await Service.GetSessionsAsync(
            mockContext.Object,
            SubjectId,
            CancellationToken.None
        );

        Assert.Equal(["live"], sessions.Select(session => session.SessionId));
    }

    [Fact]
    public async Task GetSessionsAsync_ReportsWhenTheSessionEndsUnlessItIsUsedAgain()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope(
                "idle-window",
                UtcNow.AddMinutes(-20),
                idleTimeout: TimeSpan.FromMinutes(30),
                lastActivityWhen: UtcNow.AddMinutes(-10)
            )
        );

        var sessions = await Service.GetSessionsAsync(
            mockContext.Object,
            SubjectId,
            CancellationToken.None
        );

        var session = Assert.Single(sessions);
        Assert.Equal(UtcNow.AddMinutes(20), session.ExpiresWhen);
    }

    [Fact]
    public async Task RevokeAllAsync_DispatchesGlobalSignOutWithUnionedParticipants()
    {
        var mockContext = CreateContext();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("s1", UtcNow.AddMinutes(-10), clientIds: ["c1", "c2"]),
            CreateEnvelope("s2", UtcNow.AddMinutes(-5), clientIds: ["c2", "c3"])
        );
        SetupSessionClaims(mockContext, ("s1", true), ("s2", true));
        SetupSubjectRevocation();

        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReasons.GlobalSignOut
                        && c.Termination.SessionId == null
                        && c.Termination.SubjectId == SubjectId
                        && c.Termination.ParticipatingClientIds.Contains("c1")
                        && c.Termination.ParticipatingClientIds.Contains("c2")
                        && c.Termination.ParticipatingClientIds.Contains("c3")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.RevokeAllAsync(
            mockContext.Object,
            SubjectId,
            SessionTerminationReasons.GlobalSignOut,
            CancellationToken.None
        );

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task RevokeAllAsync_WhenAnotherCallerTerminatedASession_NotifiesOnlyTheRelyingPartiesOfTheSessionsItClaimed()
    {
        var mockContext = CreateContext();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("s1", UtcNow.AddMinutes(-10), clientIds: ["c1", "c2"]),
            CreateEnvelope("s2", UtcNow.AddMinutes(-5), clientIds: ["c2", "c3"])
        );
        SetupSessionClaims(mockContext, ("s1", true), ("s2", false));
        SetupSubjectRevocation();

        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReasons.GlobalSignOut
                        && c.Termination.SessionId == null
                        && c.Termination.SubjectId == SubjectId
                        && c.Termination.ParticipatingClientIds.Contains("c1")
                        && c.Termination.ParticipatingClientIds.Contains("c2")
                        && !c.Termination.ParticipatingClientIds.Contains("c3")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.RevokeAllAsync(
            mockContext.Object,
            SubjectId,
            SessionTerminationReasons.GlobalSignOut,
            CancellationToken.None
        );

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task RevokeAllAsync_WhenTheDispatchFails_HasRevokedTheSubjectsGrants()
    {
        var mockContext = CreateContext();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("s1", UtcNow.AddMinutes(-10), clientIds: ["c1"])
        );
        SetupSessionClaims(mockContext, ("s1", true));

        // The subject's grants are revoked before the dispatch, so the failure leaves none of them active.
        SetupSubjectRevocation();
        mockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<OnSessionTerminatedCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.FromException(new InvalidOperationException("dispatch")))
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Service.RevokeAllAsync(
                mockContext.Object,
                SubjectId,
                SessionTerminationReasons.GlobalSignOut,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RevokeAllAsync_WhenAnotherCallerTerminatedEverySession_StillRevokesTheSubjectsGrantsAndDispatches()
    {
        var mockContext = CreateContext();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("s1", UtcNow.AddMinutes(-10), clientIds: ["c1"])
        );
        SetupSessionClaims(mockContext, ("s1", false));
        SetupSubjectRevocation();

        // The subject-wide termination is dispatched even though this call claimed no session, so the grants the
        // subject holds outside a listed session are still revoked and every subject-wide termination is observed.
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReasons.GlobalSignOut
                        && c.Termination.SessionId == null
                        && c.Termination.SubjectId == SubjectId
                        && c.Termination.ParticipatingClientIds.Count == 0
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.RevokeAllAsync(
            mockContext.Object,
            SubjectId,
            SessionTerminationReasons.GlobalSignOut,
            CancellationToken.None
        );

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RevokeAllAsync_WhenTheSubjectHasNoSession_StillRevokesTheSubjectsGrantsAndDispatches()
    {
        // No session is listed, so the tenant of the context is never read.
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(mockContext);
        SetupSubjectRevocation();

        // A password-grant or device-flow token has no listed session; the subject-wide termination still revokes it.
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReasons.GlobalSignOut
                        && c.Termination.SessionId == null
                        && c.Termination.SubjectId == SubjectId
                        && c.Termination.ParticipatingClientIds.Count == 0
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.RevokeAllAsync(
            mockContext.Object,
            SubjectId,
            SessionTerminationReasons.GlobalSignOut,
            CancellationToken.None
        );

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RevokeAllAsync_WhenTheSubjectIsEmpty_Throws()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service.RevokeAllAsync(
                mockContext.Object,
                subjectId: string.Empty,
                SessionTerminationReasons.GlobalSignOut,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RevokeAllExceptAsync_TerminatesEachOtherSessionIndividually_RetainingCurrent()
    {
        var mockContext = CreateContext();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("s1", UtcNow.AddMinutes(-10)),
            CreateEnvelope("s2", UtcNow.AddMinutes(-5)),
            CreateEnvelope("s3", UtcNow.AddMinutes(-1))
        );
        SetupSessionClaims(mockContext, ("s1", true), ("s3", true));

        // Each retained-exclusive session is terminated on its own (SessionId set, not a subject-wide null), with the
        // self-service reason.
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReasons.UserInitiated
                        && c.Termination.SessionId != null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.RevokeAllExceptAsync(
            mockContext.Object,
            SubjectId,
            exceptSessionId: "s2",
            SessionTerminationReasons.UserInitiated,
            CancellationToken.None
        );

        Assert.Equal(2, result);

        mockMediator.Verify(
            x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c => c.Termination.SessionId == "s1"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        mockMediator.Verify(
            x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c => c.Termination.SessionId == "s3"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        mockMediator.Verify(
            x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c => c.Termination.SessionId == "s2"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task RevokeAllExceptAsync_WhenAnotherCallerTerminatedASession_DispatchesOnlyTheSessionsItClaimed()
    {
        var mockContext = CreateContext();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("s1", UtcNow.AddMinutes(-10)),
            CreateEnvelope("s2", UtcNow.AddMinutes(-5)),
            CreateEnvelope("s3", UtcNow.AddMinutes(-1))
        );
        SetupSessionClaims(mockContext, ("s1", true), ("s3", false));

        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReasons.UserInitiated
                        && c.Termination.SessionId == "s1"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Service.RevokeAllExceptAsync(
            mockContext.Object,
            SubjectId,
            exceptSessionId: "s2",
            SessionTerminationReasons.UserInitiated,
            CancellationToken.None
        );

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task RevokeAllExceptAsync_WhenTheSubjectIsEmpty_Throws()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service.RevokeAllExceptAsync(
                mockContext.Object,
                subjectId: string.Empty,
                exceptSessionId: "s2",
                SessionTerminationReasons.UserInitiated,
                CancellationToken.None
            )
        );
    }

    #endregion
}
