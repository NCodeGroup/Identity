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
using NCode.Identity.Server.ConformanceEnvironment;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ConformanceSeedOptionsTests
{
    #region ConformanceClientSeed Tests

    [Fact]
    public void ShouldSeed_WhenSecretAndRedirectUrisPresent_IsTrue()
    {
        var client = new ConformanceClientSeed
        {
            ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0",
            RedirectUris = ["https://localhost.emobix.co.uk:8443/test/a/ncode-basic/callback"],
        };

        Assert.True(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenNeitherSecretNorKeySet_IsFalse()
    {
        var client = new ConformanceClientSeed
        {
            ClientSecret = null,
            RedirectUris = ["https://localhost.emobix.co.uk:8443/test/a/ncode-basic/callback"],
        };

        Assert.False(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenKeySetWithoutSecret_IsTrue()
    {
        var client = new ConformanceClientSeed
        {
            ClientSecret = null,
            Jwks = """{"keys":[]}""",
            RedirectUris = ["https://localhost.emobix.co.uk:8443/test/a/ncode-fapi1/callback"],
        };

        Assert.True(client.ShouldSeed);
    }

    [Fact]
    public void ShouldSeed_WhenNoRedirectUris_IsFalse()
    {
        var client = new ConformanceClientSeed
        {
            ClientSecret = "Y29uZm9ybWFuY2Utc2VjcmV0",
            RedirectUris = [],
        };

        Assert.False(client.ShouldSeed);
    }

    [Fact]
    public void ClientSeed_ByDefault_RegistersClientSecretBasicAndLeavesListsEmpty()
    {
        // The lists start empty so configuration replaces, rather than extends, their defaults.
        var client = new ConformanceClientSeed();

        Assert.Empty(client.GrantTypes);
        Assert.Equal("client_secret_basic", client.TokenEndpointAuthMethod);
        Assert.Empty(client.CodeChallengeMethods);
    }

    [Fact]
    public void ClientSeed_WhenBoundFromConfiguration_HoldsExactlyTheConfiguredValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConformanceSeed:Clients:client-secret-post:GrantTypes:0"] =
                        "authorization_code",
                    ["ConformanceSeed:Clients:client-secret-post:TokenEndpointAuthMethod"] =
                        "client_secret_post",
                    ["ConformanceSeed:Clients:client-secret-post:CodeChallengeMethods:0"] = "S256",
                }
            )
            .Build();
        var options = new ConformanceSeedOptions();

        configuration.GetSection(ConformanceSeedOptions.DefaultSectionName).BindStrictly(options);

        var (clientId, client) = Assert.Single(options.Clients);
        Assert.Equal("client-secret-post", clientId);
        Assert.Equal(["authorization_code"], client.GrantTypes);
        Assert.Equal("client_secret_post", client.TokenEndpointAuthMethod);
        Assert.Equal(["S256"], client.CodeChallengeMethods);
    }

    [Fact]
    public void Clients_WhenALaterSourceAddsAKeySet_MergesIntoTheClientWithThatId()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConformanceSeed:Clients:fapi1-mtls:RedirectUris:0"] =
                        "https://client.example/callback",
                    ["ConformanceSeed:Clients:fapi1-mtls:TokenEndpointAuthMethod"] =
                        "self_signed_tls_client_auth",
                }
            )
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConformanceSeed:Clients:fapi1-mtls:Jwks"] = """{"keys":[]}""",
                }
            )
            .Build();
        var options = new ConformanceSeedOptions();

        configuration.GetSection(ConformanceSeedOptions.DefaultSectionName).BindStrictly(options);

        var (clientId, client) = Assert.Single(options.Clients);
        Assert.Equal("fapi1-mtls", clientId);
        Assert.Equal("""{"keys":[]}""", client.Jwks);
        Assert.Equal(["https://client.example/callback"], client.RedirectUris);
        Assert.True(client.ShouldSeed);
    }

    [Fact]
    public void Clients_WhenAnEntryNamesAClientId_FailsToBind()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConformanceSeed:Clients:client:ClientId"] = "client",
                }
            )
            .Build();
        var options = new ConformanceSeedOptions();

        Assert.Throws<InvalidOperationException>(() =>
            configuration
                .GetSection(ConformanceSeedOptions.DefaultSectionName)
                .BindStrictly(options)
        );
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
