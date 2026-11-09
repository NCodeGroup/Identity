// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        Handler = new DefaultGetAccessTokenSubjectClaimsHandler(
            MockClaimsService.Object,
            MockSubjectIdentifierService.Object
        );
    }

    #region Scaffolding

    // A subject's principal id is projected unchanged unless a test projects it.
    private void SetupSubjectProjection() =>
        MockSubjectIdentifierService
            .Setup(x =>
                x.ProjectSubjectId(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<OpenIdClient>(),
                    It.IsAny<string>()
                )
            )
            .Returns((OpenIdContext _, OpenIdClient _, string principalId) => principalId)
            .Verifiable();

    private static DateTimeOffset CreatedWhen { get; } =
        DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture);

    private GetAccessTokenSubjectClaimsCommand CreateCommand(
        SubjectAuthentication? subjectAuthentication,
        OpenIdClient? openIdClient = null
    )
    {
        IReadOnlyList<string> scopes = ["openid"];
        var tokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdProtocol.GrantTypes.AuthorizationCode,
            OriginalScopes = scopes,
            EffectiveScopes = scopes,
            SubjectAuthentication = subjectAuthentication,
        };

        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            SigningCredentials: null!,
            EncryptionCredentials: null,
            TokenType: OpenIdProtocol.TokenTypes.AccessToken
        );

        return new GetAccessTokenSubjectClaimsCommand(
            CreateStrictMock<OpenIdContext>().Object,
            openIdClient ?? CreateStrictMock<OpenIdClient>().Object,
            tokenContext,
            []
        );
    }

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoSubjectAuthentication_EmitsOnlyTheClientsPrincipalAsSubject()
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.Setup(x => x.PrincipalId).Returns("client-principal-id").Verifiable();
        var command = CreateCommand(subjectAuthentication: null, mockClient.Object);

        await Handler.HandleAsync(command, CancellationToken.None);

        var claim = Assert.Single(command.SubjectClaims);
        Assert.Equal(JoseClaimNames.Payload.Sub, claim.Type);
        Assert.Equal("client-principal-id", claim.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectAuthentication_CopiesClaims()
    {
        SetupSubjectProjection();
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
