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
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.TokenExchange;

public class DefaultTokenExchangeGrantHandlerTests : BaseTests
{
    private const string RequesterClientId = "requester";

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<ITokenService> MockTokenService { get; }
    private Mock<ISubjectTokenValidator> MockSubjectTokenValidator { get; }
    private Mock<IPrincipalResolver> MockPrincipalResolver { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IClientScopeService> MockClientScopeService { get; }
    private DefaultTokenExchangeGrantHandler Handler { get; }

    public DefaultTokenExchangeGrantHandlerTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockTokenService = CreateStrictMock<ITokenService>();
        MockSubjectTokenValidator = CreateStrictMock<ISubjectTokenValidator>();
        MockPrincipalResolver = CreateStrictMock<IPrincipalResolver>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockClientScopeService = CreateStrictMock<IClientScopeService>();
        Handler = new DefaultTokenExchangeGrantHandler(
            MockTimeProvider.Object,
            MockTokenService.Object,
            MockSubjectTokenValidator.Object,
            MockPrincipalResolver.Object,
            MockStoreManagerFactory.Object,
            MockClientScopeService.Object
        );
    }

    #region Helpers

    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private (Mock<OpenIdContext> Context, Mock<IOpenIdError> Error) SetupContextWithErrorFactory()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        var mockMediator = CreateStrictMock<IMediator>();

        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object);
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        return (mockContext, mockError);
    }

    private Mock<ITokenRequest> SetupTokenRequest(
        string? subjectToken = "subject.jwt",
        string? subjectTokenType = OpenIdConstants.TokenTypeIdentifiers.AccessToken,
        string? actorToken = null,
        string? actorTokenType = null,
        string? requestedTokenType = null
    )
    {
        var mock = CreateStrictMock<ITokenRequest>();
        mock.SetupGet(x => x.SubjectToken).Returns(subjectToken);
        mock.SetupGet(x => x.SubjectTokenType).Returns(subjectTokenType);
        mock.SetupGet(x => x.ActorToken).Returns(actorToken);
        mock.SetupGet(x => x.ActorTokenType).Returns(actorTokenType);
        mock.SetupGet(x => x.RequestedTokenType).Returns(requestedTokenType);
        return mock;
    }

    private Mock<OpenIdClient> SetupConfidentialClient()
    {
        var mock = CreateStrictMock<OpenIdClient>();
        mock.SetupGet(x => x.IsConfidential).Returns(true);
        mock.SetupGet(x => x.ClientId).Returns(RequesterClientId);
        return mock;
    }

    private void SetupSubjectValidatorSuccess(string token, string payloadJson)
    {
        MockSubjectTokenValidator
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    token,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<SubjectTokenValidationResult>(
                    SubjectTokenValidationResult.Success(
                        new ClaimsPrincipal(new ClaimsIdentity()),
                        Payload(payloadJson),
                        OpenIdConstants.TokenTypeIdentifiers.AccessToken
                    )
                )
            );
    }

    #endregion

    #region GrantTypes

    [Fact]
    public void GrantTypes_Always_ContainsTokenExchange()
    {
        Assert.Contains(OpenIdConstants.GrantTypes.TokenExchange, Handler.GrantTypes);
    }

    #endregion

    #region Request validation

    [Fact]
    public async Task HandleAsync_WhenClientNotConfidential_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.IsConfidential).Returns(false);
        var mockTokenRequest = CreateStrictMock<ITokenRequest>();

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
    public async Task HandleAsync_WhenSubjectTokenMissing_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest(subjectToken: null);

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
    public async Task HandleAsync_WhenSubjectTokenTypeMissing_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest(subjectTokenType: null);

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
    public async Task HandleAsync_WhenSubjectTokenInvalid_ReturnsValidatorError()
    {
        var (mockContext, _) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest();
        var mockValidatorError = CreateLooseMock<IOpenIdError>();

        MockSubjectTokenValidator
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    "subject.jwt",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<SubjectTokenValidationResult>(
                    SubjectTokenValidationResult.Fail(mockValidatorError.Object)
                )
            );

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockValidatorError.Object, result);
    }

    [Fact]
    public async Task HandleAsync_WhenRequesterNotEntitledToSubjectToken_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest();

        // client_id names a different client and there is no audience, so the requester is not entitled.
        SetupSubjectValidatorSuccess("subject.jwt", """{"sub":"alice","client_id":"other"}""");

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
    public async Task HandleAsync_WhenRequestedTokenTypeUnsupported_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest(
            requestedTokenType: "urn:ietf:params:oauth:token-type:saml2"
        );

        SetupSubjectValidatorSuccess(
            "subject.jwt",
            $$"""{"sub":"alice","client_id":"{{RequesterClientId}}"}"""
        );

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
    public async Task HandleAsync_WhenRefreshTokenRequestedButNotAllowed_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings.Setup(x => x.GetValue(It.IsAny<SettingKey<bool>>())).Returns(false);
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);

        var mockTokenRequest = SetupTokenRequest(
            requestedTokenType: OpenIdConstants.TokenTypeIdentifiers.RefreshToken
        );

        SetupSubjectValidatorSuccess(
            "subject.jwt",
            $$"""{"sub":"alice","client_id":"{{RequesterClientId}}"}"""
        );

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    #endregion

    #region Delegation

    [Fact]
    public async Task HandleAsync_WhenActorTokenTypeMissing_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest(actorToken: "actor.jwt", actorTokenType: null);

        SetupSubjectValidatorSuccess(
            "subject.jwt",
            $$"""{"sub":"alice","client_id":"{{RequesterClientId}}"}"""
        );

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
    public async Task HandleAsync_WhenSubjectAndActorAreSame_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest(
            actorToken: "actor.jwt",
            actorTokenType: OpenIdConstants.TokenTypeIdentifiers.AccessToken
        );

        SetupSubjectValidatorSuccess(
            "subject.jwt",
            $$"""{"sub":"alice","client_id":"{{RequesterClientId}}"}"""
        );
        SetupSubjectValidatorSuccess(
            "actor.jwt",
            $$"""{"sub":"alice","client_id":"{{RequesterClientId}}"}"""
        );

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
    public async Task HandleAsync_WhenSubjectTokenDoesNotAuthorizeActor_ReturnsError()
    {
        var (mockContext, mockError) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest(
            actorToken: "actor.jwt",
            actorTokenType: OpenIdConstants.TokenTypeIdentifiers.AccessToken
        );

        // The subject token carries no may_act claim, so the presented actor is not authorized to act for it.
        SetupSubjectValidatorSuccess(
            "subject.jwt",
            $$"""{"sub":"alice","client_id":"{{RequesterClientId}}"}"""
        );
        SetupSubjectValidatorSuccess(
            "actor.jwt",
            $$"""{"sub":"service-agent","client_id":"{{RequesterClientId}}"}"""
        );

        var result = await Handler.HandleAsync(
            mockContext.Object,
            mockClient.Object,
            mockTokenRequest.Object,
            ProofOfPossessionBinding.Unbound,
            CancellationToken.None
        );

        Assert.Same(mockError.Object, result);
    }

    #endregion

    #region Subject

    [Fact]
    public async Task HandleAsync_WhenTheSubjectTokenIsValid_ValidatesTheSubjectAsOneOfThisTenant()
    {
        var (mockContext, _) = SetupContextWithErrorFactory();
        var mockClient = SetupConfidentialClient();
        var mockTokenRequest = SetupTokenRequest();

        SetupSubjectValidatorSuccess(
            "subject.jwt",
            $$"""{"client_id":"{{RequesterClientId}}","sub":"subject-1"}"""
        );

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        mockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IStoreManager>(mockStoreManager.Object))
            .Verifiable();

        MockPrincipalResolver
            .Setup(x =>
                x.ResolvePrincipalIdOrDefaultAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<string?>("principal-1"))
            .Verifiable();

        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UnixEpoch).Verifiable();

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.SetupGet(x => x.TenantId).Returns("tenant-1").Verifiable();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext
            .Setup(x => x.NormalizeSubjectId(It.IsAny<ClaimsPrincipal>(), "principal-1"))
            .Returns((ClaimsPrincipal subject, string _) => subject)
            .Verifiable();

        ValidateTokenGrantCommand<TokenExchangeGrant>? captured = null;
        Mock.Get(mockContext.Object.Mediator)
            .Setup(x =>
                x.SendAsync(
                    It.IsAny<ValidateTokenGrantCommand<TokenExchangeGrant>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ValidateTokenGrantCommand<TokenExchangeGrant> command, CancellationToken _) =>
                    captured = command
            )
            .Returns(ValueTask.FromException(new InvalidOperationException("stop")))
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Handler.HandleAsync(
                mockContext.Object,
                mockClient.Object,
                mockTokenRequest.Object,
                ProofOfPossessionBinding.Unbound,
                CancellationToken.None
            )
        );

        Assert.NotNull(captured);
        Assert.Equal(
            "tenant-1",
            captured.Value.Grant.SubjectAuthentication.AuthenticationProperties.GetString(
                OpenIdConstants.AuthenticationPropertyItems.TenantId
            )
        );
    }

    #endregion
}
