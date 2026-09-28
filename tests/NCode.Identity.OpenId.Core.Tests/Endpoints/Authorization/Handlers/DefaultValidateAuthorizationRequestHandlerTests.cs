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
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
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

        // scopes_supported runs unconditionally; make it pass so it does not mask the check under test
        IReadOnlyList<string> emptyScopes = [];
        IReadOnlyCollection<string> scopesSupported = [];
        mockRequest.SetupGet(x => x.Scopes).Returns(emptyScopes);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ScopesSupported))
            .Returns(scopesSupported);

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

    #endregion

    #region acr_values_supported

    [Fact]
    public void ValidateSupportedValues_AcrValueNotSupported_Throws()
    {
        var (mockSettings, mockRequest) = CreateScaffold();

        IReadOnlyCollection<string> acrSupported = ["supported-acr"];
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

        IReadOnlyCollection<string> acrSupported = ["acr-1", "acr-2"];
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

        IReadOnlyCollection<string> claimsLocalesSupported = ["en-US"];
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

        IReadOnlyCollection<string> claimsLocalesSupported = ["en-US", "fr-FR"];
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

    #endregion
}
