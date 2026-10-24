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

using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class SeededLocalAccountValidatorTests : BaseTests
{
    private const string TenantId = "default";

    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IPasswordPolicy> MockPasswordPolicy { get; }

    private SeededLocalAccountValidator Validator { get; }

    public SeededLocalAccountValidatorTests()
    {
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockPasswordPolicy = CreateStrictMock<IPasswordPolicy>();

        Validator = new SeededLocalAccountValidator(
            MockEffectiveSettingsResolver.Object,
            MockPasswordPolicy.Object
        );
    }

    #region Scaffolding

    private static PersistedTenant CreateTenant() =>
        new()
        {
            TenantId = TenantId,
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = TenantId,
                Value = JsonElement.Parse("{}"),
            },
            Secrets = null!,
        };

    private static PersistedLocalAccount CreateAccount() =>
        new()
        {
            TenantId = TenantId,
            LocalAccountId = "account-1",
            UserName = "admin",
            Email = "admin@localhost",
            EmailVerified = true,
            IsEnabled = true,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

    private IReadOnlySettingCollection SetupTenantResolution()
    {
        var server = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;

        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(server)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(server, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
            .Returns(tenantSettings)
            .Verifiable();

        return tenantSettings;
    }

    private void SetupPolicy(
        IReadOnlySettingCollection tenantSettings,
        PasswordPolicyResult result
    ) =>
        MockPasswordPolicy
            .Setup(x =>
                x.ValidateAsync(
                    It.Is<PasswordPolicyContext>(c =>
                        c.Settings == tenantSettings
                        && c.UserName == "admin"
                        && c.Email == "admin@localhost"
                    ),
                    It.IsAny<ReadOnlyMemory<char>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    #endregion

    #region EnsureCanBeSeededAsync Tests

    [Fact]
    public async Task EnsureCanBeSeededAsync_WhenThePolicyAcceptsThePassword_Completes()
    {
        var tenantSettings = SetupTenantResolution();
        SetupPolicy(tenantSettings, PasswordPolicyResult.Accepted);

        await Validator.EnsureCanBeSeededAsync(
            "developer administrator",
            CreateTenant(),
            CreateAccount(),
            "dev-admin-password",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task EnsureCanBeSeededAsync_WhenThePolicyRefusesThePassword_ThrowsNamingTheAccount()
    {
        var tenantSettings = SetupTenantResolution();
        SetupPolicy(
            tenantSettings,
            PasswordPolicyResult.Rejected(
                PasswordPolicyErrors.TooShort,
                "The password must have at least 15 characters."
            )
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Validator.EnsureCanBeSeededAsync(
                "developer administrator",
                CreateTenant(),
                CreateAccount(),
                "short",
                CancellationToken.None
            )
        );

        Assert.Contains("'admin'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("at least 15", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
