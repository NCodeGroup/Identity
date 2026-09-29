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
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Environments;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Handlers;

public class DefaultCreateAuthorizationTicketHandlerTests : BaseTests
{
    private const string Issuer = "https://issuer.example";

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IAuthorizationCodeService> MockAuthorizationCodeService { get; }
    private Mock<ITokenService> MockTokenService { get; }
    private DefaultCreateAuthorizationTicketHandler Handler { get; }

    public DefaultCreateAuthorizationTicketHandlerTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockAuthorizationCodeService = CreateStrictMock<IAuthorizationCodeService>();
        MockTokenService = CreateStrictMock<ITokenService>();
        Handler = new DefaultCreateAuthorizationTicketHandler(
            MockTimeProvider.Object,
            MockAuthorizationCodeService.Object,
            MockTokenService.Object
        );
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
        CreateAuthorizationTicketCommand command,
        Mock<OpenIdContext> context,
        Mock<OpenIdClient> client
    ) CreateScaffold(IReadOnlyList<string> responseTypes)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockAuthRequest = CreateStrictMock<IAuthorizationRequest>();

        mockContext
            .SetupGet(x => x.Environment)
            .Returns(CreateStrictMock<OpenIdEnvironment>().Object)
            .Verifiable();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer).Verifiable();

        mockAuthRequest.SetupGet(x => x.ResponseTypes).Returns(responseTypes).Verifiable();
        mockAuthRequest.SetupGet(x => x.State).Returns("state-value").Verifiable();
        mockAuthRequest
            .SetupGet(x => x.GrantType)
            .Returns(OpenIdConstants.GrantTypes.AuthorizationCode)
            .Verifiable();
        mockAuthRequest.SetupGet(x => x.Nonce).Returns("nonce-value").Verifiable();
        IReadOnlyList<string> scopes = ["openid"];
        mockAuthRequest.SetupGet(x => x.Scopes).Returns(scopes).Verifiable();

        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(CreatedWhen).Verifiable();

        var command = new CreateAuthorizationTicketCommand(
            mockContext.Object,
            mockClient.Object,
            mockAuthRequest.Object,
            CreateSubjectAuthentication()
        );

        return (command, mockContext, mockClient);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenCodeResponseType_SetsAuthorizationCode()
    {
        var (command, mockContext, mockClient) = CreateScaffold([
            OpenIdConstants.ResponseTypes.Code,
        ]);

        MockAuthorizationCodeService
            .Setup(x =>
                x.CreateAuthorizationCodeAsync(
                    mockContext.Object,
                    mockClient.Object,
                    It.IsAny<IAuthorizationRequest>(),
                    It.IsAny<SubjectAuthentication>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<SecurityToken>(CreateSecurityToken("authorization-code-value")))
            .Verifiable();

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("authorization-code-value", result.AuthorizationCode);
        Assert.Equal(Issuer, result.Issuer);
        Assert.Equal("state-value", result.State);
        Assert.Null(result.AccessToken);
        Assert.Null(result.IdToken);
    }

    [Fact]
    public async Task HandleAsync_WhenTokenResponseType_SetsAccessToken()
    {
        var (command, mockContext, mockClient) = CreateScaffold([
            OpenIdConstants.ResponseTypes.Token,
        ]);

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

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("access-token-value", result.AccessToken);
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, result.TokenType);
        Assert.Equal(TimeSpan.FromHours(1), result.ExpiresIn);
        Assert.Null(result.AuthorizationCode);
    }

    [Fact]
    public async Task HandleAsync_WhenIdTokenResponseType_SetsIdToken()
    {
        var (command, mockContext, mockClient) = CreateScaffold([
            OpenIdConstants.ResponseTypes.IdToken,
        ]);

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

        var result = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("id-token-value", result.IdToken);
        Assert.Null(result.AuthorizationCode);
        Assert.Null(result.AccessToken);
    }

    #endregion
}
