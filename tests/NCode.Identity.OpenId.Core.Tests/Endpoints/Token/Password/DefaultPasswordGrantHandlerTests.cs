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
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.Password;

public class DefaultPasswordGrantHandlerTests : BaseTests
{
    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<ITokenService> MockTokenService { get; }
    private DefaultPasswordGrantHandler Handler { get; }

    public DefaultPasswordGrantHandlerTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockTokenService = CreateStrictMock<ITokenService>();
        Handler = new DefaultPasswordGrantHandler(MockTimeProvider.Object, MockTokenService.Object);
    }

    #region Scaffolding

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

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
        var mockMediator = CreateStrictMock<IMediator>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        // ErrorFactory and Mediator are read unconditionally at the top of every path.
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        return (mockContext, mockClient, mockMediator, mockTokenRequest, mockError);
    }

    private void SetupAuthenticate(
        Mock<IMediator> mockMediator,
        AuthenticateSubjectDisposition disposition
    ) =>
        mockMediator
            .Setup(x =>
                x.SendAsync<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>(
                    It.IsAny<AuthenticatePasswordGrantCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<AuthenticateSubjectDisposition>(disposition))
            .Verifiable();

    private void SetupTokenResponse(
        Mock<OpenIdContext> mockContext,
        Mock<OpenIdClient> mockClient,
        Mock<IMediator> mockMediator,
        Mock<ITokenRequest> mockTokenRequest,
        List<string> scopes
    )
    {
        mockContext
            .SetupGet(x => x.Environment)
            .Returns(CreateStrictMock<OpenIdEnvironment>().Object)
            .Verifiable();

        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateTokenGrantCommand<PasswordGrant>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns(scopes).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdConstants.GrantTypes.Password)
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
    }

    #endregion

    #region GrantTypes Property Tests

    [Fact]
    public void GrantTypes_Always_ContainsPassword()
    {
        Assert.Contains(OpenIdConstants.GrantTypes.Password, Handler.GrantTypes);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenUsernameMissing_ReturnsMissingParameterError()
    {
        var (mockContext, mockClient, _, mockTokenRequest, mockError) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.Username).Returns((string?)null).Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenNotAuthenticatedWithError_ReturnsThatError()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, _) = CreateScaffold();

        var mockDispositionError = CreateLooseMock<IOpenIdError>();

        mockTokenRequest.SetupGet(x => x.Username).Returns("username").Verifiable();
        SetupAuthenticate(
            mockMediator,
            new AuthenticateSubjectDisposition(mockDispositionError.Object)
        );

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockDispositionError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenNotAuthenticatedWithoutError_ReturnsInvalidGrantError()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, mockError) = CreateScaffold();

        mockTokenRequest.SetupGet(x => x.Username).Returns("username").Verifiable();
        SetupAuthenticate(
            mockMediator,
            new AuthenticateSubjectDisposition(Error: null, Ticket: null)
        );

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthenticated_ReturnsTokenResponseWithAccessToken()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, _) = CreateScaffold();

        List<string> scopes = ["api"];

        mockTokenRequest.SetupGet(x => x.Username).Returns("username").Verifiable();
        SetupAuthenticate(
            mockMediator,
            new AuthenticateSubjectDisposition(CreateSubjectAuthentication())
        );
        SetupTokenResponse(mockContext, mockClient, mockMediator, mockTokenRequest, scopes);

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
        Assert.Equal(scopes, response.Scopes);
        Assert.Null(response.IdToken);
        Assert.Null(response.RefreshToken);
    }

    [Fact]
    public async Task HandleAsync_WhenOpenIdScopeRequested_IncludesIdToken()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, _) = CreateScaffold();

        List<string> scopes = [OpenIdConstants.ScopeTypes.OpenId];

        mockTokenRequest.SetupGet(x => x.Username).Returns("username").Verifiable();
        SetupAuthenticate(
            mockMediator,
            new AuthenticateSubjectDisposition(CreateSubjectAuthentication())
        );
        SetupTokenResponse(mockContext, mockClient, mockMediator, mockTokenRequest, scopes);

        MockTokenService
            .Setup(x =>
                x.CreateIdTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("id-token-value")))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("id-token-value", response.IdToken);
    }

    [Fact]
    public async Task HandleAsync_WhenOfflineAccessScopeRequested_IncludesRefreshToken()
    {
        var (mockContext, mockClient, mockMediator, mockTokenRequest, _) = CreateScaffold();

        List<string> scopes = [OpenIdConstants.ScopeTypes.OfflineAccess];

        mockTokenRequest.SetupGet(x => x.Username).Returns("username").Verifiable();
        SetupAuthenticate(
            mockMediator,
            new AuthenticateSubjectDisposition(CreateSubjectAuthentication())
        );
        SetupTokenResponse(mockContext, mockClient, mockMediator, mockTokenRequest, scopes);

        MockTokenService
            .Setup(x =>
                x.CreateRefreshTokenAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("refresh-token-value")))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("refresh-token-value", response.RefreshToken);
    }

    #endregion
}
