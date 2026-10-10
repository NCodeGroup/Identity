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
using Moq;
using NCode.Identity.Json;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Token.Password;

public class DefaultCreatePasswordGrantSubjectHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private DefaultCreatePasswordGrantSubjectHandler Handler { get; } = new(TimeProvider.System);

    #region Scaffolding

    private CreatePasswordGrantSubjectCommand CreateCommand(LocalAccount account)
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.SubjectClaimTypes))
            .Returns(new[] { "sub" });
        mockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim)).Returns("iss");

        var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockSettings.Object);

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object);
        mockTenant.Setup(x => x.TenantId).Returns(TenantId);

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object);

        return new CreatePasswordGrantSubjectCommand(mockContext.Object, account);
    }

    private static LocalAccount CreateAccount(JsonElement? profile, JsonElement? system) =>
        new()
        {
            Subject = "account-1",
            IsEnabled = true,
            Claims = [new Claim("role", "admin")],
            ProfileMetadata = profile ?? JsonElements.EmptyObject,
            SystemMetadata = system ?? JsonElements.EmptyObject,
        };

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenMetadataPresent_StampsBothBagsAsJsonClaims()
    {
        var account = CreateAccount(
            JsonElement.Parse("""{"theme":"dark"}"""),
            JsonElement.Parse("""{"plan":"gold"}""")
        );
        var command = CreateCommand(account);

        var principal = await Handler.HandleAsync(command, CancellationToken.None);

        var profileClaim = principal.FindFirst(AccountConstants.ProfileMetadataClaimType);
        Assert.NotNull(profileClaim);
        Assert.Equal(JsonClaimValueTypes.Json, profileClaim.ValueType);
        Assert.Equal("""{"theme":"dark"}""", profileClaim.Value);

        var systemClaim = principal.FindFirst(AccountConstants.SystemMetadataClaimType);
        Assert.NotNull(systemClaim);
        Assert.Equal(JsonClaimValueTypes.Json, systemClaim.ValueType);
    }

    [Fact]
    public async Task HandleAsync_WhenMetadataEmpty_DoesNotStampMetadataClaims()
    {
        var command = CreateCommand(CreateAccount(profile: null, system: null));

        var principal = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(principal.FindFirst(AccountConstants.ProfileMetadataClaimType));
        Assert.Null(principal.FindFirst(AccountConstants.SystemMetadataClaimType));
        // the ordinary account claims still flow
        Assert.Equal("admin", principal.FindFirstValue("role"));
    }

    #endregion
}
