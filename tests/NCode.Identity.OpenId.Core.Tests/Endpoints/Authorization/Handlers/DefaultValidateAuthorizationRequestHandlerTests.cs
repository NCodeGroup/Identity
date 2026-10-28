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

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Handlers;

public class DefaultValidateAuthorizationRequestHandlerTests : BaseTests
{
    #region Scaffolding

    private (
        Mock<IReadOnlySettingCollection> settings,
        Mock<IAuthorizationRequest> request
    ) CreateScaffold()
    {
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockRequest = CreateLooseMock<IAuthorizationRequest>();

        return (mockSettings, mockRequest);
    }

    private void InvokeValidateSupportedValues(
        Mock<IReadOnlySettingCollection> mockSettings,
        Mock<IAuthorizationRequest> mockRequest
    )
    {
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        DefaultValidateAuthorizationRequestHandler.ValidateSupportedValues(
            mockErrorFactory.Object,
            mockSettings.Object,
            mockRequest.Object
        );
    }

    private void InvokeValidateConfiguredConstraints(
        Mock<IReadOnlySettingCollection> mockSettings,
        Mock<IAuthorizationRequest> mockRequest
    )
    {
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);

        DefaultValidateAuthorizationRequestHandler.ValidateConfiguredConstraints(
            mockErrorFactory.Object,
            mockSettings.Object,
            mockRequest.Object
        );
    }

    private (
        OpenIdContext context,
        OpenIdClient client,
        Func<string?> capturedCode
    ) CreateResponseTypeScaffold(
        Mock<IReadOnlySettingCollection> mockTenantSettings,
        Mock<IReadOnlySettingCollection> mockClientSettings
    )
    {
        string? capturedCode = null;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode = code)
            .Returns(mockError.Object);

        var mockSettingsProvider = CreateLooseMock<IReadOnlySettingCollectionProvider>();
        mockSettingsProvider.SetupGet(x => x.Collection).Returns(mockTenantSettings.Object);

        var mockTenant = CreateLooseMock<OpenIdTenant>();
        mockTenant.SetupGet(x => x.SettingsProvider).Returns(mockSettingsProvider.Object);

        var mockContext = CreateLooseMock<OpenIdContext>();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);

        var mockClient = CreateLooseMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(mockClientSettings.Object);

        return (mockContext.Object, mockClient.Object, () => capturedCode);
    }

    private static void SetupSupported(
        Mock<IReadOnlySettingCollection> mockSettings,
        IReadOnlyCollection<string>? responseTypesSupported,
        IReadOnlyCollection<string>? grantTypesSupported
    )
    {
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.ResponseTypesSupported, out responseTypesSupported)
            )
            .Returns(true);
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out grantTypesSupported)
            )
            .Returns(true);
    }

    private Mock<IAuthorizationRequest> CreateRequest(params string[] responseTypes)
    {
        var mockRequest = CreateLooseMock<IAuthorizationRequest>();
        IReadOnlyList<string> values = responseTypes;
        mockRequest.SetupGet(x => x.ResponseTypes).Returns(values);
        return mockRequest;
    }

    #endregion

    #region ValidateRequestObject Tests

    private const string RequestObjectClientId = "client-1";

    private sealed record RequestObjectScaffold(
        OpenIdClient Client,
        Mock<IAuthorizationRequestMessage> Message,
        Mock<IAuthorizationRequestObject> RequestObject,
        Func<string?> CapturedCode
    );

    private RequestObjectScaffold CreateRequestObjectScaffold()
    {
        string? capturedCode = null;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode = code)
            .Returns(mockError.Object);

        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        mockEnvironment.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);

        var mockClient = CreateLooseMock<OpenIdClient>();
        mockClient.SetupGet(x => x.ClientId).Returns(RequestObjectClientId);

        var mockMessage = CreateLooseMock<IAuthorizationRequestMessage>();
        mockMessage.SetupGet(x => x.OpenIdEnvironment).Returns(mockEnvironment.Object);

        var mockRequestObject = CreateLooseMock<IAuthorizationRequestObject>();
        mockRequestObject
            .SetupGet(x => x.Parameters)
            .Returns(CreateLooseMock<IParameterCollection>().Object);
        mockRequestObject.SetupGet(x => x.ClientId).Returns(RequestObjectClientId);
        mockRequestObject.SetupGet(x => x.RedirectUri).Returns("https://client.example/callback");
        mockRequestObject
            .SetupGet(x => x.ResponseTypes)
            .Returns([OpenIdConstants.ResponseTypes.Code]);
        mockRequestObject.SetupGet(x => x.Scopes).Returns([OpenIdConstants.ScopeTypes.OpenId]);

        return new RequestObjectScaffold(
            mockClient.Object,
            mockMessage,
            mockRequestObject,
            () => capturedCode
        );
    }

    private static void InvokeValidateRequestObject(RequestObjectScaffold scaffold) =>
        DefaultValidateAuthorizationRequestHandler.ValidateRequestObject(
            scaffold.Client,
            scaffold.Message.Object,
            scaffold.RequestObject.Object
        );

    [Fact]
    public void ValidateRequestObject_WhenTheOuterRequestCarriesNoParameters_DoesNotThrow()
    {
        // RFC 9126 §3: a pushed request may carry only the request object and client authentication.
        var scaffold = CreateRequestObjectScaffold();

        InvokeValidateRequestObject(scaffold);

        Assert.Null(scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheOuterValuesMatch_DoesNotThrow()
    {
        var scaffold = CreateRequestObjectScaffold();
        scaffold.Message.SetupGet(x => x.ClientId).Returns(RequestObjectClientId);
        scaffold.Message.SetupGet(x => x.RedirectUri).Returns("https://client.example/callback");
        scaffold
            .Message.SetupGet(x => x.ResponseTypes)
            .Returns([OpenIdConstants.ResponseTypes.Code]);

        InvokeValidateRequestObject(scaffold);

        Assert.Null(scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheObjectNamesAnotherClient_ThrowsInvalidRequestObject()
    {
        var scaffold = CreateRequestObjectScaffold();
        scaffold.RequestObject.SetupGet(x => x.ClientId).Returns("client-2");

        Assert.Throws<OpenIdException>(() => InvokeValidateRequestObject(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheOuterClientIdDiffers_ThrowsInvalidRequestObject()
    {
        var scaffold = CreateRequestObjectScaffold();
        scaffold.Message.SetupGet(x => x.ClientId).Returns("client-2");

        Assert.Throws<OpenIdException>(() => InvokeValidateRequestObject(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheOuterRedirectUriDiffers_ThrowsInvalidRequestObject()
    {
        var scaffold = CreateRequestObjectScaffold();
        scaffold.Message.SetupGet(x => x.RedirectUri).Returns("https://client.example/other");

        Assert.Throws<OpenIdException>(() => InvokeValidateRequestObject(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheOuterRedirectUriDiffersOnlyInHostCase_ThrowsInvalidRequestObject()
    {
        // Uri equality ignores the case of the host, so the redirect URI is compared as written (ADR-0203).
        var scaffold = CreateRequestObjectScaffold();
        scaffold.Message.SetupGet(x => x.RedirectUri).Returns("https://CLIENT.example/callback");

        Assert.Throws<OpenIdException>(() => InvokeValidateRequestObject(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheObjectLacksTheResponseType_ThrowsInvalidRequestObject()
    {
        // An outer response_type is never used in place of the object's (RFC 9101 §6.3).
        var scaffold = CreateRequestObjectScaffold();
        scaffold.RequestObject.SetupGet(x => x.ResponseTypes).Returns((List<string>?)null);
        scaffold
            .Message.SetupGet(x => x.ResponseTypes)
            .Returns([OpenIdConstants.ResponseTypes.Code]);

        Assert.Throws<OpenIdException>(() => InvokeValidateRequestObject(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheObjectLacksTheScope_ThrowsInvalidRequestObject()
    {
        // An outer scope is never used in place of the object's (RFC 9101 §6.3).
        var scaffold = CreateRequestObjectScaffold();
        scaffold.RequestObject.SetupGet(x => x.Scopes).Returns((List<string>?)null);
        scaffold.Message.SetupGet(x => x.Scopes).Returns([OpenIdConstants.ScopeTypes.OpenId]);

        Assert.Throws<OpenIdException>(() => InvokeValidateRequestObject(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheOuterScopeDiffers_DoesNotThrow()
    {
        // OpenID Connect Core §6.1 lets the object's scope supersede the outer one, which is ignored.
        var scaffold = CreateRequestObjectScaffold();
        scaffold
            .Message.SetupGet(x => x.Scopes)
            .Returns([OpenIdConstants.ScopeTypes.OpenId, "profile"]);

        InvokeValidateRequestObject(scaffold);

        Assert.Null(scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateRequestObject_WhenTheObjectLacksTheRedirectUri_ThrowsInvalidRequestObject()
    {
        var scaffold = CreateRequestObjectScaffold();
        scaffold.RequestObject.SetupGet(x => x.RedirectUri).Returns((string?)null);

        Assert.Throws<OpenIdException>(() => InvokeValidateRequestObject(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestJwt, scaffold.CapturedCode());
    }

    #endregion

    #region ValidateResponseType Tests

    [Fact]
    public void ValidateResponseType_WhenTheWholeResponseTypeIsSupported_DoesNotThrow()
    {
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockClientSettings = CreateLooseMock<IReadOnlySettingCollection>();
        SetupSupported(
            mockTenantSettings,
            [OpenIdConstants.ResponseTypes.Code, OpenIdConstants.ResponseTypes.CodeIdToken],
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );
        SetupSupported(
            mockClientSettings,
            [OpenIdConstants.ResponseTypes.CodeIdToken],
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );
        var (context, client, _) = CreateResponseTypeScaffold(
            mockTenantSettings,
            mockClientSettings
        );
        var mockRequest = CreateRequest(
            OpenIdConstants.ResponseTypes.IdToken,
            OpenIdConstants.ResponseTypes.Code
        );

        var exception = Record.Exception(() =>
            DefaultValidateAuthorizationRequestHandler.ValidateResponseType(
                context,
                client,
                mockRequest.Object
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateResponseType_WhenOnlyItsNamesAreSupportedSeparately_ThrowsUnsupportedResponseType()
    {
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockClientSettings = CreateLooseMock<IReadOnlySettingCollection>();
        SetupSupported(
            mockTenantSettings,
            [OpenIdConstants.ResponseTypes.Code, OpenIdConstants.ResponseTypes.IdToken],
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );
        var (context, client, capturedCode) = CreateResponseTypeScaffold(
            mockTenantSettings,
            mockClientSettings
        );
        var mockRequest = CreateRequest(
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.IdToken
        );

        Assert.Throws<OpenIdException>(() =>
            DefaultValidateAuthorizationRequestHandler.ValidateResponseType(
                context,
                client,
                mockRequest.Object
            )
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.UnsupportedResponseType, capturedCode());
    }

    [Fact]
    public void ValidateResponseType_WhenTheTenantDoesNotSupportAGrantTypeItUses_ThrowsUnsupportedResponseType()
    {
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockClientSettings = CreateLooseMock<IReadOnlySettingCollection>();
        SetupSupported(
            mockTenantSettings,
            [OpenIdConstants.ResponseTypes.CodeIdToken],
            [OpenIdConstants.GrantTypes.AuthorizationCode]
        );
        var (context, client, capturedCode) = CreateResponseTypeScaffold(
            mockTenantSettings,
            mockClientSettings
        );
        var mockRequest = CreateRequest(
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.IdToken
        );

        Assert.Throws<OpenIdException>(() =>
            DefaultValidateAuthorizationRequestHandler.ValidateResponseType(
                context,
                client,
                mockRequest.Object
            )
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.UnsupportedResponseType, capturedCode());
    }

    [Fact]
    public void ValidateResponseType_WhenTheClientMayNotUseIt_ThrowsUnauthorizedClient()
    {
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockClientSettings = CreateLooseMock<IReadOnlySettingCollection>();
        SetupSupported(
            mockTenantSettings,
            [OpenIdConstants.ResponseTypes.Code, OpenIdConstants.ResponseTypes.CodeIdToken],
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );
        SetupSupported(
            mockClientSettings,
            [OpenIdConstants.ResponseTypes.Code],
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );
        var (context, client, capturedCode) = CreateResponseTypeScaffold(
            mockTenantSettings,
            mockClientSettings
        );
        var mockRequest = CreateRequest(
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.IdToken
        );

        Assert.Throws<OpenIdException>(() =>
            DefaultValidateAuthorizationRequestHandler.ValidateResponseType(
                context,
                client,
                mockRequest.Object
            )
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.UnauthorizedClient, capturedCode());
    }

    [Fact]
    public void ValidateResponseType_WhenTheClientLacksAGrantTypeItUses_ThrowsUnauthorizedClient()
    {
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockClientSettings = CreateLooseMock<IReadOnlySettingCollection>();
        SetupSupported(
            mockTenantSettings,
            [OpenIdConstants.ResponseTypes.CodeIdToken],
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );
        SetupSupported(
            mockClientSettings,
            [OpenIdConstants.ResponseTypes.CodeIdToken],
            [OpenIdConstants.GrantTypes.AuthorizationCode]
        );
        var (context, client, capturedCode) = CreateResponseTypeScaffold(
            mockTenantSettings,
            mockClientSettings
        );
        var mockRequest = CreateRequest(
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.IdToken
        );

        Assert.Throws<OpenIdException>(() =>
            DefaultValidateAuthorizationRequestHandler.ValidateResponseType(
                context,
                client,
                mockRequest.Object
            )
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.UnauthorizedClient, capturedCode());
    }

    [Fact]
    public void ValidateResponseType_WhenNoListIsSet_DoesNotThrow()
    {
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockClientSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var (context, client, _) = CreateResponseTypeScaffold(
            mockTenantSettings,
            mockClientSettings
        );
        var mockRequest = CreateRequest(
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.Token
        );

        var exception = Record.Exception(() =>
            DefaultValidateAuthorizationRequestHandler.ValidateResponseType(
                context,
                client,
                mockRequest.Object
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateResponseType_WhenTheClientListsNoResponseTypeThatReturnsAnAccessToken_ThrowsUnauthorizedClient()
    {
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();
        var mockClientSettings = CreateLooseMock<IReadOnlySettingCollection>();
        SetupSupported(
            mockTenantSettings,
            [OpenIdConstants.ResponseTypes.Code, OpenIdConstants.ResponseTypes.CodeToken],
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );
        SetupSupported(
            mockClientSettings,
            [
                OpenIdConstants.ResponseTypes.Code,
                OpenIdConstants.ResponseTypes.IdToken,
                OpenIdConstants.ResponseTypes.CodeIdToken,
            ],
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
        );
        var (context, client, capturedCode) = CreateResponseTypeScaffold(
            mockTenantSettings,
            mockClientSettings
        );
        var mockRequest = CreateRequest(
            OpenIdConstants.ResponseTypes.Token,
            OpenIdConstants.ResponseTypes.Code
        );

        Assert.Throws<OpenIdException>(() =>
            DefaultValidateAuthorizationRequestHandler.ValidateResponseType(
                context,
                client,
                mockRequest.Object
            )
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.UnauthorizedClient, capturedCode());
    }

    #endregion

    #region ValidateConfiguredConstraints Tests

    [Fact]
    public void ValidateConfiguredConstraints_WhenTheResponseTypeReturnsAnAccessToken_DoesNotThrow()
    {
        // The settings stub reads false for every boolean, so a separate "allow token responses" switch would reject
        // this request; response_types_supported alone decides it (ADR-0142).
        var (mockSettings, mockRequest) = CreateScaffold();
        IReadOnlyList<string> responseTypes =
        [
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.Token,
        ];
        mockRequest.SetupGet(x => x.ResponseTypes).Returns(responseTypes);

        var exception = Record.Exception(() =>
            InvokeValidateConfiguredConstraints(mockSettings, mockRequest)
        );

        Assert.Null(exception);
    }

    #endregion

    #region ValidateCodeChallenge Tests

    // RFC 7636 Appendix B.
    private const string CodeChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    private sealed record CodeChallengeScaffold(
        IOpenIdErrorFactory ErrorFactory,
        OpenIdClient Client,
        Mock<IAuthorizationRequest> Request,
        Func<string?> CapturedCode
    );

    private CodeChallengeScaffold CreateCodeChallengeScaffold(
        bool requirePkce,
        string registeredAuthMethod = "",
        bool nonceExemption = false
    )
    {
        string? capturedCode = null;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode = code)
            .Returns(mockError.Object);

        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RequireCodeChallenge))
            .Returns(requirePkce);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PkceNonceExemption))
            .Returns(nonceExemption);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(registeredAuthMethod);
        IReadOnlyCollection<string> methods = [OpenIdConstants.CodeChallengeMethods.S256];
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.CodeChallengeMethodsSupported))
            .Returns(methods);

        var mockClient = CreateLooseMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object);

        var mockRequest = CreateRequest(OpenIdConstants.ResponseTypes.Code);
        IReadOnlyList<string> scopes = [OpenIdConstants.ScopeTypes.OpenId];
        mockRequest.SetupGet(x => x.Scopes).Returns(scopes);

        return new CodeChallengeScaffold(
            mockErrorFactory.Object,
            mockClient.Object,
            mockRequest,
            () => capturedCode
        );
    }

    private static void InvokeValidateCodeChallenge(CodeChallengeScaffold scaffold) =>
        DefaultValidateAuthorizationRequestHandler.ValidateCodeChallenge(
            scaffold.ErrorFactory,
            scaffold.Client,
            scaffold.Request.Object
        );

    [Fact]
    public void ValidateCodeChallenge_WhenRequiredAndMissing_ThrowsInvalidRequest()
    {
        // RFC 7636 §4.4.1: a missing code_challenge is invalid_request, not unauthorized_client.
        var scaffold = CreateCodeChallengeScaffold(requirePkce: true);

        Assert.Throws<OpenIdException>(() => InvokeValidateCodeChallenge(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateCodeChallenge_WhenTheResponseTypeIssuesNoCode_DoesNotRequireAChallenge()
    {
        // PKCE binds an authorization code; an implicit response type issues none.
        var scaffold = CreateCodeChallengeScaffold(requirePkce: true);
        IReadOnlyList<string> responseTypes = [OpenIdConstants.ResponseTypes.IdToken];
        scaffold.Request.SetupGet(x => x.ResponseTypes).Returns(responseTypes);

        var exception = Record.Exception(() => InvokeValidateCodeChallenge(scaffold));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCodeChallenge_WhenExemptedByTheNonce_DoesNotRequireAChallenge()
    {
        var scaffold = CreateCodeChallengeScaffold(
            requirePkce: false,
            registeredAuthMethod: OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            nonceExemption: true
        );
        scaffold.Request.SetupGet(x => x.Nonce).Returns("n-0S6_WzA2Mj");

        var exception = Record.Exception(() => InvokeValidateCodeChallenge(scaffold));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateCodeChallenge_WhenAPublicClientOmitsIt_ThrowsInvalidRequest()
    {
        // A public client is bound to PKCE however the deployment relaxes require_pkce.
        var scaffold = CreateCodeChallengeScaffold(
            requirePkce: false,
            registeredAuthMethod: OpenIdConstants.ClientAuthenticationMethods.None,
            nonceExemption: true
        );
        scaffold.Request.SetupGet(x => x.Nonce).Returns("n-0S6_WzA2Mj");

        Assert.Throws<OpenIdException>(() => InvokeValidateCodeChallenge(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, scaffold.CapturedCode());
    }

    [Fact]
    public void ValidateCodeChallenge_WhenWellFormedWithASupportedMethod_DoesNotThrow()
    {
        var scaffold = CreateCodeChallengeScaffold(requirePkce: true);
        scaffold.Request.SetupGet(x => x.CodeChallenge).Returns(CodeChallenge);
        scaffold
            .Request.SetupGet(x => x.CodeChallengeMethod)
            .Returns(OpenIdConstants.CodeChallengeMethods.S256);

        var exception = Record.Exception(() => InvokeValidateCodeChallenge(scaffold));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateCodeChallenge_WhenTheMethodIsMissing_ThrowsInvalidRequest(string? method)
    {
        // OAuth 2.1 §4.1.1: code_challenge_method has no default; RFC 7636's implicit 'plain' is not applied.
        var scaffold = CreateCodeChallengeScaffold(requirePkce: true);
        scaffold.Request.SetupGet(x => x.CodeChallenge).Returns(CodeChallenge);
        scaffold.Request.SetupGet(x => x.CodeChallengeMethod).Returns(method);

        Assert.Throws<OpenIdException>(() => InvokeValidateCodeChallenge(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, scaffold.CapturedCode());
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("s256")]
    public void ValidateCodeChallenge_WhenTheMethodIsNotSupported_ThrowsInvalidRequest(
        string method
    )
    {
        var scaffold = CreateCodeChallengeScaffold(requirePkce: true);
        scaffold.Request.SetupGet(x => x.CodeChallenge).Returns(CodeChallenge);
        scaffold.Request.SetupGet(x => x.CodeChallengeMethod).Returns(method);

        Assert.Throws<OpenIdException>(() => InvokeValidateCodeChallenge(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, scaffold.CapturedCode());
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM")]
    public void ValidateCodeChallenge_WhenMalformed_ThrowsInvalidRequest(string codeChallenge)
    {
        var scaffold = CreateCodeChallengeScaffold(requirePkce: true);
        scaffold.Request.SetupGet(x => x.CodeChallenge).Returns(codeChallenge);

        Assert.Throws<OpenIdException>(() => InvokeValidateCodeChallenge(scaffold));

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, scaffold.CapturedCode());
    }

    #endregion

    #region dpop_jkt

    [Fact]
    public void ValidateConfiguredConstraints_WhenDPoPJwkThumbprintMalformed_Throws()
    {
        var (mockSettings, mockRequest) = CreateScaffold();
        mockRequest.SetupGet(x => x.DPoPJwkThumbprint).Returns("not-a-valid-thumbprint");

        Assert.Throws<OpenIdException>(() =>
            InvokeValidateConfiguredConstraints(mockSettings, mockRequest)
        );
    }

    [Fact]
    public void ValidateConfiguredConstraints_WhenDPoPJwkThumbprintValid_DoesNotThrow()
    {
        var (mockSettings, mockRequest) = CreateScaffold();
        var thumbprint = NCode.Encoders.Base64Url.Encode(new byte[32]);
        mockRequest.SetupGet(x => x.DPoPJwkThumbprint).Returns(thumbprint);

        var exception = Record.Exception(() =>
            InvokeValidateConfiguredConstraints(mockSettings, mockRequest)
        );
        Assert.Null(exception);
    }

    #endregion

    #region acr_values_supported

    [Fact]
    public void ValidateSupportedValues_AcrValueNotSupported_Throws()
    {
        var (mockSettings, mockRequest) = CreateScaffold();

        IReadOnlyCollection<string>? acrSupported = ["supported-acr"];
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.AcrValuesSupported, out acrSupported))
            .Returns(true);

        IReadOnlyList<string> acrValues = ["unsupported-acr"];
        mockRequest.SetupGet(x => x.AcrValues).Returns(acrValues);

        Assert.Throws<OpenIdException>(() =>
            InvokeValidateSupportedValues(mockSettings, mockRequest)
        );
    }

    [Fact]
    public void ValidateSupportedValues_AllAcrValuesSupported_DoesNotThrow()
    {
        var (mockSettings, mockRequest) = CreateScaffold();

        IReadOnlyCollection<string>? acrSupported = ["acr-1", "acr-2"];
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.AcrValuesSupported, out acrSupported))
            .Returns(true);

        IReadOnlyList<string> acrValues = ["acr-1"];
        mockRequest.SetupGet(x => x.AcrValues).Returns(acrValues);

        var exception = Record.Exception(() =>
            InvokeValidateSupportedValues(mockSettings, mockRequest)
        );
        Assert.Null(exception);
    }

    #endregion

    #region claims_locales_supported

    [Fact]
    public void ValidateSupportedValues_ClaimsLocaleNotSupported_Throws()
    {
        var (mockSettings, mockRequest) = CreateScaffold();

        IReadOnlyCollection<string>? claimsLocalesSupported = ["en-US"];
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.ClaimsLocalesSupported, out claimsLocalesSupported)
            )
            .Returns(true);

        IReadOnlyList<string> claimsLocales = ["fr-FR"];
        mockRequest.SetupGet(x => x.ClaimsLocales).Returns(claimsLocales);

        Assert.Throws<OpenIdException>(() =>
            InvokeValidateSupportedValues(mockSettings, mockRequest)
        );
    }

    [Fact]
    public void ValidateSupportedValues_AllClaimsLocalesSupported_DoesNotThrow()
    {
        var (mockSettings, mockRequest) = CreateScaffold();

        IReadOnlyCollection<string>? claimsLocalesSupported = ["en-US", "fr-FR"];
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.ClaimsLocalesSupported, out claimsLocalesSupported)
            )
            .Returns(true);

        IReadOnlyList<string> claimsLocales = ["en-US"];
        mockRequest.SetupGet(x => x.ClaimsLocales).Returns(claimsLocales);

        var exception = Record.Exception(() =>
            InvokeValidateSupportedValues(mockSettings, mockRequest)
        );
        Assert.Null(exception);
    }

    #endregion

    #region claims_parameter_supported

    [Fact]
    public void ValidateSupportedValues_IdTokenClaimsRequestedButNotSupported_Throws()
    {
        // Guards the operator-precedence fix: UserInfo present-but-empty must not hide IdToken claims.
        var (mockSettings, mockRequest) = CreateScaffold();

        var claimsParameterSupported = false;
        mockSettings
            .Setup(x =>
                x.TryGetValue(
                    OpenIdSettingKeys.ClaimsParameterSupported,
                    out claimsParameterSupported
                )
            )
            .Returns(true);

        var mockClaims = CreateLooseMock<IRequestClaims>();
        IReadOnlyDictionary<string, IRequestClaim?> userInfo =
            new Dictionary<string, IRequestClaim?>();
        IReadOnlyDictionary<string, IRequestClaim?> idToken = new Dictionary<string, IRequestClaim?>
        {
            ["sub"] = null,
        };
        mockClaims.SetupGet(x => x.UserInfo).Returns(userInfo);
        mockClaims.SetupGet(x => x.IdToken).Returns(idToken);
        mockRequest.SetupGet(x => x.Claims).Returns(mockClaims.Object);

        Assert.Throws<OpenIdException>(() =>
            InvokeValidateSupportedValues(mockSettings, mockRequest)
        );
    }

    [Fact]
    public void ValidateSupportedValues_NoClaimsRequested_DoesNotThrow()
    {
        var (mockSettings, mockRequest) = CreateScaffold();

        var claimsParameterSupported = false;
        mockSettings
            .Setup(x =>
                x.TryGetValue(
                    OpenIdSettingKeys.ClaimsParameterSupported,
                    out claimsParameterSupported
                )
            )
            .Returns(true);

        mockRequest.SetupGet(x => x.Claims).Returns((IRequestClaims?)null);

        var exception = Record.Exception(() =>
            InvokeValidateSupportedValues(mockSettings, mockRequest)
        );
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateSupportedValues_ClaimsRequestedAndSupported_DoesNotThrow()
    {
        var (mockSettings, mockRequest) = CreateScaffold();

        var claimsParameterSupported = true;
        mockSettings
            .Setup(x =>
                x.TryGetValue(
                    OpenIdSettingKeys.ClaimsParameterSupported,
                    out claimsParameterSupported
                )
            )
            .Returns(true);

        var mockClaims = CreateLooseMock<IRequestClaims>();
        IReadOnlyDictionary<string, IRequestClaim?> userInfo = new Dictionary<
            string,
            IRequestClaim?
        >
        {
            ["email"] = null,
        };
        IReadOnlyDictionary<string, IRequestClaim?> idToken = new Dictionary<string, IRequestClaim?>
        {
            ["sub"] = null,
        };
        mockClaims.SetupGet(x => x.UserInfo).Returns(userInfo);
        mockClaims.SetupGet(x => x.IdToken).Returns(idToken);
        mockRequest.SetupGet(x => x.Claims).Returns(mockClaims.Object);

        var exception = Record.Exception(() =>
            InvokeValidateSupportedValues(mockSettings, mockRequest)
        );
        Assert.Null(exception);
    }

    #endregion
}
