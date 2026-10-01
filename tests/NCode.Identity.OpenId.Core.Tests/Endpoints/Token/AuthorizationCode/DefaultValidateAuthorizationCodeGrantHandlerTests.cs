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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.AuthorizationCode;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.AuthorizationCode;

public class DefaultValidateAuthorizationCodeGrantHandlerTests : BaseTests
{
    private const string ClientId = "client-1";
    private const string TenantId = "tenant-1";
    private static readonly Uri RedirectUri = new("https://client/callback");

    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IClientScopeService> MockClientScopeService { get; }
    private DefaultValidateAuthorizationCodeGrantHandler Handler { get; }

    public DefaultValidateAuthorizationCodeGrantHandlerTests()
    {
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockClientScopeService = CreateStrictMock<IClientScopeService>();
        Handler = new DefaultValidateAuthorizationCodeGrantHandler(
            MockCryptoService.Object,
            MockClientScopeService.Object
        );
    }

    #region Scaffolding

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private (
        ValidateTokenGrantCommand<AuthorizationGrant> command,
        Mock<OpenIdContext> context,
        Mock<ITokenRequest> tokenRequest,
        Mock<IAuthorizationRequest> authRequest,
        Mock<IReadOnlySettingCollection> settings,
        Mock<IMediator> mediator
    ) CreateScaffold(string authRequestClientId)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockAuthRequest = CreateStrictMock<IAuthorizationRequest>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        // ErrorFactory, Settings and the two client ids are read before the first branch on every path.
        // Mediator is read only on the success path (subject validation), so that test sets it up itself.
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        mockAuthRequest.SetupGet(x => x.ClientId).Returns(authRequestClientId).Verifiable();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        var command = new ValidateTokenGrantCommand<AuthorizationGrant>(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            new AuthorizationGrant(mockAuthRequest.Object, CreateSubjectAuthentication())
        );

        return (
            command,
            mockContext,
            mockTokenRequest,
            mockAuthRequest,
            mockSettings,
            mockMediator
        );
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAuthorizationClientMismatch_ThrowsOpenIdException()
    {
        var (command, _, _, _, _, _) = CreateScaffold("different-client");

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriMissing_ThrowsOpenIdException()
    {
        var (command, _, mockTokenRequest, _, _, _) = CreateScaffold(ClientId);

        mockTokenRequest.SetupGet(x => x.RedirectUri).Returns((Uri?)null).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriMismatch_ThrowsOpenIdException()
    {
        var (command, _, mockTokenRequest, mockAuthRequest, _, _) = CreateScaffold(ClientId);

        mockTokenRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        mockAuthRequest
            .SetupGet(x => x.RedirectUri)
            .Returns(new Uri("https://client/other"))
            .Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRequestedScopesExceedGranted_ThrowsOpenIdException()
    {
        var (command, _, mockTokenRequest, mockAuthRequest, _, _) = CreateScaffold(ClientId);

        mockTokenRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        mockAuthRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        mockTokenRequest.SetupGet(x => x.Scopes).Returns(["api", "extra"]).Verifiable();
        mockAuthRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenEffectiveScopeNotSupported_ThrowsOpenIdException()
    {
        var (command, _, mockTokenRequest, mockAuthRequest, mockSettings, _) = CreateScaffold(
            ClientId
        );

        mockTokenRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        mockAuthRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockAuthRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();

        MockClientScopeService
            .Setup(x => x.GetAllowedScopesAsync(TenantId, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenGrantValidWithoutPkce_CompletesSuccessfully()
    {
        var (command, mockContext, mockTokenRequest, mockAuthRequest, mockSettings, mockMediator) =
            CreateScaffold(ClientId);

        mockTokenRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        mockAuthRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        mockTokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        mockAuthRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();

        MockClientScopeService
            .Setup(x => x.GetAllowedScopesAsync(TenantId, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "api" });

        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateSubjectAuthenticationCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        // PKCE arguments are all evaluated at the ValidatePkce call site.
        mockTokenRequest.SetupGet(x => x.CodeVerifier).Returns((string?)null).Verifiable();
        mockAuthRequest.SetupGet(x => x.CodeChallenge).Returns((string?)null).Verifiable();
        mockAuthRequest.SetupGet(x => x.CodeChallengeMethod).Returns((string?)null).Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    #endregion
}
