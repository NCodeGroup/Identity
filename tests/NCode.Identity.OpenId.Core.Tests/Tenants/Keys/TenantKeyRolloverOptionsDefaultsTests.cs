// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Tenants.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyRolloverOptionsDefaultsTests
{
    private TenantKeyRolloverOptionsDefaults Defaults { get; } = new();

    #region PostConfigure Tests

    [Fact]
    public void PostConfigure_WhenConfigurationNamesNoSetting_FillsTheDefaults()
    {
        var options = new OpenIdOptions();
        Assert.Empty(options.Tenant.KeyRollover.TokenLifetimeSettings);

        Defaults.PostConfigure(null, options);

        Assert.Equal(
            OpenIdTenantKeyRolloverOptions.DefaultTokenLifetimeSettings,
            options.Tenant.KeyRollover.TokenLifetimeSettings
        );
    }

    [Fact]
    public void PostConfigure_WhenConfigurationNamesASetting_KeepsOnlyTheConfiguredNames()
    {
        // Configuration replaces the defaults rather than adding to them, so a deployment can drop a default from
        // the retirement-grace floor.
        var options = new OpenIdOptions
        {
            Tenant = { KeyRollover = { TokenLifetimeSettings = ["access_token_lifetime"] } },
        };

        Defaults.PostConfigure(null, options);

        Assert.Equal(["access_token_lifetime"], options.Tenant.KeyRollover.TokenLifetimeSettings);
    }

    #endregion
}
