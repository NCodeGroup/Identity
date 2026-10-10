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

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Claims;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Authentication.Tokens.Handlers;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.Handlers;

public class DefaultGetAccessTokenMetadataClaimsHandlerTests : BaseTests
{
    private Mock<IClaimsService> MockClaimsService { get; }
    private DefaultGetAccessTokenMetadataClaimsHandler Handler { get; }

    public DefaultGetAccessTokenMetadataClaimsHandlerTests()
    {
        MockClaimsService = CreateStrictMock<IClaimsService>();
        Handler = new DefaultGetAccessTokenMetadataClaimsHandler(MockClaimsService.Object);
    }

    #region Scaffolding

    private static readonly DateTimeOffset CreatedWhen = DateTimeOffset.Parse(
        "2025-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private GetAccessTokenSubjectClaimsCommand CreateCommand(
        SubjectAuthentication? subjectAuthentication,
        IReadOnlySettingCollection? settings
    )
    {
        IReadOnlyList<string> scopes = ["openid"];
        var tokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = CreatedWhen,
            GrantType = OpenIdConstants.GrantTypes.Password,
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

        var mockClient = CreateStrictMock<OpenIdClient>();
        if (settings is not null)
        {
            mockClient.Setup(x => x.Settings).Returns(settings);
        }

        return new GetAccessTokenSubjectClaimsCommand(
            CreateStrictMock<OpenIdContext>().Object,
            mockClient.Object,
            tokenContext,
            new List<Claim>()
        );
    }

    private static SubjectAuthentication CreateSubjectAuthentication() =>
        new("scheme", new AuthenticationProperties(), new ClaimsPrincipal(), "subject-id");

    private IReadOnlySettingCollection CreateSettings(bool profile, bool system)
    {
        var mock = CreateStrictMock<IReadOnlySettingCollection>();
        mock.Setup(x => x.GetValue(OpenIdSettingKeys.SendProfileMetadataInAccessToken))
            .Returns(profile);
        mock.Setup(x => x.GetValue(OpenIdSettingKeys.SendSystemMetadataInAccessToken))
            .Returns(system);
        return mock.Object;
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoSubjectAuthentication_DoesNothing()
    {
        var command = CreateCommand(subjectAuthentication: null, settings: null);

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenBothDisabled_DoesNotCopyClaims()
    {
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            CreateSettings(profile: false, system: false)
        );

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenSystemEnabled_CopiesOnlySystemMetadata()
    {
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            CreateSettings(profile: false, system: true)
        );

        MockClaimsService
            .Setup(x =>
                x.CopyClaims(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<ICollection<Claim>>(),
                    true,
                    It.Is<IEnumerable<string>>(types =>
                        types.Contains(AccountConstants.SystemMetadataClaimType)
                        && !types.Contains(AccountConstants.ProfileMetadataClaimType)
                    )
                )
            )
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenBothEnabled_CopiesBothMetadata()
    {
        var command = CreateCommand(
            CreateSubjectAuthentication(),
            CreateSettings(profile: true, system: true)
        );

        MockClaimsService
            .Setup(x =>
                x.CopyClaims(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<ICollection<Claim>>(),
                    true,
                    It.Is<IEnumerable<string>>(types =>
                        types.Contains(AccountConstants.ProfileMetadataClaimType)
                        && types.Contains(AccountConstants.SystemMetadataClaimType)
                    )
                )
            )
            .Verifiable();

        await Handler.HandleAsync(command, CancellationToken.None);
    }

    #endregion
}
