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
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.Handlers;

public class DefaultGetIdTokenSubjectClaimsHandlerTests : BaseTests
{
    private Mock<IClaimsService> MockClaimsService { get; }
    private Mock<ISubjectIdentifierService> MockSubjectIdentifierService { get; }
    private DefaultGetIdTokenSubjectClaimsHandler Handler { get; }

    public DefaultGetIdTokenSubjectClaimsHandlerTests()
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
        Handler = new DefaultGetIdTokenSubjectClaimsHandler(
            MockClaimsService.Object,
            MockSubjectIdentifierService.Object
        );
    }

    #region Scaffolding

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private GetIdTokenSubjectClaimsCommand CreateCommand(
        SubjectAuthentication? subjectAuthentication,
        IReadOnlyList<string> effectiveScopes,
        IRequestClaims? requestClaims = null,
        bool alwaysIncludeAuthTime = true,
        bool authTimeRequired = false,
        string? accessToken = null
    )
    {
        var tokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdConstants.GrantTypes.AuthorizationCode,
            OriginalScopes = effectiveScopes,
            EffectiveScopes = effectiveScopes,
            SubjectAuthentication = subjectAuthentication,
            RequestClaims = requestClaims,
            AuthTimeRequired = authTimeRequired,
            AccessToken = accessToken,
        };

        var tokenContext = new SecurityTokenContext(
            tokenRequest,
            SigningCredentials: null!,
            EncryptionCredentials: null,
            TokenType: OpenIdConstants.TokenTypes.IdToken
        );

        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.AlwaysIncludeAuthTime))
            .Returns(alwaysIncludeAuthTime);

        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.Setup(x => x.Settings).Returns(mockSettings.Object);

        return new GetIdTokenSubjectClaimsCommand(
            CreateStrictMock<OpenIdContext>().Object,
            mockClient.Object,
            tokenContext,
            new List<Claim>()
        );
    }

    private List<string> CaptureCopiedClaimTypes(GetIdTokenSubjectClaimsCommand command)
    {
        List<string>? capturedClaimTypes = null;
        MockClaimsService
            .Setup(x =>
                x.CopyClaims(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<ICollection<Claim>>(),
                    true,
                    It.IsAny<IEnumerable<string>>()
                )
            )
            .Callback(
                (ClaimsPrincipal _, ICollection<Claim> _, bool _, IEnumerable<string> claimTypes) =>
                    capturedClaimTypes = claimTypes.ToList()
            )
            .Verifiable();
        MockClaimsService
            .Setup(x =>
                x.CopyClaims(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<ICollection<Claim>>(),
                    false,
                    It.IsAny<IEnumerable<string>>()
                )
            )
            .Verifiable();

        Handler.HandleAsync(command, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        Assert.NotNull(capturedClaimTypes);
        return capturedClaimTypes;
    }

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoSubjectAuthentication_DoesNotCopyClaims()
    {
        var command = CreateCommand(subjectAuthentication: null, ["openid"]);

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenIdTokenClaimsRequested_AddsThemToCopiedClaimTypes()
    {
        var requestClaims = new RequestClaims
        {
            IdToken = new RequestClaimDictionary
            {
                ["custom_claim"] = new RequestClaim { Essential = true },
            },
        };
        var command = CreateCommand(CreateSubjectAuthentication(), ["openid"], requestClaims);

        List<string>? capturedClaimTypes = null;
        MockClaimsService
            .Setup(x =>
                x.CopyClaims(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<ICollection<Claim>>(),
                    true,
                    It.IsAny<IEnumerable<string>>()
                )
            )
            .Callback(
                (ClaimsPrincipal _, ICollection<Claim> _, bool _, IEnumerable<string> claimTypes) =>
                    capturedClaimTypes = claimTypes.ToList()
            )
            .Verifiable();
        MockClaimsService
            .Setup(x =>
                x.CopyClaims(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<ICollection<Claim>>(),
                    false,
                    It.IsAny<IEnumerable<string>>()
                )
            )
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(capturedClaimTypes);
        Assert.Contains("custom_claim", capturedClaimTypes);
    }

    [Fact]
    public async Task HandleAsync_WhenSubjectAuthentication_CopiesClaims()
    {
        var command = CreateCommand(CreateSubjectAuthentication(), ["openid"]);

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
    public async Task HandleAsync_WhenScopeClaimsRequested_CopiesClaims()
    {
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            [
                OpenIdConstants.ScopeTypes.Profile,
                OpenIdConstants.ScopeTypes.Email,
                OpenIdConstants.ScopeTypes.Address,
                OpenIdConstants.ScopeTypes.Phone,
            ]
        );

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
    public void HandleAsync_AlwaysCopiesAcrClaimType()
    {
        var command = CreateCommand(CreateSubjectAuthentication(), ["openid"]);

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.Contains(JoseClaimNames.Payload.Acr, claimTypes);
    }

    [Fact]
    public void HandleAsync_WhenAlwaysIncludeAuthTime_CopiesAuthTimeClaimType()
    {
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid"],
            alwaysIncludeAuthTime: true
        );

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.Contains(JoseClaimNames.Payload.AuthTime, claimTypes);
    }

    [Fact]
    public void HandleAsync_WhenNotAlwaysIncludeAndNotRequired_OmitsAuthTimeClaimType()
    {
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid"],
            alwaysIncludeAuthTime: false
        );

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.DoesNotContain(JoseClaimNames.Payload.AuthTime, claimTypes);
    }

    [Fact]
    public void HandleAsync_WhenAuthTimeRequired_CopiesAuthTimeClaimType()
    {
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid"],
            alwaysIncludeAuthTime: false,
            authTimeRequired: true
        );

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.Contains(JoseClaimNames.Payload.AuthTime, claimTypes);
    }

    [Fact]
    public void HandleAsync_WhenEssentialAuthTimeRequested_CopiesAuthTimeClaimType()
    {
        var requestClaims = new RequestClaims
        {
            IdToken = new RequestClaimDictionary
            {
                [JoseClaimNames.Payload.AuthTime] = new RequestClaim { Essential = true },
            },
        };
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid"],
            requestClaims,
            alwaysIncludeAuthTime: false
        );

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.Contains(JoseClaimNames.Payload.AuthTime, claimTypes);
    }

    [Fact]
    public async Task HandleAsync_EmitsProjectedSubjectId_AndNeverCopiesSub()
    {
        // the service projects the stable principal id to a per-client pseudonym
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

        var requestClaims = new RequestClaims
        {
            // a client that explicitly requests 'sub' must still never receive the raw principal id
            IdToken = new RequestClaimDictionary
            {
                [JoseClaimNames.Payload.Sub] = new RequestClaim { Essential = true },
            },
        };
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid"],
            requestClaims,
            alwaysIncludeAuthTime: false
        );

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.DoesNotContain(JoseClaimNames.Payload.Sub, claimTypes);
        var subClaim = Assert.Single(
            command.SubjectClaims,
            claim => claim.Type == JoseClaimNames.Payload.Sub
        );
        Assert.Equal("pairwise-pseudonym", subClaim.Value);
    }

    [Fact]
    public void HandleAsync_WhenNoAccessTokenIssued_IncludesScopeClaimTypes()
    {
        // OpenID Connect Core 1.0 §5.4: with no access token issued the scope-driven claims belong in the ID token.
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            [OpenIdConstants.ScopeTypes.Profile, OpenIdConstants.ScopeTypes.Email],
            accessToken: null
        );

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.Contains(JoseClaimNames.Payload.Name, claimTypes);
        Assert.Contains(JoseClaimNames.Payload.Email, claimTypes);
    }

    [Fact]
    public void HandleAsync_WhenAccessTokenIssued_OmitsScopeClaimTypes()
    {
        // OpenID Connect Core 1.0 §5.4: when an access token is issued (the code flow) the scope-driven claims are
        // returned from the UserInfo endpoint, not the ID token; the account-sourced UserInfo enricher supplies them.
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            [OpenIdConstants.ScopeTypes.Profile, OpenIdConstants.ScopeTypes.Email],
            accessToken: "the-access-token"
        );

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.DoesNotContain(JoseClaimNames.Payload.Name, claimTypes);
        Assert.DoesNotContain(JoseClaimNames.Payload.Email, claimTypes);
        Assert.DoesNotContain(JoseClaimNames.Payload.EmailVerified, claimTypes);
    }

    [Fact]
    public void HandleAsync_WhenAccessTokenIssued_StillIncludesRequestedIdTokenClaims()
    {
        // Claims explicitly requested via the 'claims.id_token' parameter are additive to the scope gate and still
        // belong in the ID token even when an access token is issued.
        var requestClaims = new RequestClaims
        {
            IdToken = new RequestClaimDictionary
            {
                ["custom_claim"] = new RequestClaim { Essential = true },
            },
        };
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            [OpenIdConstants.ScopeTypes.Profile],
            requestClaims,
            accessToken: "the-access-token"
        );

        var claimTypes = CaptureCopiedClaimTypes(command);

        Assert.Contains("custom_claim", claimTypes);
        Assert.DoesNotContain(JoseClaimNames.Payload.Name, claimTypes);
    }

    #endregion
}
