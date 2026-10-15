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
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
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

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_Always_PersistsSessionGrantAndReturnsSid()
    {
        var mockContext = CreateContext();
        var grantId = CreateGrantId();

        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<BinaryEncodingType>()))
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

        var request = new CreateSessionRequest
        {
            SubjectId = SubjectId,
            AuthTime = UtcNow,
            AuthenticationMethods = ["pwd"],
            IdentityProvider = "idp-1",
            Lifetime = TimeSpan.FromHours(8),
        };

        var result = await Service.CreateAsync(mockContext.Object, request, CancellationToken.None);

        Assert.Equal(SessionId, result);
        Assert.Equal(PersistedGrantStatus.Active, capturedGrant.Status);
        Assert.Equal(SubjectId, capturedGrant.SubjectId);
        Assert.Null(capturedGrant.ClientId);
        Assert.Equal(UtcNow, capturedGrant.Payload.AuthTime);
        Assert.Equal(new[] { "pwd" }, capturedGrant.Payload.AuthenticationMethods);
        Assert.Equal("idp-1", capturedGrant.Payload.IdentityProvider);
        Assert.Empty(capturedGrant.Payload.ClientIds);
        Assert.Equal(UtcNow, capturedCreatedWhen);
        Assert.Equal(TimeSpan.FromHours(8), capturedLifetime);
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
            Payload = new SessionGrant
            {
                AuthTime = UtcNow,
                ClientIds = ["client-a"],
                LastActivityWhen = lastActivityWhen,
                IdleTimeout = idleTimeout,
            },
        };

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

    [Fact]
    public async Task ValidateAndTouchAsync_WhenNoIdleTimeout_ReturnsTrueWithoutWrite()
    {
        var mockContext = CreateContext();
        SetupGetSession(mockContext, CreateIdleSession(idleTimeout: null, UtcNow));

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
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
                        c.Termination.Reason == SessionTerminationReason.IdleTimeout
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
                (OpenIdContext _, PersistedGrantId _, SessionGrant p, CancellationToken _) =>
                    captured = p
            )
            .ReturnsAsync(true)
            .Verifiable();

        var result = await Service.ValidateAndTouchAsync(
            mockContext.Object,
            SessionId,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(UtcNow, captured.LastActivityWhen);
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
    public async Task RevokeAsync_DispatchesTerminationWithSessionParticipants()
    {
        var mockContext = CreateContextWithMediator(out var mockMediator);
        SetupGetSession(mockContext, CreateIdleSession(idleTimeout: null, UtcNow));
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.Is<OnSessionTerminatedCommand>(c =>
                        c.Termination.Reason == SessionTerminationReason.AdministrativeRevocation
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
            SessionTerminationReason.AdministrativeRevocation,
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
                    null,
                    It.IsAny<int>(),
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
        IReadOnlyList<string>? clientIds = null
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
            ExpiresWhen = null,
            RevokedWhen = revokedWhen,
            ConsumedWhen = null,
            PayloadJson = JsonSerializer.SerializeToElement(
                new SessionGrant
                {
                    AuthTime = createdWhen,
                    ClientIds = clientIds ?? [],
                    LastActivityWhen = lastActivityWhen ?? createdWhen,
                    IdleTimeout = idleTimeout,
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
                        c.Termination.Reason == SessionTerminationReason.GlobalSignOut
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

        await Service.RevokeAllAsync(mockContext.Object, SubjectId, CancellationToken.None);
    }

    #endregion
}
