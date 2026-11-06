// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DeveloperInteractiveSeedOptionsTests
{
    #region SpaClients Tests

    [Fact]
    public void SpaClients_ByDefault_IsEmpty()
    {
        // There are no built-in clients: the seed registers only what configuration declares.
        var options = new DeveloperInteractiveSeedOptions();

        Assert.Empty(options.SpaClients);
    }

    [Fact]
    public void SpaClients_WhenBoundFromConfiguration_HoldsExactlyTheConfiguredClients()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DeveloperInteractiveSeed:SpaClients:0:ClientId"] = "custom-spa",
                    ["DeveloperInteractiveSeed:SpaClients:0:RedirectUris:0"] =
                        "https://localhost:6000/callback",
                    ["DeveloperInteractiveSeed:SpaClients:0:RequireConsent"] = "true",
                }
            )
            .Build();
        var options = new DeveloperInteractiveSeedOptions();

        configuration
            .GetSection(DeveloperInteractiveSeedOptions.DefaultSectionName)
            .BindStrictly(options);

        var client = Assert.Single(options.SpaClients);
        Assert.Equal("custom-spa", client.ClientId);
        Assert.Equal(["https://localhost:6000/callback"], client.RedirectUris);
        Assert.True(client.RequireConsent);
    }

    [Fact]
    public void RequireConsent_ByDefault_IsFalse()
    {
        var client = new DeveloperSpaClientSeed();

        Assert.False(client.RequireConsent);
    }

    [Fact]
    public void ShouldSeed_WhenClientIdAndRedirectUrisPresent_IsTrue()
    {
        var client = new DeveloperSpaClientSeed
        {
            ClientId = "mgmt-spa",
            RedirectUris = ["https://localhost:5173/callback"],
        };

        Assert.True(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenClientIdEmpty_IsFalse()
    {
        var client = new DeveloperSpaClientSeed
        {
            ClientId = null,
            RedirectUris = ["https://localhost:5173/callback"],
        };

        Assert.False(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenNoRedirectUris_IsFalse()
    {
        var client = new DeveloperSpaClientSeed { ClientId = "mgmt-spa", RedirectUris = [] };

        Assert.False(client.ShouldSeed);
    }

    #endregion

    #region BffClients Tests

    [Fact]
    public void BffClients_ByDefault_IsEmpty()
    {
        var options = new DeveloperInteractiveSeedOptions();

        Assert.Empty(options.BffClients);
    }

    [Fact]
    public void BffClients_WhenBoundFromConfiguration_HoldsExactlyTheConfiguredClients()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DeveloperInteractiveSeed:BffClients:0:ClientId"] = "custom-bff",
                    ["DeveloperInteractiveSeed:BffClients:0:Jwks"] = "{\"keys\":[]}",
                    ["DeveloperInteractiveSeed:BffClients:0:RedirectUris:0"] =
                        "https://localhost:6000/bff/callback",
                    ["DeveloperInteractiveSeed:BffClients:0:PostLogoutRedirectUris:0"] =
                        "https://localhost:6000/",
                    ["DeveloperInteractiveSeed:BffClients:0:BackchannelLogoutUri"] =
                        "https://localhost:6000/bff/backchannel-logout",
                }
            )
            .Build();
        var options = new DeveloperInteractiveSeedOptions();

        configuration
            .GetSection(DeveloperInteractiveSeedOptions.DefaultSectionName)
            .BindStrictly(options);

        var client = Assert.Single(options.BffClients);
        Assert.Equal("custom-bff", client.ClientId);
        Assert.Equal("{\"keys\":[]}", client.Jwks);
        Assert.Equal(["https://localhost:6000/bff/callback"], client.RedirectUris);
        Assert.Equal(["https://localhost:6000/"], client.PostLogoutRedirectUris);
        Assert.Equal("https://localhost:6000/bff/backchannel-logout", client.BackchannelLogoutUri);
    }

    [Fact]
    public void ShouldSeed_WhenBffClientHasClientIdKeySetAndRedirectUri_IsTrue()
    {
        var client = new DeveloperBffClientSeed
        {
            ClientId = "mgmt-console",
            Jwks = "{\"keys\":[]}",
            RedirectUris = ["https://localhost:5005/bff/callback"],
        };

        Assert.True(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenBffClientIdEmpty_IsFalse()
    {
        var client = new DeveloperBffClientSeed
        {
            ClientId = null,
            Jwks = "{\"keys\":[]}",
            RedirectUris = ["https://localhost:5005/bff/callback"],
        };

        Assert.False(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenBffClientKeySetEmpty_IsFalse()
    {
        var client = new DeveloperBffClientSeed
        {
            ClientId = "mgmt-console",
            Jwks = null,
            RedirectUris = ["https://localhost:5005/bff/callback"],
        };

        Assert.False(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenBffClientHasNoRedirectUris_IsFalse()
    {
        var client = new DeveloperBffClientSeed
        {
            ClientId = "mgmt-console",
            Jwks = "{\"keys\":[]}",
            RedirectUris = [],
        };

        Assert.False(client.ShouldSeed);
    }

    #endregion

    #region SeedAdminUser Tests

    [Fact]
    public void SeedAdminUser_WhenUserNameAndPasswordPresent_IsTrue()
    {
        var options = new DeveloperInteractiveSeedOptions
        {
            AdminUserName = "admin",
            AdminPassword = "dev-password",
        };

        Assert.True(options.SeedAdminUser);
    }

    [Fact]
    public void SeedAdminUser_WhenPasswordAbsent_IsFalse()
    {
        // There is intentionally no default password, so the admin user is inert until one is configured.
        var options = new DeveloperInteractiveSeedOptions
        {
            AdminUserName = "admin",
            AdminPassword = null,
        };

        Assert.False(options.SeedAdminUser);
    }

    #endregion
}
