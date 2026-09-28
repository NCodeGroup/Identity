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
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.ClientCredentials;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.ClientCredentials;

public class DefaultClientCredentialsGrantHandlerTests : BaseTests
{
    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<ITokenService> MockTokenService { get; }
    private DefaultClientCredentialsGrantHandler Handler { get; }

    public DefaultClientCredentialsGrantHandlerTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockTokenService = CreateStrictMock<ITokenService>();
        Handler = new DefaultClientCredentialsGrantHandler(
            MockTimeProvider.Object,
            MockTokenService.Object
        );
    }

    #region GrantTypes Property Tests

    [Fact]
    public void GrantTypes_Always_ContainsClientCredentials()
    {
        Assert.Contains(OpenIdConstants.GrantTypes.ClientCredentials, Handler.GrantTypes);
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

        var createdWhen = DateTimeOffset.Parse(
            "2025-01-01T00:00:00Z",
            CultureInfo.InvariantCulture
        );
        var tokenLifetime = new TimePeriod
        {
            StartTime = createdWhen,
            EndTime = createdWhen.AddHours(1),
        };
        var securityToken = new SecurityToken
        {
            TokenType = OpenIdConstants.TokenTypes.Bearer,
            TokenValue = "access-token-value",
            TokenLifetime = tokenLifetime,
        };

        List<string> scopes = ["api"];

        mockContext
            .SetupGet(x => x.ErrorFactory)
            .Returns(CreateLooseMock<IOpenIdErrorFactory>().Object);
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockContext.SetupGet(x => x.Environment).Returns(mockEnvironment.Object).Verifiable();

        mockClient.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();
        mockClient
            .SetupGet(x => x.ConfidentialClient)
            .Returns(mockConfidentialClient.Object)
            .Verifiable();

        mockTokenRequest.SetupGet(x => x.Scopes).Returns(scopes).Verifiable();
        mockTokenRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdConstants.GrantTypes.ClientCredentials)
            .Verifiable();

        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(createdWhen).Verifiable();

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
            CancellationToken.None
        );

        var response = Assert.IsType<TokenResponse>(result);
        Assert.Equal("access-token-value", response.AccessToken);
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, response.TokenType);
        Assert.Equal(TimeSpan.FromHours(1), response.ExpiresIn);
        Assert.Equal(scopes, response.Scopes);
    }

    #endregion
}
