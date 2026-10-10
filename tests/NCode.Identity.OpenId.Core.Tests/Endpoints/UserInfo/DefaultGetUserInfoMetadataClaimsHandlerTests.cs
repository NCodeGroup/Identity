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
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.UserInfo;

public class DefaultGetUserInfoMetadataClaimsHandlerTests : BaseTests
{
    private DefaultGetUserInfoMetadataClaimsHandler Handler { get; } = new();

    #region Scaffolding

    private (GetUserInfoClaimsCommand command, Dictionary<string, object> claims) CreateCommand(
        ClaimsPrincipal subject,
        bool profile,
        bool system
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.SendProfileMetadataInUserInfo))
            .Returns(profile);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.SendSystemMetadataInUserInfo))
            .Returns(system);

        var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockSettings.Object);

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object);

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object);

        var subjectAuthentication = new SubjectAuthentication(
            "scheme",
            new AuthenticationProperties(),
            subject,
            "subject-123"
        );

        var claims = new Dictionary<string, object>();
        var command = new GetUserInfoClaimsCommand(
            mockContext.Object,
            subjectAuthentication,
            claims
        );

        return (command, claims);
    }

    private static ClaimsPrincipal CreateSubject(params Claim[] claims)
    {
        var identity = new ClaimsIdentity("scheme");
        identity.AddClaims(claims);
        return new ClaimsPrincipal(identity);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenBothDisabled_AddsNothing()
    {
        var subject = CreateSubject(
            new Claim(AccountConstants.ProfileMetadataClaimType, """{"theme":"dark"}"""),
            new Claim(AccountConstants.SystemMetadataClaimType, """{"plan":"gold"}""")
        );
        var (command, claims) = CreateCommand(subject, profile: false, system: false);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(claims);
    }

    [Fact]
    public async Task HandleAsync_WhenProfileEnabled_AddsProfileMetadataObject()
    {
        var subject = CreateSubject(
            new Claim(AccountConstants.ProfileMetadataClaimType, """{"theme":"dark"}""")
        );
        var (command, claims) = CreateCommand(subject, profile: true, system: false);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(claims.TryGetValue(AccountConstants.ProfileMetadataClaimType, out var value));
        var element = Assert.IsType<JsonElement>(value);
        Assert.Equal("dark", element.GetProperty("theme").GetString());
        Assert.False(claims.ContainsKey(AccountConstants.SystemMetadataClaimType));
    }

    [Fact]
    public async Task HandleAsync_WhenEnabledButSubjectHasNoMetadata_AddsNothing()
    {
        var subject = CreateSubject();
        var (command, claims) = CreateCommand(subject, profile: true, system: true);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(claims);
    }

    #endregion
}
