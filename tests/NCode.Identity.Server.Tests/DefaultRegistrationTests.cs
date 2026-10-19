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

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DefaultRegistrationTests
{
    private static IHost BuildHost(Dictionary<string, string?> configurationData)
    {
        var hostBuilder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        hostBuilder.Configuration.AddInMemoryCollection(configurationData);
        hostBuilder.Services.AddIdentityServer().AddConfiguration(hostBuilder.Configuration);

        // These tests exercise configuration only; the persistence provider a host composes is never called.
        hostBuilder.Services.AddSingleton(
            new Mock<IStoreManagerFactory>(MockBehavior.Strict).Object
        );

        return hostBuilder.Build();
    }

    public static TheoryData<string> SettingNames { get; } =
        new(
            typeof(OpenIdSettingNames)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => field.GetRawConstantValue())
                .OfType<string>()
        );

    #region AddIdentityServer Tests

    [Theory]
    [MemberData(nameof(SettingNames))]
    public void AddIdentityServer_RegistersADescriptorForEverySettingName(string settingName)
    {
        // Setting names are a closed vocabulary: a name without a descriptor cannot be configured, stored, or loaded.
        using var host = BuildHost([]);
        var descriptors = host.Services.GetRequiredService<ISettingDescriptorCollectionProvider>();

        Assert.True(
            descriptors.Collection.TryGet(settingName, out _),
            $"The setting '{settingName}' has no registered descriptor."
        );
    }

    [Fact]
    public void AddIdentityServer_WhenServerSettingsCanBeHonored_BindsThem()
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["OpenId:Server:Settings:grant_types_supported:0"] = "authorization_code",
                ["OpenId:Server:Settings:session_management_supported"] = "true",
                ["OpenId:Server:Settings:acr_values_supported"] = null,
            }
        );

        var options = host.Services.GetRequiredService<IOptions<OpenIdOptions>>().Value;

        Assert.Equal("OpenId:Server:Settings", options.Server.Settings?.Path);
    }

    [Theory]
    [InlineData("tenant_issuer", "https://op.example")]
    [InlineData("session_management_supported", "yes")]
    [InlineData("grant_types_supported", "authorization_code")]
    public async Task AddIdentityServer_WhenServerSettingCannotBeHonored_FailsToStart(
        string name,
        string value
    )
    {
        using var host = BuildHost(
            new Dictionary<string, string?> { [$"OpenId:Server:Settings:{name}"] = value }
        );

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync()
        );

        Assert.Contains(
            $"OpenId:Server:Settings:{name}",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddIdentityServer_EveryDefaultIsInItsOwnDomain()
    {
        // A default is set in code and never passes an entry point, so the composed catalog pins it to its own rule.
        using var host = BuildHost([]);
        var descriptors = host.Services.GetRequiredService<ISettingDescriptorCollectionProvider>();

        foreach (var descriptor in descriptors.Collection.Where(x => x.HasDefault))
        {
            var setting = descriptor.CreateDefault();
            var valid = descriptor.TryValidate(setting.GetValue(), out var failure);

            Assert.True(valid, $"The default of '{descriptor.Name}' {failure}.");
        }
    }

    [Theory]
    [InlineData("security_profile", "fapi-2")]
    [InlineData("subject_type", "pairwse")]
    [InlineData("token_endpoint_auth_method", "client_secret")]
    [InlineData("token_endpoint_auth_method", "")]
    [InlineData("refresh_token_expiration_policy", "none")]
    [InlineData("backchannel_token_delivery_mode", "pull")]
    [InlineData("access_token_type", "JWT")]
    [InlineData("access_token_lifetime", "-00:05:00")]
    [InlineData("request_uri_fetch_timeout", "00:00:00")]
    [InlineData("user_code_length", "4")]
    [InlineData("jwks_uri_allowed_hosts:0", "10.0.0")]
    [InlineData("grant_types_supported:0", "authorisation_code")]
    [InlineData("token_endpoint_auth_methods_supported:0", "client_secret")]
    [InlineData("code_challenge_methods_supported:0", "plain")]
    [InlineData("id_token_signing_alg_values_supported:0", "HS256")]
    [InlineData("request_object_signing_alg_values_supported:0", "none")]
    [InlineData("id_token_encryption_alg_values_supported:0", "dir")]
    public async Task AddIdentityServer_WhenServerSettingIsOutsideItsDomain_FailsToStart(
        string key,
        string value
    )
    {
        using var host = BuildHost(
            new Dictionary<string, string?> { [$"OpenId:Server:Settings:{key}"] = value }
        );

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync()
        );

        var settingName = key.Split(':')[0];
        Assert.Contains(
            $"OpenId:Server:Settings:{settingName}' must",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddIdentityServer_WhenServerSettingsAreInTheirDomains_BindsThem()
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["OpenId:Server:Settings:security_profile"] = "fapi2",
                ["OpenId:Server:Settings:token_endpoint_auth_method"] = "private_key_jwt",
                ["OpenId:Server:Settings:id_token_signing_alg_values_supported:0"] = "ES256",
                ["OpenId:Server:Settings:request_object_signing_alg_values_supported:0"] = "HS256",
                ["OpenId:Server:Settings:grant_types_supported:0"] =
                    "urn:ietf:params:oauth:grant-type:device_code",
                ["OpenId:Server:Settings:jwks_uri_allowed_hosts:0"] = "keys.example.com",
                ["OpenId:Server:Settings:user_code_length"] = "12",
                ["OpenId:Server:Settings:code_challenge_methods_supported:0"] = "S256",
                ["OpenId:Server:Settings:require_pkce"] = "false",
                ["OpenId:Server:Settings:pkce_nonce_exemption"] = "true",
            }
        );

        // Resolving the options runs their validators, which read the server settings through the descriptors.
        var options = host.Services.GetRequiredService<IOptions<OpenIdOptions>>().Value;

        Assert.Equal("OpenId:Server:Settings", options.Server.Settings?.Path);
    }

    #endregion
}
