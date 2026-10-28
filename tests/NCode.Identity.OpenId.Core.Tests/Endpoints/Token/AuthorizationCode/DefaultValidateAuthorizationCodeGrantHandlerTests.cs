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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.AuthorizationCode;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Pkce;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.AuthorizationCode;

public class DefaultValidateAuthorizationCodeGrantHandlerTests : BaseTests
{
    private const string ClientId = "client-1";
    private const string TenantId = "tenant-1";
    private const string Nonce = "n-0S6_WzA2Mj";

    // RFC 7636 Appendix B.
    private const string CodeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string CodeChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private const string RedirectUri = "https://client/callback";

    private Mock<IClientScopeService> MockClientScopeService { get; }
    private Mock<ICodeChallengeMethodCollectionProvider> MockCodeChallengeMethodProvider { get; }
    private DefaultValidateAuthorizationCodeGrantHandler Handler { get; }

    public DefaultValidateAuthorizationCodeGrantHandlerTests()
    {
        MockClientScopeService = CreateStrictMock<IClientScopeService>();
        MockCodeChallengeMethodProvider =
            CreateStrictMock<ICodeChallengeMethodCollectionProvider>();
        Handler = new DefaultValidateAuthorizationCodeGrantHandler(
            MockClientScopeService.Object,
            MockCodeChallengeMethodProvider.Object
        );
    }

    #region Scaffolding

    private sealed record Scaffold(
        ValidateTokenGrantCommand<AuthorizationGrant> Command,
        Mock<OpenIdContext> Context,
        Mock<OpenIdClient> Client,
        Mock<ITokenRequest> TokenRequest,
        Mock<IAuthorizationRequest> AuthRequest,
        Mock<IReadOnlySettingCollection> Settings,
        Func<string?> CapturedErrorCode
    );

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private Scaffold CreateScaffold(string authRequestClientId)
    {
        string? capturedErrorCode = null;
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();
        var mockAuthRequest = CreateStrictMock<IAuthorizationRequest>();
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        // ErrorFactory and the two client ids are read before the first branch on every path; the client settings are
        // read only on the PKCE path, so the tests that reach it set them up (CreatePkceScaffold).
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        mockClient.SetupGet(x => x.ClientId).Returns(ClientId).Verifiable();
        mockAuthRequest.SetupGet(x => x.ClientId).Returns(authRequestClientId).Verifiable();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedErrorCode = code)
            .Returns(mockError.Object);

        var command = new ValidateTokenGrantCommand<AuthorizationGrant>(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            new AuthorizationGrant(mockAuthRequest.Object, CreateSubjectAuthentication())
        );

        return new Scaffold(
            command,
            mockContext,
            mockClient,
            mockTokenRequest,
            mockAuthRequest,
            mockSettings,
            () => capturedErrorCode
        );
    }

    /// <summary>
    /// Creates a scaffold whose request passes every check before PKCE: the client, redirect URI, scopes
    /// (<paramref name="authorizedScopes"/>, or <paramref name="requestedScopes"/> at the token endpoint), subject, and
    /// resources.
    /// </summary>
    private Scaffold CreatePkceScaffold(
        string[] authorizedScopes,
        List<string>? requestedScopes = null
    )
    {
        var scaffold = CreateScaffold(ClientId);
        var mockMediator = CreateStrictMock<IMediator>();

        scaffold.TokenRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        scaffold.TokenRequest.SetupGet(x => x.Scopes).Returns(requestedScopes).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.Scopes).Returns(authorizedScopes).Verifiable();

        MockClientScopeService
            .Setup(x => x.GetAllowedScopesAsync(TenantId, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(authorizedScopes)
            .Verifiable();

        scaffold.Context.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();
        mockMediator
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateSubjectAuthenticationCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        // RFC 8707 resource indicators: neither the token request nor the authorization request named a resource.
        scaffold.TokenRequest.SetupGet(x => x.Resources).Returns((List<string>?)null).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.Resources).Returns([]).Verifiable();

        return scaffold;
    }

    private static void SetupUnboundCode(Scaffold scaffold, string? codeVerifier = null)
    {
        scaffold.TokenRequest.SetupGet(x => x.CodeVerifier).Returns(codeVerifier).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.CodeChallenge).Returns((string?)null).Verifiable();
    }

    private static void SetupBoundCode(Scaffold scaffold, string? codeVerifier)
    {
        scaffold.TokenRequest.SetupGet(x => x.CodeVerifier).Returns(codeVerifier).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.CodeChallenge).Returns(CodeChallenge).Verifiable();
    }

    private void SetupS256Method(Scaffold scaffold)
    {
        var mockMethod = CreateStrictMock<ICodeChallengeMethod>();
        mockMethod
            .Setup(x => x.ComputeChallenge(It.IsAny<string>()))
            .Returns(
                (string verifier) =>
                    verifier == CodeVerifier ? CodeChallenge : "a-different-challenge"
            )
            .Verifiable();

        var codeChallengeMethod = mockMethod.Object;
        scaffold
            .AuthRequest.SetupGet(x => x.CodeChallengeMethod)
            .Returns(OpenIdProtocol.CodeChallengeMethods.S256)
            .Verifiable();
        MockCodeChallengeMethodProvider
            .Setup(x => x.TryGet(OpenIdProtocol.CodeChallengeMethods.S256, out codeChallengeMethod))
            .Returns(true)
            .Verifiable();
    }

    /// <summary>
    /// Sets up the client as the nonce exemption sees it: <c>require_pkce</c> relaxed, the client attested by
    /// <c>pkce_nonce_exemption</c>, authenticated as <paramref name="isConfidential"/>, and an authorization request
    /// that carried a nonce.
    /// </summary>
    private static void SetupNonceExemption(Scaffold scaffold, bool isConfidential)
    {
        scaffold.Client.SetupGet(x => x.Settings).Returns(scaffold.Settings.Object).Verifiable();
        scaffold.Client.SetupGet(x => x.IsConfidential).Returns(isConfidential).Verifiable();
        scaffold
            .Settings.Setup(x => x.GetValue(OpenIdSettingKeys.RequireCodeChallenge))
            .Returns(false);
        scaffold
            .Settings.Setup(x => x.GetValue(OpenIdSettingKeys.PkceNonceExemption))
            .Returns(true);
        scaffold.AuthRequest.SetupGet(x => x.Nonce).Returns(Nonce).Verifiable();
    }

    private async Task AssertInvalidGrantAsync(Scaffold scaffold)
    {
        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidGrant, scaffold.CapturedErrorCode());
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAuthorizationClientMismatch_ThrowsOpenIdException()
    {
        var scaffold = CreateScaffold("different-client");

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriMissing_ThrowsOpenIdException()
    {
        var scaffold = CreateScaffold(ClientId);

        scaffold.TokenRequest.SetupGet(x => x.RedirectUri).Returns((string?)null).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriIsEmpty_ThrowsOpenIdException()
    {
        var scaffold = CreateScaffold(ClientId);

        scaffold.TokenRequest.SetupGet(x => x.RedirectUri).Returns(string.Empty).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRedirectUriMismatch_ThrowsOpenIdException()
    {
        var scaffold = CreateScaffold(ClientId);

        scaffold.TokenRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        scaffold
            .AuthRequest.SetupGet(x => x.RedirectUri)
            .Returns("https://client/other")
            .Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheRedirectUriDiffersOnlyInHostCase_ThrowsOpenIdException()
    {
        var scaffold = CreateScaffold(ClientId);

        scaffold
            .TokenRequest.SetupGet(x => x.RedirectUri)
            .Returns("https://CLIENT/callback")
            .Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRequestedScopesExceedGranted_ThrowsOpenIdException()
    {
        var scaffold = CreateScaffold(ClientId);

        scaffold.TokenRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        scaffold.TokenRequest.SetupGet(x => x.Scopes).Returns(["api", "extra"]).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenEffectiveScopeNotSupported_ThrowsOpenIdException()
    {
        var scaffold = CreateScaffold(ClientId);

        scaffold.TokenRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.RedirectUri).Returns(RedirectUri).Verifiable();
        scaffold.TokenRequest.SetupGet(x => x.Scopes).Returns((List<string>?)null).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.Scopes).Returns(["api"]).Verifiable();

        MockClientScopeService
            .Setup(x => x.GetAllowedScopesAsync(TenantId, ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>())
            .Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRequestedResourceNotAuthorized_ThrowsOpenIdException()
    {
        var scaffold = CreatePkceScaffold(["api"]);

        // RFC 8707: the token request names a resource that was not authorized at the authorization endpoint.
        scaffold.TokenRequest.SetupGet(x => x.Resources).Returns(["urn:other"]).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.Resources).Returns(["urn:api"]).Verifiable();

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await Handler.HandleAsync(scaffold.Command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task HandleAsync_WhenBoundCodeAndMatchingVerifier_CompletesSuccessfully()
    {
        var scaffold = CreatePkceScaffold(["api"]);
        SetupBoundCode(scaffold, CodeVerifier);
        SetupS256Method(scaffold);

        await Handler.HandleAsync(scaffold.Command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenBoundCodeAndMismatchedVerifier_ThrowsInvalidGrant()
    {
        const string otherVerifier = "OTHERJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var scaffold = CreatePkceScaffold(["api"]);
        SetupBoundCode(scaffold, otherVerifier);
        SetupS256Method(scaffold);

        await AssertInvalidGrantAsync(scaffold);
    }

    [Fact]
    public async Task HandleAsync_WhenBoundCodeAndNoVerifier_ThrowsInvalidGrant()
    {
        var scaffold = CreatePkceScaffold(["api"]);
        SetupBoundCode(scaffold, codeVerifier: null);

        await AssertInvalidGrantAsync(scaffold);
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEj+k")]
    public async Task HandleAsync_WhenBoundCodeAndMalformedVerifier_ThrowsInvalidGrant(
        string codeVerifier
    )
    {
        var scaffold = CreatePkceScaffold(["api"]);
        SetupBoundCode(scaffold, codeVerifier);

        await AssertInvalidGrantAsync(scaffold);
    }

    [Fact]
    public async Task HandleAsync_WhenBoundCodeMethodIsNotRegistered_ThrowsInvalidGrant()
    {
        ICodeChallengeMethod? noMethod = null;
        var scaffold = CreatePkceScaffold(["api"]);
        SetupBoundCode(scaffold, CodeVerifier);
        scaffold.AuthRequest.SetupGet(x => x.CodeChallengeMethod).Returns("plain").Verifiable();
        MockCodeChallengeMethodProvider
            .Setup(x => x.TryGet("plain", out noMethod))
            .Returns(false)
            .Verifiable();

        await AssertInvalidGrantAsync(scaffold);
    }

    [Fact]
    public async Task HandleAsync_WhenUnboundCodeAndVerifierPresented_ThrowsInvalidGrant()
    {
        // PKCE downgrade (RFC 9700 §4.8.2): a verifier is accepted only for a code bound to a challenge.
        var scaffold = CreatePkceScaffold(["api"]);
        SetupUnboundCode(scaffold, CodeVerifier);

        await AssertInvalidGrantAsync(scaffold);
    }

    [Fact]
    public async Task HandleAsync_WhenUnboundCodeAndPkceRequired_ThrowsInvalidGrant()
    {
        var scaffold = CreatePkceScaffold(["openid"]);
        SetupUnboundCode(scaffold);
        scaffold.Client.SetupGet(x => x.Settings).Returns(scaffold.Settings.Object).Verifiable();
        scaffold.Client.SetupGet(x => x.IsConfidential).Returns(true).Verifiable();
        scaffold.AuthRequest.SetupGet(x => x.Nonce).Returns(Nonce).Verifiable();
        scaffold
            .Settings.Setup(x => x.GetValue(OpenIdSettingKeys.RequireCodeChallenge))
            .Returns(true);

        await AssertInvalidGrantAsync(scaffold);
    }

    [Fact]
    public async Task HandleAsync_WhenUnboundCodeIsExemptedByTheNonce_CompletesSuccessfully()
    {
        var scaffold = CreatePkceScaffold([OpenIdProtocol.ScopeTypes.OpenId]);
        SetupUnboundCode(scaffold);
        SetupNonceExemption(scaffold, isConfidential: true);

        await Handler.HandleAsync(scaffold.Command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenUnboundCodeIsRedeemedByAPublicClient_ThrowsInvalidGrant()
    {
        // The exemption covers only a client that authenticates at the token endpoint.
        var scaffold = CreatePkceScaffold([OpenIdProtocol.ScopeTypes.OpenId]);
        SetupUnboundCode(scaffold);
        SetupNonceExemption(scaffold, isConfidential: false);

        await AssertInvalidGrantAsync(scaffold);
    }

    [Fact]
    public async Task HandleAsync_WhenUnboundCodeTokenRequestDropsOpenId_ThrowsInvalidGrant()
    {
        // Without 'openid' no ID Token carries the nonce back, so the nonce cannot protect the code.
        var scaffold = CreatePkceScaffold(
            [OpenIdProtocol.ScopeTypes.OpenId, "api"],
            requestedScopes: ["api"]
        );
        SetupUnboundCode(scaffold);
        SetupNonceExemption(scaffold, isConfidential: true);

        await AssertInvalidGrantAsync(scaffold);
    }

    #endregion
}
