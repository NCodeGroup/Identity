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
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.EndSession;

public class DefaultResolveEndSessionSubjectHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string Issuer = "https://op.example";
    private const string SubjectId = "subject-1";
    private const string PairwisePseudonym = "ppid-opaque-pseudonym";
    private const string SessionId = "sid-1";
    private const string ClientId = "client-1";

    private Mock<IJwtValidator> MockJsonWebTokenService { get; }
    private Mock<ISessionService> MockSessionService { get; }
    private DefaultResolveEndSessionSubjectHandler Handler { get; }

    public DefaultResolveEndSessionSubjectHandlerTests()
    {
        MockJsonWebTokenService = CreateStrictMock<IJwtValidator>();
        MockSessionService = CreateStrictMock<ISessionService>();
        Handler = new DefaultResolveEndSessionSubjectHandler(
            MockJsonWebTokenService.Object,
            MockSessionService.Object,
            NullLogger<DefaultResolveEndSessionSubjectHandler>.Instance
        );
    }

    #region Scaffolding

    private Mock<OpenIdContext> CreateContext(HttpContext? httpContext = null)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        if (httpContext is not null)
        {
            mockContext.SetupGet(x => x.Http).Returns(httpContext).Verifiable();
        }

        return mockContext;
    }

    private static HttpContext CreateHttpContext(Mock<IAuthenticationService> mockAuthService)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(mockAuthService.Object);
        return new DefaultHttpContext
        {
            RequestServices = serviceCollection.BuildServiceProvider(),
        };
    }

    private void SetupTenant(Mock<OpenIdContext> mockContext)
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSecretsProvider = CreateLooseMock<ISecretKeyCollectionProvider>();
        var mockSecretKeys = CreateLooseMock<ISecretKeyCollection>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer).Verifiable();
        mockTenant
            .SetupGet(x => x.SecretsProvider)
            .Returns(mockSecretsProvider.Object)
            .Verifiable();
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(mockSecretKeys.Object);
    }

    private Mock<IEndSessionRequest> CreateRequest(string? idTokenHint, string? clientId)
    {
        var mockRequest = CreateStrictMock<IEndSessionRequest>();
        mockRequest.SetupGet(x => x.IdTokenHint).Returns(idTokenHint).Verifiable();
        mockRequest.SetupGet(x => x.ClientId).Returns(clientId).Verifiable();
        return mockRequest;
    }

    private void SetupSession(
        OpenIdContext context,
        string sessionId,
        IReadOnlyList<string> clientIds
    ) =>
        MockSessionService
            .Setup(x => x.GetOrDefaultAsync(context, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PersistedGrant<SessionGrant>
                {
                    Status = PersistedGrantStatus.Active,
                    TenantId = TenantId,
                    ClientId = null,
                    SubjectId = SubjectId,
                    SessionId = null,
                    Payload = new SessionGrant
                    {
                        AuthTime = DateTimeOffset.UnixEpoch,
                        AuthenticationMethods = null,
                        IdentityProvider = null,
                        ClientIds = clientIds,
                        LastActivityWhen = DateTimeOffset.UnixEpoch,
                    },
                }
            )
            .Verifiable();

    private void SetupSessionNotFound(OpenIdContext context, string sessionId) =>
        MockSessionService
            .Setup(x => x.GetOrDefaultAsync(context, sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedGrant<SessionGrant>?)null)
            .Verifiable();

    private void SetupValidHint(JsonElement payload)
    {
        var compactJwt = new CompactJwt("JWS", default, "aaa.bbb.ccc", []);
        var decodedJwt = new DecodedJwt(compactJwt, payload, secretKey: null!);
        var result = JwtValidationResult.Success(
            new JwtValidationParameters(),
            PropertyBagFactory.Create(),
            decodedJwt,
            (_, _, _, _, _, _) => ValueTask.FromResult(new ClaimsIdentity())
        );

        MockJsonWebTokenService
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<string>(),
                    It.IsAny<JwtValidationParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
    }

    private void SetupInvalidHint() =>
        MockJsonWebTokenService
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<string>(),
                    It.IsAny<JwtValidationParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                JwtValidationResult.Fail(
                    new JwtValidationParameters(),
                    PropertyBagFactory.Create(),
                    "token",
                    new InvalidOperationException("invalid")
                )
            )
            .Verifiable();

    private static JsonElement CreatePayload(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    private static AuthenticateResult SucceededCookie(params Claim[] claims)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test-cookie"));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, "test-cookie"));
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenValidIdTokenHint_ResolvesSubjectFromSessionGrantNotHint()
    {
        var mockContext = CreateContext(new DefaultHttpContext());
        SetupTenant(mockContext);
        // The hint carries a pairwise pseudonym as `sub`; it must be ignored and the stable subject taken from the
        // persisted session grant (the OP never reverses a one-way pairwise projection).
        SetupValidHint(
            CreatePayload(
                $$"""{"sub":"{{PairwisePseudonym}}","sid":"{{SessionId}}","azp":"{{ClientId}}"}"""
            )
        );
        SetupSession(mockContext.Object, SessionId, [ClientId, "client-2"]);

        var request = CreateRequest(idTokenHint: "aaa.bbb.ccc", clientId: null);

        var result = await Handler.HandleAsync(
            new ResolveEndSessionSubjectCommand(mockContext.Object, request.Object),
            CancellationToken.None
        );

        Assert.Equal(SubjectId, result.SubjectId);
        Assert.NotEqual(PairwisePseudonym, result.SubjectId);
        Assert.Equal(SessionId, result.SessionId);
        Assert.Equal(ClientId, result.ClientId);
        Assert.Equal(new[] { ClientId, "client-2" }, result.ParticipatingClientIds);
    }

    [Fact]
    public async Task HandleAsync_WhenPairwiseHintSubButSessionNotFound_LeavesSubjectUnresolved()
    {
        var mockContext = CreateContext(new DefaultHttpContext());
        SetupTenant(mockContext);
        // A hint that carries only a pairwise `sub` (plus sid) whose session has already lapsed cannot be mapped back
        // to the stable subject without reversing the one-way projection, which the OP refuses to do.
        SetupValidHint(
            CreatePayload(
                $$"""{"sub":"{{PairwisePseudonym}}","sid":"{{SessionId}}","azp":"{{ClientId}}"}"""
            )
        );
        SetupSessionNotFound(mockContext.Object, SessionId);

        var request = CreateRequest(idTokenHint: "aaa.bbb.ccc", clientId: null);

        var result = await Handler.HandleAsync(
            new ResolveEndSessionSubjectCommand(mockContext.Object, request.Object),
            CancellationToken.None
        );

        Assert.Null(result.SubjectId);
        Assert.Equal(SessionId, result.SessionId);
        Assert.Equal(ClientId, result.ClientId);
        Assert.Empty(result.ParticipatingClientIds);
    }

    [Fact]
    public async Task HandleAsync_WhenInvalidIdTokenHint_FallsBackToCookie()
    {
        var mockAuthService = CreateStrictMock<IAuthenticationService>();
        var httpContext = CreateHttpContext(mockAuthService);
        var mockContext = CreateContext(httpContext);
        SetupTenant(mockContext);
        SetupInvalidHint();

        mockContext
            .Setup(x => x.GetSubjectId(It.IsAny<ClaimsPrincipal>()))
            .Returns(SubjectId)
            .Verifiable();
        mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(SucceededCookie(new Claim(JoseClaimNames.Payload.Sid, SessionId)))
            .Verifiable();
        SetupSession(mockContext.Object, SessionId, [ClientId]);

        var request = CreateRequest(idTokenHint: "aaa.bbb.ccc", clientId: null);

        var result = await Handler.HandleAsync(
            new ResolveEndSessionSubjectCommand(mockContext.Object, request.Object),
            CancellationToken.None
        );

        Assert.Equal(SubjectId, result.SubjectId);
        Assert.Equal(SessionId, result.SessionId);
        Assert.Equal(new[] { ClientId }, result.ParticipatingClientIds);
    }

    [Fact]
    public async Task HandleAsync_WhenNoHintAndCookiePresent_ResolvesFromCookie()
    {
        var mockAuthService = CreateStrictMock<IAuthenticationService>();
        var httpContext = CreateHttpContext(mockAuthService);
        var mockContext = CreateContext(httpContext);

        mockContext
            .Setup(x => x.GetSubjectId(It.IsAny<ClaimsPrincipal>()))
            .Returns(SubjectId)
            .Verifiable();
        mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(SucceededCookie(new Claim(JoseClaimNames.Payload.Sid, SessionId)))
            .Verifiable();
        SetupSession(mockContext.Object, SessionId, [ClientId]);

        var request = CreateRequest(idTokenHint: null, clientId: ClientId);

        var result = await Handler.HandleAsync(
            new ResolveEndSessionSubjectCommand(mockContext.Object, request.Object),
            CancellationToken.None
        );

        Assert.Equal(SubjectId, result.SubjectId);
        Assert.Equal(SessionId, result.SessionId);
        Assert.Equal(ClientId, result.ClientId);
        Assert.Equal(new[] { ClientId }, result.ParticipatingClientIds);
    }

    [Fact]
    public async Task HandleAsync_WhenSessionNotFound_ReturnsEmptyParticipants()
    {
        var mockAuthService = CreateStrictMock<IAuthenticationService>();
        var httpContext = CreateHttpContext(mockAuthService);
        var mockContext = CreateContext(httpContext);

        mockContext
            .Setup(x => x.GetSubjectId(It.IsAny<ClaimsPrincipal>()))
            .Returns(SubjectId)
            .Verifiable();
        mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(SucceededCookie(new Claim(JoseClaimNames.Payload.Sid, SessionId)))
            .Verifiable();
        SetupSessionNotFound(mockContext.Object, SessionId);

        var request = CreateRequest(idTokenHint: null, clientId: null);

        var result = await Handler.HandleAsync(
            new ResolveEndSessionSubjectCommand(mockContext.Object, request.Object),
            CancellationToken.None
        );

        Assert.Equal(SessionId, result.SessionId);
        Assert.Empty(result.ParticipatingClientIds);
    }

    [Fact]
    public async Task HandleAsync_WhenNoHintAndNoCookie_ReturnsUnresolved()
    {
        var mockAuthService = CreateStrictMock<IAuthenticationService>();
        var httpContext = CreateHttpContext(mockAuthService);
        var mockContext = CreateContext(httpContext);

        mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string?>()))
            .ReturnsAsync(AuthenticateResult.NoResult())
            .Verifiable();

        var request = CreateRequest(idTokenHint: null, clientId: null);

        var result = await Handler.HandleAsync(
            new ResolveEndSessionSubjectCommand(mockContext.Object, request.Object),
            CancellationToken.None
        );

        Assert.Null(result.SubjectId);
        Assert.Null(result.SessionId);
        Assert.Empty(result.ParticipatingClientIds);
    }

    #endregion
}
