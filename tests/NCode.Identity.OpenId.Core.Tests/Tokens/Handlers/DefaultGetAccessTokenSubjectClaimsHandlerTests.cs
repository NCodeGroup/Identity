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
using NCode.Identity.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.Handlers;

public class DefaultGetAccessTokenSubjectClaimsHandlerTests : BaseTests
{
    private Mock<IClaimsService> MockClaimsService { get; }
    private Mock<ISubjectIdentifierService> MockSubjectIdentifierService { get; }
    private DefaultGetAccessTokenSubjectClaimsHandler Handler { get; }

    public DefaultGetAccessTokenSubjectClaimsHandlerTests()
    {
        MockClaimsService = CreateStrictMock<IClaimsService>();
        MockSubjectIdentifierService = CreateStrictMock<ISubjectIdentifierService>();
        MockSubjectIdentifierService
            .Setup(x =>
                x.ProjectSubjectId(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    It.IsAny<string>()
                )
            )
            .Returns((OpenIdContext _, OpenIdClient _, string principalId) => principalId);
        Handler = new DefaultGetAccessTokenSubjectClaimsHandler(
            MockClaimsService.Object,
            MockSubjectIdentifierService.Object
        );
    }

    #region Scaffolding

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private GetAccessTokenSubjectClaimsCommand CreateCommand(
        SubjectAuthentication? subjectAuthentication
    )
    {
        IReadOnlyList<string> scopes = ["openid"];
        var tokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdConstants.GrantTypes.AuthorizationCode,
            OriginalScopes = scopes,
            EffectiveScopes = scopes,
            SubjectAuthentication = subjectAuthentication,
        };

        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            SigningCredentials: null!,
            EncryptionCredentials: null,
            TokenType: OpenIdConstants.TokenTypes.AccessToken
        );

        return new GetAccessTokenSubjectClaimsCommand(
            CreateStrictMock<OpenIdContext>().Object,
            CreateStrictMock<OpenIdClient>().Object,
            tokenContext,
            []
        );
    }

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoSubjectAuthentication_DoesNotCopyClaims()
    {
        var command = CreateCommand(subjectAuthentication: null);

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectAuthentication_CopiesClaims()
    {
        var command = CreateCommand(CreateSubjectAuthentication());

        MockClaimsService
            .Setup(x =>
                x.CopyClaims(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<ICollection<Claim>>(),
                    It.IsAny<bool>(),
                    It.IsAny<IEnumerable<string>>()
                )
            )
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_EmitsProjectedSubjectIdAsSubClaim()
    {
        MockSubjectIdentifierService
            .Setup(x =>
                x.ProjectSubjectId(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    "subject-id"
                )
            )
            .Returns("pairwise-pseudonym")
            .Verifiable();
        MockClaimsService
            .Setup(x =>
                x.CopyClaims(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<ICollection<Claim>>(),
                    It.IsAny<bool>(),
                    It.IsAny<IEnumerable<string>>()
                )
            )
            .Verifiable();

        var command = CreateCommand(CreateSubjectAuthentication());

        await Handler.HandleAsync(command, CancellationToken.None);

        var subClaim = Assert.Single(
            command.SubjectClaims,
            claim => claim.Type == JoseClaimNames.Payload.Sub
        );
        Assert.Equal("pairwise-pseudonym", subClaim.Value);
    }

    #endregion
}
