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
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Principals;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.UserInfo;

public class DefaultGetUserInfoMetadataClaimsHandlerTests : BaseTests
{
    private Mock<IPrincipalMetadataProvider> MockMetadataProvider { get; }
    private DefaultGetUserInfoMetadataClaimsHandler Handler { get; }

    public DefaultGetUserInfoMetadataClaimsHandlerTests()
    {
        MockMetadataProvider = CreateStrictMock<IPrincipalMetadataProvider>();
        Handler = new DefaultGetUserInfoMetadataClaimsHandler(MockMetadataProvider.Object);
    }

    #region Scaffolding

    private const string PrincipalId = "subject-123";

    private (
        GetUserInfoClaimsCommand Command,
        Dictionary<string, object> Claims,
        OpenIdContext Context
    ) CreateCommand(bool profile, bool system)
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
            new ClaimsPrincipal(),
            PrincipalId
        );

        var claims = new Dictionary<string, object>();
        var command = new GetUserInfoClaimsCommand(
            mockContext.Object,
            subjectAuthentication,
            claims
        );

        return (command, claims, mockContext.Object);
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
    public async Task HandleAsync_WhenBothDisabled_AddsNothing()
    {
        var (command, claims, _) = CreateCommand(profile: false, system: false);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(claims);
    }

    [Fact]
    public async Task HandleAsync_WhenProfileEnabled_AddsProfileMetadataObject()
    {
        var (command, claims, context) = CreateCommand(profile: true, system: false);
        SetupMetadata(context, Obj("""{"theme":"dark"}"""), Obj("""{"plan":"gold"}"""));

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.True(claims.TryGetValue(SubjectMetadataClaimTypes.ProfileMetadata, out var value));
        var element = Assert.IsType<JsonElement>(value);
        Assert.Equal("dark", element.GetProperty("theme").GetString());
        Assert.False(claims.ContainsKey(SubjectMetadataClaimTypes.SystemMetadata));
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataEmpty_AddsNothing()
    {
        var (command, claims, context) = CreateCommand(profile: true, system: true);
        SetupMetadata(context, Obj("{}"), Obj("{}"));

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(claims);
    }

    #endregion
}
