// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Jwt.Claims;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Principals;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.Handlers;

public class DefaultGetAccessTokenMetadataClaimsHandlerTests : BaseTests
{
    private Mock<IPrincipalMetadataProvider> MockMetadataProvider { get; }
    private DefaultGetAccessTokenMetadataClaimsHandler Handler { get; }

    public DefaultGetAccessTokenMetadataClaimsHandlerTests()
    {
        MockMetadataProvider = CreateStrictMock<IPrincipalMetadataProvider>();
        Handler = new DefaultGetAccessTokenMetadataClaimsHandler(MockMetadataProvider.Object);
    }

    #region Scaffolding

    private const string PrincipalId = "subject-id";

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private (
        GetAccessTokenSubjectClaimsCommand Command,
        List<Claim> TargetClaims,
        OpenIdContext Context
    ) CreateCommand(
        SubjectAuthentication? subjectAuthentication,
        IReadOnlySettingCollection? settings
    )
    {
        IReadOnlyList<string> scopes = ["openid"];
        var tokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdProtocol.GrantTypes.Password,
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

        var mockClient = CreateStrictMock<OpenIdClient>();
        if (settings is not null)
        {
            mockClient.Setup(x => x.Settings).Returns(settings).Verifiable();
        }

        var context = CreateStrictMock<OpenIdContext>().Object;
        var targetClaims = new List<Claim>();
        var command = new GetAccessTokenSubjectClaimsCommand(
            context,
            mockClient.Object,
            tokenContext,
            targetClaims
        );
        return (command, targetClaims, context);
    }

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), PrincipalId);

    private IReadOnlySettingCollection CreateSettings(bool profile, bool system)
    {
        var mock = CreateStrictMock<IReadOnlySettingCollection>();
        mock.Setup(x => x.GetValue(OpenIdSettingKeys.SendProfileMetadataInAccessToken))
            .Returns(profile)
            .Verifiable();
        mock.Setup(x => x.GetValue(OpenIdSettingKeys.SendSystemMetadataInAccessToken))
            .Returns(system)
            .Verifiable();
        return mock.Object;
    }

    private void SetupMetadata(OpenIdContext context, JsonElement profile, JsonElement system) =>
        MockMetadataProvider
            .Setup(x => x.GetMetadataAsync(context, PrincipalId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PrincipalMetadata>(new PrincipalMetadata(profile, system)))
            .Verifiable();

    private static JsonElement Obj(string json) => JsonElement.Parse(json);

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoSubjectAuthentication_DoesNothing()
    {
        var (command, targetClaims, _) = CreateCommand(subjectAuthentication: null, settings: null);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(targetClaims);
    }

    [Fact]
    public async Task HandleAsync_WhenBothDisabled_DoesNotResolveMetadata()
    {
        var (command, targetClaims, _) = CreateCommand(
            CreateSubjectAuthentication(),
            CreateSettings(profile: false, system: false)
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(targetClaims);
    }

    [Fact]
    public async Task HandleAsync_WhenSystemEnabled_EmitsOnlySystemMetadata()
    {
        var (command, targetClaims, context) = CreateCommand(
            CreateSubjectAuthentication(),
            CreateSettings(profile: false, system: true)
        );
        SetupMetadata(context, Obj("""{"theme":"dark"}"""), Obj("""{"plan":"gold"}"""));

        await Handler.HandleAsync(command, CancellationToken.None);

        var claim = Assert.Single(
            targetClaims,
            c => c.Type == SubjectMetadataClaimTypes.SystemMetadata
        );
        Assert.Equal(JsonClaimValueTypes.Json, claim.ValueType);
        Assert.DoesNotContain(
            targetClaims,
            c => c.Type == SubjectMetadataClaimTypes.ProfileMetadata
        );
    }

    [Fact]
    public async Task HandleAsync_WhenBothEnabled_EmitsBothMetadata()
    {
        var (command, targetClaims, context) = CreateCommand(
            CreateSubjectAuthentication(),
            CreateSettings(profile: true, system: true)
        );
        SetupMetadata(context, Obj("""{"theme":"dark"}"""), Obj("""{"plan":"gold"}"""));

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Contains(targetClaims, c => c.Type == SubjectMetadataClaimTypes.ProfileMetadata);
        Assert.Contains(targetClaims, c => c.Type == SubjectMetadataClaimTypes.SystemMetadata);
    }

    #endregion
}
