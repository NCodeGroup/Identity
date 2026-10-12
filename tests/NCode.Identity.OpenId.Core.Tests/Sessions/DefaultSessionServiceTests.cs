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
using Moq;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
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
    private DefaultSessionService Service { get; }

    public DefaultSessionServiceTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        Service = new DefaultSessionService(
            MockTimeProvider.Object,
            MockCryptoService.Object,
            MockPersistedGrantService.Object
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
            Payload = new SessionGrant
            {
                AuthTime = UtcNow,
                AuthenticationMethods = null,
                IdentityProvider = null,
                ClientIds = clientIds,
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

    #region RevokeAsync Tests

    [Fact]
    public async Task RevokeAsync_Always_RevokesTheSessionGrant()
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
                x.SetRevokedOnceAsync(
                    mockContext.Object,
                    grantId,
                    UtcNow,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.RevokeAsync(mockContext.Object, SessionId, UtcNow, CancellationToken.None);
    }

    #endregion
}
