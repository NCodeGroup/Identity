// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.Server.DevelopmentEnvironment;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DeveloperInteractiveSubjectClaimsHandlerTests : BaseTests
{
    private DeveloperInteractiveSubjectClaimsHandler CreateHandler(bool expectCopy)
    {
        var mockClaimsService = CreateStrictMock<IClaimsService>();
        if (expectCopy)
        {
            // Emulate the real copy semantics: only the requested claim types flow into the target.
            mockClaimsService
                .Setup(x =>
                    x.CopyClaims(
                        It.IsAny<ClaimsPrincipal>(),
                        It.IsAny<ICollection<Claim>>(),
                        false,
                        It.IsAny<IEnumerable<string>>()
                    )
                )
                .Callback(
                    (
                        ClaimsPrincipal source,
                        ICollection<Claim> target,
                        bool _,
                        IEnumerable<string> claimTypes
                    ) =>
                    {
                        var types = new HashSet<string>(claimTypes, StringComparer.Ordinal);
                        foreach (var claim in source.Claims)
                        {
                            if (types.Contains(claim.Type))
                            {
                                target.Add(claim);
                            }
                        }
                    }
                )
                .Verifiable();
        }

        return new DeveloperInteractiveSubjectClaimsHandler(mockClaimsService.Object);
    }

    private GetAccessTokenSubjectClaimsCommand CreateCommand(
        ClaimsPrincipal? subject,
        ICollection<Claim> subjectClaims
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();

        var tokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = DateTimeOffset.UtcNow,
            GrantType = "authorization_code",
            OriginalScopes = [],
            EffectiveScopes = [],
            SubjectAuthentication = subject is null
                ? null
                : new SubjectAuthentication(
                    "test-scheme",
                    new AuthenticationProperties(),
                    subject,
                    "subject-id"
                ),
        };

        // The signing credentials are never read by this handler; only the request's subject is.
        var tokenContext = new SecurityTokenContext(tokenRequest, null!, null, "access_token");

        return new GetAccessTokenSubjectClaimsCommand(
            mockContext.Object,
            mockClient.Object,
            tokenContext,
            subjectClaims
        );
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoSubjectAuthentication_AddsNothing()
    {
        var handler = CreateHandler(expectCopy: false);
        var subjectClaims = new List<Claim>();
        var command = CreateCommand(subject: null, subjectClaims);

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(subjectClaims);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectHasRole_ProjectsRoleClaim()
    {
        var handler = CreateHandler(expectCopy: true);
        var subject = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim("sub", "subject-id"),
                    new Claim(JoseClaimNames.Payload.Role, BuiltInRoles.GlobalAdmin),
                ],
                "test"
            )
        );
        var subjectClaims = new List<Claim>();
        var command = CreateCommand(subject, subjectClaims);

        await handler.HandleAsync(command, CancellationToken.None);

        var role = Assert.Single(subjectClaims);
        Assert.Equal(JoseClaimNames.Payload.Role, role.Type);
        Assert.Equal(BuiltInRoles.GlobalAdmin, role.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectHasNoRole_AddsNothing()
    {
        var handler = CreateHandler(expectCopy: true);
        var subject = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("sub", "subject-id")], "test")
        );
        var subjectClaims = new List<Claim>();
        var command = CreateCommand(subject, subjectClaims);

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(subjectClaims);
    }

    #endregion
}
