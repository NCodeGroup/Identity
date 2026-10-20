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

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Management.Contracts.Sessions;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Sessions;

public sealed class SessionApiEndpointHandlerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string SubjectId = "subject-1";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<ISessionService> MockSessionService { get; }
    private Mock<IOpenIdContextFactory> MockOpenIdContextFactory { get; }
    private Mock<IManagementAuditRecorder> MockManagementAuditRecorder { get; }
    private SessionApiEndpointHandler Handler { get; }

    public SessionApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();
        MockSessionService = MockRepository.Create<ISessionService>();
        MockOpenIdContextFactory = MockRepository.Create<IOpenIdContextFactory>();
        MockManagementAuditRecorder = MockRepository.Create<IManagementAuditRecorder>();

        Handler = new SessionApiEndpointHandler(
            MockAuthorizationService.Object,
            MockCryptoService.Object,
            MockSessionService.Object,
            MockOpenIdContextFactory.Object,
            MockManagementAuditRecorder.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private void SetupAuthorization(AuthorizationResult result)
    {
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
    }

    // Mocks the addressed-tenant context the revoke paths materialize; a loose context suffices as it is only passed
    // through to the (strict) session service and disposed.
    private void SetupTenantContext()
    {
        MockOpenIdContextFactory
            .Setup(x =>
                x.CreateForTenantAsync(
                    It.IsAny<HttpContext>(),
                    TenantId,
                    It.IsAny<IMediator>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Mock.Of<OpenIdContext>())
            .Verifiable();
    }

    private static HttpContext CreateHttpContext(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IMediator>());
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = services.BuildServiceProvider(),
        };
        // The shared environment filter publishes the context in the pipeline; emulate it for direct handler calls.
        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature =>
                feature.OpenIdContext == Mock.Of<OpenIdContext>()
            )
        );
        return httpContext;
    }

    private static SessionInfo CreateSession(string sessionId) =>
        new(
            sessionId,
            SubjectId,
            DateTimeOffset.UnixEpoch,
            ExpiresWhen: null,
            new SessionGrant
            {
                AuthTime = DateTimeOffset.UnixEpoch,
                ClientIds = [],
                LastActivityWhen = DateTimeOffset.UnixEpoch,
            }
        );

    #endregion

    #region ListSessionsAsync Tests

    [Fact]
    public async Task ListSessionsAsync_WhenNoSubject_ReturnsBadRequestWithoutAuthorizing()
    {
        var httpContext = CreateHttpContext(authenticated: true);

        // No authorization scaffold: a strict-mock failure would mean the guard ran too late.
        var result = await Handler.ListSessionsAsync(
            httpContext,
            TenantId,
            subjectId: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task ListSessionsAsync_WhenNotAuthorized_ReturnsForbid()
    {
        SetupAuthorization(AuthorizationResult.Failed());
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListSessionsAsync(
            httpContext,
            TenantId,
            SubjectId,
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task ListSessionsAsync_WhenAuthorized_ReturnsSessions()
    {
        SetupAuthorization(AuthorizationResult.Success());
        MockSessionService
            .Setup(x =>
                x.GetSessionsAsync(
                    It.IsAny<OpenIdContext>(),
                    SubjectId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateSession("s1"), CreateSession("s2")])
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.ListSessionsAsync(
            httpContext,
            TenantId,
            SubjectId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<SessionResource>>>(result);
        Assert.NotNull(json.Value);
        Assert.Equal(["s1", "s2"], json.Value.Items.Select(item => item.SessionId));
    }

    #endregion

    #region RevokeSessionsAsync Tests

    [Fact]
    public async Task RevokeSessionsAsync_WhenNoSubject_ReturnsBadRequestWithoutAuthorizing()
    {
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeSessionsAsync(
            httpContext,
            TenantId,
            subjectId: null,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task RevokeSessionsAsync_WhenAuthorized_RevokesAllAndAudits()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantContext();
        MockSessionService
            .Setup(x =>
                x.GetSessionsAsync(
                    It.IsAny<OpenIdContext>(),
                    SubjectId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([CreateSession("s1"), CreateSession("s2")])
            .Verifiable();
        MockSessionService
            .Setup(x =>
                x.RevokeAllAsync(
                    It.IsAny<OpenIdContext>(),
                    SubjectId,
                    SessionTerminationReasons.AdministrativeRevocation,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordSessionsRevokedAsync(
                    It.IsAny<HttpContext>(),
                    TenantId,
                    SubjectId,
                    null,
                    2,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeSessionsAsync(
            httpContext,
            TenantId,
            SubjectId,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<SessionRevocationResult>>(result);
        Assert.NotNull(json.Value);
        Assert.Equal(2, json.Value.Revoked);
    }

    #endregion

    #region RevokeSessionAsync Tests

    [Fact]
    public async Task RevokeSessionAsync_WhenNotFound_ReturnsNoContentWithoutRevoking()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantContext();
        MockSessionService
            .Setup(x =>
                x.GetOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    "missing",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((PersistedGrant<SessionGrant>?)null)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        // No RevokeAsync / audit scaffold: a strict-mock failure would mean a missing session was still terminated.
        var result = await Handler.RevokeSessionAsync(
            httpContext,
            TenantId,
            "missing",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task RevokeSessionAsync_WhenFound_RevokesAndAudits()
    {
        SetupAuthorization(AuthorizationResult.Success());
        SetupTenantContext();
        MockSessionService
            .Setup(x =>
                x.GetOrDefaultAsync(It.IsAny<OpenIdContext>(), "s1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PersistedGrant<SessionGrant>
                {
                    TenantId = TenantId,
                    ClientId = null,
                    SubjectId = SubjectId,
                    SessionId = "s1",
                    Payload = new SessionGrant
                    {
                        AuthTime = DateTimeOffset.UnixEpoch,
                        ClientIds = [],
                        LastActivityWhen = DateTimeOffset.UnixEpoch,
                    },
                }
            )
            .Verifiable();
        MockSessionService
            .Setup(x =>
                x.RevokeAsync(
                    It.IsAny<OpenIdContext>(),
                    "s1",
                    SessionTerminationReasons.AdministrativeRevocation,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockManagementAuditRecorder
            .Setup(x =>
                x.RecordSessionsRevokedAsync(
                    It.IsAny<HttpContext>(),
                    TenantId,
                    SubjectId,
                    "s1",
                    1,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.RevokeSessionAsync(
            httpContext,
            TenantId,
            "s1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }

    [Fact]
    public async Task RevokeSessionAsync_WhenNotAuthorized_ReturnsForbid()
    {
        SetupAuthorization(AuthorizationResult.Failed());
        var httpContext = CreateHttpContext(authenticated: true);

        // No context/session scaffold: authorization must short-circuit before materializing the tenant.
        var result = await Handler.RevokeSessionAsync(
            httpContext,
            TenantId,
            "s1",
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    #endregion
}
