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

using NCode.Identity.Server.ConformanceEnvironment;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ConformanceSeedOptionsTests
{
    #region ConformanceClientSeed Tests

    [Fact]
    public void ShouldSeed_WhenClientIdSecretAndRedirectUrisPresent_IsTrue()
    {
        var client = new ConformanceClientSeed
        {
            ClientId = "client",
            ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0",
            RedirectUris = ["https://localhost.emobix.co.uk:8443/test/a/ncode-basic/callback"],
        };

        Assert.True(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenClientIdEmpty_IsFalse()
    {
        var client = new ConformanceClientSeed
        {
            ClientId = null,
            ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0",
            RedirectUris = ["https://localhost.emobix.co.uk:8443/test/a/ncode-basic/callback"],
        };

        Assert.False(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenClientSecretEmpty_IsFalse()
    {
        var client = new ConformanceClientSeed
        {
            ClientId = "client",
            ClientSecret = null,
            RedirectUris = ["https://localhost.emobix.co.uk:8443/test/a/ncode-basic/callback"],
        };

        Assert.False(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenNoRedirectUris_IsFalse()
    {
        var client = new ConformanceClientSeed
        {
            ClientId = "client",
            ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0",
            RedirectUris = [],
        };

        Assert.False(client.ShouldSeed);
    }

    [Fact]
    public void ClientSeed_ByDefault_UsesBasicOpGrantsScopesAndS256()
    {
        var client = new ConformanceClientSeed();

        Assert.Equal(["authorization_code", "refresh_token"], client.GrantTypes);
        Assert.Equal(["openid", "profile", "email"], client.Scopes);
        Assert.Equal(["client_secret_basic"], client.TokenEndpointAuthMethods);
        Assert.Equal(["S256"], client.CodeChallengeMethods);
    }

    #endregion

    #region SeedTestUser Tests

    [Fact]
    public void SeedTestUser_WhenUserNameAndPasswordPresent_IsTrue()
    {
        var options = new ConformanceSeedOptions
        {
            TestUserName = "conformance-user",
            TestUserPassword = "conformance-password",
        };

        Assert.True(options.SeedTestUser);
    }

    [Fact]
    public void SeedTestUser_WhenPasswordAbsent_IsFalse()
    {
        // There is intentionally no default password, so the test user is inert until one is configured.
        var options = new ConformanceSeedOptions
        {
            TestUserName = "conformance-user",
            TestUserPassword = null,
        };

        Assert.False(options.SeedTestUser);
    }

    #endregion
}
