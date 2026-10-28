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
using NCode.Identity.Jose;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.TokenExchange;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.TokenExchange;

public class DefaultSubjectTokenValidatorTests : BaseTests
{
    private const string Token = "subject-token";
    private const string Subject = "subject-1";

    private Mock<IIssuedTokenValidator> MockIssuedTokenValidator { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdClient> MockClient { get; }
    private Mock<IOpenIdError> MockError { get; }
    private DefaultSubjectTokenValidator Validator { get; }

    public DefaultSubjectTokenValidatorTests()
    {
        MockIssuedTokenValidator = CreateStrictMock<IIssuedTokenValidator>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockClient = CreateStrictMock<OpenIdClient>();
        MockError = CreateLooseMock<IOpenIdError>();
        SetupErrorFactory();

        Validator = new DefaultSubjectTokenValidator(MockIssuedTokenValidator.Object);
    }

    #region Scaffolding

    // The validator reads the error factory on every path.
    private void SetupErrorFactory()
    {
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(MockError.Object);
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }

    private static JwtValidationResult ValidResult() =>
        TestJwtValidationResults.Valid(
            new Dictionary<string, object> { [JoseClaimNames.Payload.Sub] = Subject }
        );

    private void SetupAccessTokenValidation(JwtValidationResult result) =>
        MockIssuedTokenValidator
            .Setup(x =>
                x.ValidateAccessTokenAsync(
                    MockContext.Object,
                    Token,
                    It.Is<AccessTokenValidationOptions>(options =>
                        options.RequiredAudiences.Count == 0
                        && options.AdditionalDecryptionKeys.Count == 0
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupIdTokenValidation(JwtValidationResult result) =>
        MockIssuedTokenValidator
            .Setup(x =>
                x.ValidateIdTokenAsync(MockContext.Object, Token, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(result)
            .Verifiable();

    private async Task<SubjectTokenValidationResult> ValidateAsync(string tokenTypeIdentifier) =>
        await Validator.ValidateAsync(
            MockContext.Object,
            MockClient.Object,
            Token,
            tokenTypeIdentifier,
            CancellationToken.None
        );

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WhenTokenTypeNotJwt_ReturnsFailWithoutValidatingJwt()
    {
        var result = await ValidateAsync("urn:ietf:params:oauth:token-type:saml2");

        Assert.False(result.IsValid);
        Assert.Same(MockError.Object, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnAccessTokenIsValid_ReturnsItsPrincipal()
    {
        SetupAccessTokenValidation(ValidResult());

        var result = await ValidateAsync(OpenIdProtocol.TokenTypeIdentifiers.AccessToken);

        Assert.True(result.IsValid);
        Assert.Equal(Subject, result.Principal?.FindFirst(JoseClaimNames.Payload.Sub)?.Value);
        Assert.Equal(OpenIdProtocol.TokenTypeIdentifiers.AccessToken, result.TokenTypeIdentifier);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnAccessTokenIsInvalid_FailsWithoutTryingTheIdTokenPolicy()
    {
        SetupAccessTokenValidation(TestJwtValidationResults.Invalid(Token));

        var result = await ValidateAsync(OpenIdProtocol.TokenTypeIdentifiers.AccessToken);

        Assert.False(result.IsValid);
        Assert.Same(MockError.Object, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnIdTokenIsValid_ReturnsItsPrincipal()
    {
        SetupIdTokenValidation(ValidResult());

        var result = await ValidateAsync(OpenIdProtocol.TokenTypeIdentifiers.IdToken);

        Assert.True(result.IsValid);
        Assert.Equal(Subject, result.Principal?.FindFirst(JoseClaimNames.Payload.Sub)?.Value);
    }

    [Fact]
    public async Task ValidateAsync_WhenAJwtIsAnAccessToken_DoesNotTryTheIdTokenPolicy()
    {
        SetupAccessTokenValidation(ValidResult());

        var result = await ValidateAsync(OpenIdProtocol.TokenTypeIdentifiers.Jwt);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAJwtIsAnIdToken_AcceptsIt()
    {
        SetupAccessTokenValidation(TestJwtValidationResults.Invalid(Token));
        SetupIdTokenValidation(ValidResult());

        var result = await ValidateAsync(OpenIdProtocol.TokenTypeIdentifiers.Jwt);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenAJwtIsNeitherAnAccessTokenNorAnIdToken_Fails()
    {
        SetupAccessTokenValidation(TestJwtValidationResults.Invalid(Token));
        SetupIdTokenValidation(TestJwtValidationResults.Invalid(Token));

        var result = await ValidateAsync(OpenIdProtocol.TokenTypeIdentifiers.Jwt);

        Assert.False(result.IsValid);
        Assert.Same(MockError.Object, result.Error);
    }

    #endregion
}
