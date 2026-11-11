// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.ClientCredentials;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Time;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.ClientCredentials;

public class DefaultClientCredentialsGrantHandlerTests : BaseTests
{
    private static DateTimeOffset CreatedWhen { get; } =
        DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture);

    private FakeTimeProvider TimeProvider { get; } = new(CreatedWhen);
    private Mock<ITokenService> MockTokenService { get; }
    private DefaultClientCredentialsGrantHandler Handler { get; }

    public DefaultClientCredentialsGrantHandlerTests()
    {
        MockTokenService = CreateStrictMock<ITokenService>();
        Handler = new DefaultClientCredentialsGrantHandler(TimeProvider, MockTokenService.Object);
    }

    #region GrantTypes Property Tests

    [Fact]
    public void GrantTypes_Always_ContainsClientCredentials()
    {
        Assert.Contains(OpenIdProtocol.GrantTypes.ClientCredentials, Handler.GrantTypes);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenClientNotConfidential_ReturnsInvalidClientError()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        mockClient.SetupGet(x => x.IsConfidential).Returns(false).Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenClientConfidential_ReturnsTokenResponseWithAccessToken()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockConfidentialClient = CreateStrictMock<OpenIdConfidentialClient>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();

        var tokenLifetime = new TimePeriod
        {
            StartTime = CreatedWhen,
            EndTime = CreatedWhen.AddHours(1),
        };
        var securityToken = new SecurityToken
        {
            TokenType = OpenIdProtocol.TokenTypes.Bearer,
            TokenValue = "access-token-value",
            TokenLifetime = tokenLifetime,
        };

        List<string> scopes = ["api"];

        mockContext
            .SetupGet(x => x.ErrorFactory)
            .Returns(CreateLooseMock<IOpenIdErrorFactory>().Object)
            .Verifiable();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockContext.SetupGet(x => x.Environment).Returns(mockEnvironment.Object).Verifiable();

        mockClient.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();
        mockClient
            .SetupGet(x => x.ConfidentialClient)
            .Returns(mockConfidentialClient.Object)
            .Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns(scopes).Verifiable();
        mockTokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdProtocol.GrantTypes.ClientCredentials)
            .Verifiable();

        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateTokenGrantCommand<ClientCredentialsGrant>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        MockTokenService
            .Setup(x =>
                x.CreateAccessTokenAsync(
                    mockContext.Object,
                    mockConfidentialClient.Object,
                    It.IsAny<CreateSecurityTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(securityToken))
            .Verifiable();

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("access-token-value", response.AccessToken);
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, response.TokenType);
        Assert.Equal(TimeSpan.FromHours(1), response.ExpiresIn);
        Assert.Equal(scopes, response.Scopes);
    }

    #endregion
}
