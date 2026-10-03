#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.AuthorizationCode;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.AuthorizationCode;

public class DefaultAuthorizationCodeGrantHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string AuthorizationCodeValue = "authorization-code";

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<ITokenService> MockTokenService { get; }
    private DefaultAuthorizationCodeGrantHandler Handler { get; }

    public DefaultAuthorizationCodeGrantHandlerTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockTokenService = CreateStrictMock<ITokenService>();
        Handler = new DefaultAuthorizationCodeGrantHandler(
            MockTimeProvider.Object,
            MockPersistedGrantService.Object,
            MockTokenService.Object
        );
    }

    #region Scaffolding

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private static PersistedGrantId CreateGrantId() =>
        new()
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.AuthorizationCode,
            GrantKey = AuthorizationCodeValue,
        };

    private static SecurityToken CreateSecurityToken(string tokenValue) =>
        new()
        {
            TokenType = OpenIdConstants.TokenTypes.Bearer,
            TokenValue = tokenValue,
            TokenLifetime = new TimePeriod
            {
                StartTime = CreatedWhen,
                EndTime = CreatedWhen.AddHours(1),
            },
        };

    private (
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client,
        Mock<IMediator> mediator,
        Mock<ITokenRequest> tokenRequest,
        Mock<IOpenIdError> error
    ) CreateScaffold()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        // Tenant, ErrorFactory and Mediator are read unconditionally at the top of every path.
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        return (mockContext, mockClient, mockMediator, mockTokenRequest, mockError);
    }

    private void SetupConsume(
        Mock<ITokenRequest> mockTokenRequest,
        PersistedGrant<AuthorizationGrant>? persistedGrant
    )
    {
        mockTokenRequest
            .SetupGet(x => x.AuthorizationCode)
            .Returns(AuthorizationCodeValue)
            .Verifiable();

        MockPersistedGrantService
            .Setup(x => x.CreateGrantId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(CreateGrantId())
            .Verifiable();

        MockPersistedGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<AuthorizationGrant>(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<PersistedGrantId>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<AuthorizationGrant>?>(persistedGrant))
            .Verifiable();
    }

    #endregion

    #region GrantTypes Property Tests

    [Fact]
    public void GrantTypes_Always_ContainsAuthorizationCode()
    {
        Assert.Contains(OpenIdConstants.GrantTypes.AuthorizationCode, Handler.GrantTypes);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAuthorizationCodeMissing_ReturnsMissingParameterError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockError) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.AuthorizationCode).Returns((string?)null).Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantNotFound_ReturnsInvalidGrantError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockError) = CreateScaffold();

        SetupConsume(mockTokenRequest, null);

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenGrantActive_ReturnsTokenResponseWithAccessToken()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, _) = CreateScaffold();

        var mockAuthRequest = CreateStrictMock<IAuthorizationRequest>();
        mockAuthRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();
        mockAuthRequest.SetupGet(x => x.Nonce).Returns((string?)null).Verifiable();
        mockAuthRequest.SetupGet(x => x.State).Returns((string?)null).Verifiable();
        mockAuthRequest.SetupGet(x => x.Claims).Returns((IRequestClaims?)null).Verifiable();

        var authorizationGrant = new AuthorizationGrant(
            mockAuthRequest.Object,
            CreateSubjectAuthentication()
        );
        var persistedGrant = new PersistedGrant<AuthorizationGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            Payload = authorizationGrant,
        };
        SetupConsume(mockTokenRequest, persistedGrant);

        mockContext
            .SetupGet(x => x.Environment)
            .Returns(CreateStrictMock<OpenIdEnvironment>().Object)
            .Verifiable();

        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateTokenGrantCommand<AuthorizationGrant>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdConstants.GrantTypes.AuthorizationCode)
            .Verifiable();

        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(CreatedWhen).Verifiable();

        MockTokenService
            .Setup(x =>
                x.CreateAccessTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("access-token-value")))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("access-token-value", response.AccessToken);
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, response.TokenType);
        Assert.Equal(TimeSpan.FromHours(1), response.ExpiresIn);
        Assert.Equal(["api"], response.Scopes);
    }

    #endregion
}
