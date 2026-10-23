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
    )> CreateSessionAsync(CreateSessionRequest request)
    {
        var mockContext = CreateContext();
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

    #endregion

    #region AddClientAsync Tests

    [Fact]
    public async Task AddClientAsync_NewClient_UpdatesPayloadAndReturnsTrue()
    {
        var mockContext = CreateContext();
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
            .Returns(
                ValueTask.FromResult<PersistedGrant<SessionGrant>?>(
                    CreateSession(PersistedGrantStatus.Active, [])
                )
            )
            .Verifiable();

        SessionGrant capturedPayload = default;
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAsync(
                    mockContext.Object,
                    grantId,
                    It.IsAny<SessionGrant>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (OpenIdContext _, PersistedGrantId _, SessionGrant payload, CancellationToken _) =>
                    capturedPayload = payload
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
        Assert.Equal(new[] { ClientId }, capturedPayload.ClientIds);
    }

    [Fact]
    public async Task AddClientAsync_ExistingClient_IsIdempotentAndReturnsTrue()
    {
        var mockContext = CreateContext();
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
            .Returns(
                ValueTask.FromResult<PersistedGrant<SessionGrant>?>(
                    CreateSession(PersistedGrantStatus.Active, [ClientId])
                )
            )
            .Verifiable();

        var result = await Service.AddClientAsync(
            mockContext.Object,
            SessionId,
            ClientId,
            CancellationToken.None
        );

        // No UpdatePayloadAsync setup exists; a strict mock would throw if it were called.
        Assert.True(result);
    }

    [Fact]
    public async Task AddClientAsync_InactiveSession_ReturnsFalse()
    {
        var mockContext = CreateContext();
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
            .Returns(
                ValueTask.FromResult<PersistedGrant<SessionGrant>?>(
                    CreateSession(PersistedGrantStatus.Revoked, [])
                )
            )
            .Verifiable();

        var result = await Service.AddClientAsync(
            mockContext.Object,
            SessionId,
            ClientId,
            CancellationToken.None
        );

        Assert.False(result);
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

    private void SetupTouch(
        Mock<OpenIdContext> mockContext,
        PersistedGrantId grantId,
        Action<SessionGrant, DateTimeOffset> onTouch
    ) =>
        MockPersistedGrantService
            .Setup(x =>
                x.UpdatePayloadAndExpirationAsync(
                    mockContext.Object,
                    grantId,
                    It.IsAny<SessionGrant>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    OpenIdContext _,
                    PersistedGrantId _,
                    SessionGrant payload,
                    DateTimeOffset expiresWhen,
                    CancellationToken _
                ) => onTouch(payload, expiresWhen)
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
        SetupGetSession(
            mockContext,
            session with
            {
                Status = PersistedGrantStatus.Expired,
                ExpiresWhen = UtcNow.AddMinutes(-1),
                Payload = session.Payload with { AbsoluteExpiresWhen = UtcNow.AddMinutes(-1) },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
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
        SetupGetSession(
            mockContext,
            session with
            {
                Status = PersistedGrantStatus.Expired,
                ExpiresWhen = UtcNow.AddMinutes(-1),
                Payload = session.Payload with { AbsoluteExpiresWhen = UtcNow.AddDays(29) },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
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
        SetupGetSession(
            mockContext,
            session with
            {
                ExpiresWhen = null,
                Payload = session.Payload with { AbsoluteExpiresWhen = null },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
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
        SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromMinutes(30), UtcNow.AddHours(-1))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
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
    public async Task ValidateAndTouchAsync_WhenActiveBeyondGranularity_SlidesWindow()
    {
        var mockContext = CreateContext();
        var grantId = SetupGetSession(
            mockContext,
            CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-5))
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        SessionGrant captured = default;
        DateTimeOffset capturedExpiresWhen = default;
        SetupTouch(
            mockContext,
            grantId,
            (payload, expiresWhen) =>
            {
                captured = payload;
                capturedExpiresWhen = expiresWhen;
            }
        );

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(UtcNow, captured.LastActivityWhen);
        Assert.Equal(UtcNow.AddDays(30), captured.AbsoluteExpiresWhen);
        Assert.Equal(UtcNow.AddHours(1), capturedExpiresWhen);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenTheAbsoluteEndIsNearerThanTheIdleWindow_SlidesTheExpiryToTheAbsoluteEnd()
    {
        var mockContext = CreateContext();
        var session = CreateIdleSession(TimeSpan.FromHours(1), UtcNow.AddMinutes(-5));
        var grantId = SetupGetSession(
            mockContext,
            session with
            {
                ExpiresWhen = UtcNow.AddMinutes(10),
                Payload = session.Payload with { AbsoluteExpiresWhen = UtcNow.AddMinutes(10) },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        DateTimeOffset capturedExpiresWhen = default;
        SetupTouch(mockContext, grantId, (_, expiresWhen) => capturedExpiresWhen = expiresWhen);

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(UtcNow.AddMinutes(10), capturedExpiresWhen);
    }

    [Fact]
    public async Task ValidateAndTouchAsync_WhenStoredWithoutAnAbsoluteEnd_StampsTheGrantsExpiryAsItsAbsoluteEnd()
    {
        var mockContext = CreateContext();
        var session = CreateIdleSession(idleTimeout: null, UtcNow.AddMinutes(-5));
        var grantId = SetupGetSession(
            mockContext,
            session with
            {
                ExpiresWhen = UtcNow.AddDays(1),
                Payload = session.Payload with { AbsoluteExpiresWhen = null },
            }
        );
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();

        SessionGrant captured = default;
        DateTimeOffset capturedExpiresWhen = default;
        SetupTouch(
            mockContext,
            grantId,
            (payload, expiresWhen) =>
            {
                captured = payload;
                capturedExpiresWhen = expiresWhen;
            }
        );

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(UtcNow.AddDays(1), captured.AbsoluteExpiresWhen);
        Assert.Equal(UtcNow.AddDays(1), capturedExpiresWhen);
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

        // No UpdatePayloadAndExpirationAsync setup exists; a strict mock would throw if the window were persisted.
        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
    }

    [Fact]
    public async Task RevokeAsync_DispatchesTerminationWithSessionParticipants()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        SetupGetSession(mockContext, CreateIdleSession(idleTimeout: null, UtcNow));
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

        await Service.RevokeAsync(
            mockContext.Object,
            SessionId,
            SessionTerminationReasons.AdministrativeRevocation,
            CancellationToken.None
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
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("s1", UtcNow.AddMinutes(-10), clientIds: ["c1", "c2"]),
            CreateEnvelope("s2", UtcNow.AddMinutes(-5), clientIds: ["c2", "c3"])
        );

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

        await Service.RevokeAllAsync(
            mockContext.Object,
            SubjectId,
            SessionTerminationReasons.GlobalSignOut,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task RevokeAllExceptAsync_TerminatesEachOtherSessionIndividually_RetainingCurrent()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockMediator = CreateStrictMock<IMediator>();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
        SetupSessionEnumeration(
            mockContext,
            CreateEnvelope("s1", UtcNow.AddMinutes(-10)),
            CreateEnvelope("s2", UtcNow.AddMinutes(-5)),
            CreateEnvelope("s3", UtcNow.AddMinutes(-1))
        );

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

        await Service.RevokeAllExceptAsync(
            mockContext.Object,
            SubjectId,
            exceptSessionId: "s2",
            SessionTerminationReasons.UserInitiated,
            CancellationToken.None
        );

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

    #endregion
}
