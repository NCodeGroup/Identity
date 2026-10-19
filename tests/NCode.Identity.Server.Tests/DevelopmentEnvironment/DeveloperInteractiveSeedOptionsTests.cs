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
