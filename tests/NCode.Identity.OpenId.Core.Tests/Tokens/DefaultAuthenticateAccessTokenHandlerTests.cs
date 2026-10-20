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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens;

public class DefaultAuthenticateAccessTokenHandlerTests : BaseTests
{
    private const string Token = "token-value";
    private const string ManagementAudience = "urn:ncode:management";

    private Mock<IIssuedTokenValidator> MockIssuedTokenValidator { get; }
    private Mock<IProofOfPossessionService> MockProofOfPossessionService { get; }
    private DefaultAuthenticateAccessTokenHandler Handler { get; }

    public DefaultAuthenticateAccessTokenHandlerTests()
    {
        MockIssuedTokenValidator = CreateStrictMock<IIssuedTokenValidator>();
        MockProofOfPossessionService = CreateStrictMock<IProofOfPossessionService>();
        Handler = new DefaultAuthenticateAccessTokenHandler(
            NullLogger<DefaultAuthenticateAccessTokenHandler>.Instance,
            MockIssuedTokenValidator.Object,
            MockProofOfPossessionService.Object
        );
    }

    #region Scaffolding

    private AuthenticateAccessTokenCommand CreateCommand(string token)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        return new AuthenticateAccessTokenCommand(mockContext.Object, token, [ManagementAudience]);
    }

    private void SetupValidation(OpenIdContext context, JwtValidationResult result) =>
        MockIssuedTokenValidator
            .Setup(x =>
                x.ValidateAccessTokenAsync(
                    context,
                    Token,
                    It.Is<AccessTokenValidationOptions>(options =>
                        options.RequiredAudiences.SequenceEqual(new[] { ManagementAudience })
                        && options.AdditionalDecryptionKeys.Count == 0
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private static JwtValidationResult CreateInvalidResult() =>
        JwtValidationResult.Fail(
            new JwtValidationParameters(),
            PropertyBagFactory.Create(),
            Token,
            new JwtValidationException("invalid")
        );

    private static JwtValidationResult CreateValidResult(object payloadObject)
    {
        var payload = JsonSerializer.SerializeToElement(payloadObject);
        var compactJwt = new CompactJwt("JWS", default, "aaa.bbb.ccc", []);
        var decodedJwt = new DecodedJwt(compactJwt, payload, signingKey: null!);
        return JwtValidationResult.Success(
            new JwtValidationParameters(),
            PropertyBagFactory.Create(),
            decodedJwt,
            (decoded, _, authType, nameType, roleType, _) =>
            {
                var identity = new ClaimsIdentity(authType, nameType, roleType);
                foreach (var property in decoded.Payload.EnumerateObject())
                {
                    identity.AddClaim(new Claim(property.Name, property.Value.ToString()));
                }
                return ValueTask.FromResult(identity);
            }
        );
    }

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
    public async Task HandleAsync_WhenTheValidatorRejectsTheToken_ReturnsError()
    {
        var command = CreateCommand(Token);
        SetupValidation(command.OpenIdContext, CreateInvalidResult());
        SetupErrorFactory(command.OpenIdContext);

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.HasError);
        Assert.False(disposition.IsAuthenticated);
    }

    [Fact]
    public async Task HandleAsync_WhenTheValidatorAcceptsAnUnboundToken_ReturnsAuthenticatedPrincipal()
    {
        var command = CreateCommand(Token);
        var result = CreateValidResult(
            new Dictionary<string, object> { [JoseClaimNames.Payload.Sub] = "subject-1" }
        );
        SetupValidation(command.OpenIdContext, result);

        var disposition = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(disposition.IsAuthenticated);
        Assert.False(disposition.HasError);
        Assert.Equal(
            "subject-1",
            disposition.Principal.FindFirst(JoseClaimNames.Payload.Sub)?.Value
        );
    }

    #endregion
}
