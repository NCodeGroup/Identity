// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        Handler = new DefaultGetIdTokenSubjectClaimsHandler(
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
            GrantType = OpenIdProtocol.GrantTypes.AuthorizationCode,
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
            TokenType: OpenIdProtocol.TokenTypes.IdToken
        );

        // Only a token for an authenticated subject reads the client's auth_time setting.
        var mockClient = CreateStrictMock<OpenIdClient>();
        if (subjectAuthentication is not null)
        {
            var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.AlwaysIncludeAuthTime))
                .Returns(alwaysIncludeAuthTime)
                .Verifiable();
            mockClient.Setup(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        }

        return new GetIdTokenSubjectClaimsCommand(
            CreateStrictMock<OpenIdContext>().Object,
            mockClient.Object,
            tokenContext,
            []
        );
    }

    private async Task<List<string>> CaptureCopiedClaimTypesAsync(
        GetIdTokenSubjectClaimsCommand command
    )
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

        await Handler.HandleAsync(command, CancellationToken.None);

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
        SetupSubjectProjection();
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
        SetupSubjectProjection();
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
        SetupSubjectProjection();
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            [
                OpenIdProtocol.ScopeTypes.Profile,
                OpenIdProtocol.ScopeTypes.Email,
                OpenIdProtocol.ScopeTypes.Address,
                OpenIdProtocol.ScopeTypes.Phone,
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
    public async Task HandleAsync_AlwaysCopiesAcrClaimType()
    {
        SetupSubjectProjection();
        var command = CreateCommand(CreateSubjectAuthentication(), ["openid"]);

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

        Assert.Contains(JoseClaimNames.Payload.Acr, claimTypes);
    }

    [Fact]
    public async Task HandleAsync_WhenAlwaysIncludeAuthTime_CopiesAuthTimeClaimType()
    {
        SetupSubjectProjection();
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid"],
            alwaysIncludeAuthTime: true
        );

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

        Assert.Contains(JoseClaimNames.Payload.AuthTime, claimTypes);
    }

    [Fact]
    public async Task HandleAsync_WhenNotAlwaysIncludeAndNotRequired_OmitsAuthTimeClaimType()
    {
        SetupSubjectProjection();
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid"],
            alwaysIncludeAuthTime: false
        );

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

        Assert.DoesNotContain(JoseClaimNames.Payload.AuthTime, claimTypes);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthTimeRequired_CopiesAuthTimeClaimType()
    {
        SetupSubjectProjection();
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            ["openid"],
            alwaysIncludeAuthTime: false,
            authTimeRequired: true
        );

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

        Assert.Contains(JoseClaimNames.Payload.AuthTime, claimTypes);
    }

    [Fact]
    public async Task HandleAsync_WhenEssentialAuthTimeRequested_CopiesAuthTimeClaimType()
    {
        SetupSubjectProjection();
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

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

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

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

        Assert.DoesNotContain(JoseClaimNames.Payload.Sub, claimTypes);
        var subClaim = Assert.Single(
            command.SubjectClaims,
            claim => claim.Type == JoseClaimNames.Payload.Sub
        );
        Assert.Equal("pairwise-pseudonym", subClaim.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAccessTokenIssued_IncludesScopeClaimTypes()
    {
        SetupSubjectProjection();
        // OpenID Connect Core 1.0 §5.4: with no access token issued the scope-driven claims belong in the ID token.
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            [OpenIdProtocol.ScopeTypes.Profile, OpenIdProtocol.ScopeTypes.Email],
            accessToken: null
        );

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

        Assert.Contains(JoseClaimNames.Payload.Name, claimTypes);
        Assert.Contains(JoseClaimNames.Payload.Email, claimTypes);
    }

    [Fact]
    public async Task HandleAsync_WhenAccessTokenIssued_OmitsScopeClaimTypes()
    {
        SetupSubjectProjection();
        // OpenID Connect Core 1.0 §5.4: when an access token is issued (the code flow) the scope-driven claims are
        // returned from the UserInfo endpoint, not the ID token; the account-sourced UserInfo enricher supplies them.
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            [OpenIdProtocol.ScopeTypes.Profile, OpenIdProtocol.ScopeTypes.Email],
            accessToken: "the-access-token"
        );

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

        Assert.DoesNotContain(JoseClaimNames.Payload.Name, claimTypes);
        Assert.DoesNotContain(JoseClaimNames.Payload.Email, claimTypes);
        Assert.DoesNotContain(JoseClaimNames.Payload.EmailVerified, claimTypes);
    }

    [Fact]
    public async Task HandleAsync_WhenAccessTokenIssued_StillIncludesRequestedIdTokenClaims()
    {
        SetupSubjectProjection();
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
            [OpenIdProtocol.ScopeTypes.Profile],
            requestClaims,
            accessToken: "the-access-token"
        );

        var claimTypes = await CaptureCopiedClaimTypesAsync(command);

        Assert.Contains("custom_claim", claimTypes);
        Assert.DoesNotContain(JoseClaimNames.Payload.Name, claimTypes);
    }

    #endregion
}
