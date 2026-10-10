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
using Moq;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Principals;
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

    private CreatePasswordGrantSubjectCommand CreateCommand(PersistedLocalAccount account)
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

    private static PersistedLocalAccount CreateAccount() =>
        new()
        {
            TenantId = string.Empty,
            LocalAccountId = "account-1",
            UserName = "user-1",
            Email = null,
            EmailVerified = false,
            IsEnabled = true,
            Claims = [new PersistedLocalAccountClaim { Type = "role", Value = "admin" }],
            ConcurrencyToken = string.Empty,
        };

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_StampsSubjectIssuerAndAccountClaims()
    {
        var command = CreateCommand(CreateAccount());

        var principal = await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("account-1", principal.FindFirstValue("sub"));
        Assert.Equal(AccountConstants.SelfIssuer, principal.FindFirstValue("iss"));
        Assert.Equal("admin", principal.FindFirstValue("role"));
    }

    [Fact]
    public async Task HandleAsync_DoesNotStampMetadataClaims()
    {
        var command = CreateCommand(CreateAccount());

        var principal = await Handler.HandleAsync(command, CancellationToken.None);

        // Metadata is a principal-level concept resolved at issuance, never stamped on the ROPC subject (ADR-0055).
        Assert.Null(principal.FindFirst(SubjectMetadataClaimTypes.ProfileMetadata));
        Assert.Null(principal.FindFirst(SubjectMetadataClaimTypes.SystemMetadata));
    }

    #endregion
}
