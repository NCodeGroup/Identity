// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        new PersistedTenantBuilder { TenantId = TenantId }.Build();

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
        var platform = CreateLooseMock<ISettingCollection>().Object;
        var tenantSettings = CreateLooseMock<ISettingCollection>().Object;

        MockEffectiveSettingsResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(platform)
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x => x.Resolve(platform, OpenIdSettingScopes.Tenant, It.IsAny<JsonElement>()))
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
