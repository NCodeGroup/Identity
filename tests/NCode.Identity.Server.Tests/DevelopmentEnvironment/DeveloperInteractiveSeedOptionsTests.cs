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
    #region SeedSpaClient Tests

    [Fact]
    public void SeedSpaClient_WhenClientIdAndRedirectUrisPresent_IsTrue()
    {
        var options = new DeveloperInteractiveSeedOptions
        {
            SpaClientId = "mgmt-spa",
            SpaRedirectUris = ["https://localhost:5173/callback"],
        };

        Assert.True(options.SeedSpaClient);
    }

    [Fact]
    public void SeedSpaClient_WhenClientIdEmpty_IsFalse()
    {
        var options = new DeveloperInteractiveSeedOptions { SpaClientId = null };

        Assert.False(options.SeedSpaClient);
    }

    [Fact]
    public void SeedSpaClient_WhenNoRedirectUris_IsFalse()
    {
        var options = new DeveloperInteractiveSeedOptions
        {
            SpaClientId = "mgmt-spa",
            SpaRedirectUris = [],
        };

        Assert.False(options.SeedSpaClient);
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
