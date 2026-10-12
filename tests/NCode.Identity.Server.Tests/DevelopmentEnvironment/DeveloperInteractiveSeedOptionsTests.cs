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

using NCode.Identity.Server.DevelopmentEnvironment;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class DeveloperInteractiveSeedOptionsTests
{
    #region SpaClients Tests

    [Fact]
    public void SpaClients_ByDefault_SeedsManagementAndPlayground()
    {
        var options = new DeveloperInteractiveSeedOptions();

        Assert.Contains(options.SpaClients, client => client.ClientId == "mgmt-spa");
        Assert.Contains(options.SpaClients, client => client.ClientId == "playground-spa");
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
