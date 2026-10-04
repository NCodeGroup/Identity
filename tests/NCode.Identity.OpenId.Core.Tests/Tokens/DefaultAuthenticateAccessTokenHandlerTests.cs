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

using Moq;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Logic;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens;

public class DefaultAuthenticateAccessTokenHandlerTests : BaseTests
{
    private const string Issuer = "https://issuer.example.com";
    private const string Token = "token-value";
    private const string ManagementAudience = "urn:ncode:management";

    private Mock<IJsonWebTokenService> MockJsonWebTokenService { get; }
    private DefaultAuthenticateAccessTokenHandler Handler { get; }

    public DefaultAuthenticateAccessTokenHandlerTests()
    {
        MockJsonWebTokenService = CreateStrictMock<IJsonWebTokenService>();
        Handler = new DefaultAuthenticateAccessTokenHandler(MockJsonWebTokenService.Object);
    }

    #region Scaffolding

    private AuthenticateAccessTokenCommand CreateCommand(string token)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        return new AuthenticateAccessTokenCommand(mockContext.Object, token, [ManagementAudience]);
    }

    private void SetupTenantForJwtValidation(OpenIdContext context)
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSecretsProvider = CreateLooseMock<ISecretKeyCollectionProvider>();
        var mockSecretKeys = CreateLooseMock<ISecretKeyCollection>();

        Mock.Get(context).SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer);
        mockTenant.SetupGet(x => x.SecretsProvider).Returns(mockSecretsProvider.Object);
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(mockSecretKeys.Object);
    }

    private void SetupInvalidJwt() =>
        MockJsonWebTokenService
            .Setup(x =>
                x.ValidateJwtAsync(
                    Token,
                    It.IsAny<ValidateJwtParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                ValidateJwtResult.Fail(
                    new ValidateJwtParameters(),
                    PropertyBagFactory.Create(),
                    Token,
                    new InvalidOperationException("invalid")
                )
            )
            .Verifiable();

    private void SetupErrorFactory(OpenIdContext context)
    {
        var mockErrorFactory = CreateStrictMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        Mock.Get(context).SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Returns(mockError.Object)
            .Verifiable();
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenTokenEmpty_ReturnsUndefined()
    {
        var command = CreateCommand(string.Empty);

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.IsUndefined);
        Assert.False(disposition.IsAuthenticated);
        Assert.False(disposition.HasError);
    }

    [Fact]
    public async Task HandleAsync_WhenTokenInvalid_ReturnsError()
    {
        var command = CreateCommand(Token);
        SetupTenantForJwtValidation(command.OpenIdContext);
        SetupInvalidJwt();
        SetupErrorFactory(command.OpenIdContext);

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
        Assert.False(disposition.IsAuthenticated);
    }

    #endregion
}
