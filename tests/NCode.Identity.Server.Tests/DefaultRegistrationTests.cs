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
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DefaultRegistrationTests
{
    private static IHost BuildHost(Dictionary<string, string?> configurationData) =>
        BuildHost(configurationData, _ => { });

    private static IHost BuildHost(
        Dictionary<string, string?> configurationData,
        Action<IServiceCollection> configureServices
    )
    {
        var hostBuilder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        // The static tenant is served only beneath its issuer, so a host that starts must configure one.
        hostBuilder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StaticSingle:Issuer"] = "https://op.example",
            }
        );
        hostBuilder.Configuration.AddInMemoryCollection(configurationData);
        hostBuilder.Services.AddIdentityServer().AddConfiguration(hostBuilder.Configuration);

        // These tests exercise configuration only; the persistence provider a host composes is never called.
        hostBuilder.Services.AddSingleton(
            new Mock<IStoreManagerFactory>(MockBehavior.Strict).Object
        );

        configureServices(hostBuilder.Services);

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

    [Fact]
    public void AddIdentityServer_RegistersTheOpenIdSettingScopeCascade()
    {
        // The server's settings cascade into every tenant's, and a tenant's into its clients' and resource servers'.
        using var host = BuildHost([]);
        var scopes = host.Services.GetRequiredService<ISettingScopeCollectionProvider>().Collection;

        Assert.True(scopes.TryGet(OpenIdSettingScopes.Server, out var server));
        Assert.Null(server.ParentName);
        Assert.True(scopes.TryGet(OpenIdSettingScopes.Tenant, out var tenant));
        Assert.Equal(OpenIdSettingScopes.Server, tenant.ParentName);
        Assert.True(scopes.TryGet(OpenIdSettingScopes.Client, out var client));
        Assert.Equal(OpenIdSettingScopes.Tenant, client.ParentName);
        Assert.True(scopes.TryGet(OpenIdSettingScopes.ResourceServer, out var resourceServer));
        Assert.Equal(OpenIdSettingScopes.Tenant, resourceServer.ParentName);
    }

    [Fact]
    public async Task AddIdentityServer_RegistersAnExpiringKeySetForTheSingleUseStores()
    {
        // The DPoP and client-assertion stores record into the shared-state port, which the composition root backs.
        using var host = BuildHost([]);
        var keySet = host.Services.GetRequiredService<IExpiringKeySet>();
        var expiresWhen = DateTimeOffset.UtcNow.AddMinutes(1);

        Assert.True(await keySet.TryAddAsync("key", expiresWhen, CancellationToken.None));
        Assert.False(await keySet.TryAddAsync("key", expiresWhen, CancellationToken.None));
        Assert.NotNull(host.Services.GetRequiredService<IDPoPNonceService>());
        Assert.NotNull(host.Services.GetRequiredService<IDPoPProofReplayCache>());
        Assert.NotNull(host.Services.GetRequiredService<IClientAssertionReplayCache>());
    }

    [Fact]
    public async Task AddIdentityServer_RegistersAnExpiringCounterSet()
    {
        using var host = BuildHost([]);
        var counterSet = host.Services.GetRequiredService<IExpiringCounterSet>();
        var expiresWhen = DateTimeOffset.UtcNow.AddMinutes(1);

        Assert.Equal(
            1,
            await counterSet.IncrementAsync("key", expiresWhen, CancellationToken.None)
        );
        Assert.Equal(
            2,
            await counterSet.IncrementAsync("key", expiresWhen, CancellationToken.None)
        );
    }

    [Fact]
    public void AddIdentityServer_RegistersTheClientJwtCredentialSelectorBesideTheTokenService()
    {
        // A replacement ITokenService resolves the selector to protect ID tokens exactly as the client registered.
        using var host = BuildHost([]);

        Assert.NotNull(host.Services.GetRequiredService<ITokenService>());
        Assert.NotNull(host.Services.GetRequiredService<IClientJwtCredentialSelector>());
    }

    [Fact]
    public void AddIdentityServer_WhenAClientJwtCredentialSelectorIsRegistered_KeepsIt()
    {
        var selector = new Mock<IClientJwtCredentialSelector>(MockBehavior.Strict).Object;
        var services = new ServiceCollection();
        services.AddSingleton(selector);

        services.AddIdentityServer();

        using var serviceProvider = services.BuildServiceProvider();
        Assert.Same(selector, serviceProvider.GetRequiredService<IClientJwtCredentialSelector>());
    }

    [Fact]
    public void AddIdentityServer_EveryDescriptorAppliesOnlyToRegisteredScopes()
    {
        // A setting that names no registered scope could never be configured, and a misspelled scope name is never read.
        using var host = BuildHost([]);
        var descriptors = host.Services.GetRequiredService<ISettingDescriptorCollectionProvider>();
        var scopes = host.Services.GetRequiredService<ISettingScopeCollectionProvider>().Collection;

        foreach (var descriptor in descriptors.Collection)
        {
            Assert.False(
                descriptor.AppliesTo.IsDefaultOrEmpty,
                $"The setting '{descriptor.Name}' declares no scope it applies to."
            );
            foreach (var scopeName in descriptor.AppliesTo)
            {
                Assert.True(
                    scopes.TryGet(scopeName, out _),
                    $"The setting '{descriptor.Name}' applies to the unregistered scope '{scopeName}'."
                );
            }
        }
    }

    [Fact]
    public void AddIdentityServer_EveryDiscoverableSettingAppliesToTenants()
    {
        // Discovery advertises a tenant's effective settings, so a discoverable setting is read at the tenant scope.
        using var host = BuildHost([]);
        var descriptors = host.Services.GetRequiredService<ISettingDescriptorCollectionProvider>();

        foreach (var descriptor in descriptors.Collection.Where(x => x.IsDiscoverable))
        {
            Assert.True(
                descriptor.AppliesTo.Contains(OpenIdSettingScopes.Tenant),
                $"The discoverable setting '{descriptor.Name}' does not apply to tenants."
            );
        }
    }

    [Theory]
    [InlineData(OpenIdSettingNames.SecurityProfiles, OpenIdSettingScopes.Client, true)]
    [InlineData(OpenIdSettingNames.SecurityProfiles, OpenIdSettingScopes.ResourceServer, false)]
    [InlineData(OpenIdSettingNames.RequireCodeChallenge, OpenIdSettingScopes.ResourceServer, false)]
    [InlineData(OpenIdSettingNames.AccessTokenEncryptionRequired, OpenIdSettingScopes.Client, true)]
    [InlineData(
        OpenIdSettingNames.AccessTokenEncryptionRequired,
        OpenIdSettingScopes.ResourceServer,
        true
    )]
    [InlineData(
        OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported,
        OpenIdSettingScopes.ResourceServer,
        true
    )]
    [InlineData(OpenIdSettingNames.AccessTokenLifetime, OpenIdSettingScopes.ResourceServer, true)]
    [InlineData(
        OpenIdSettingNames.AccessTokenSigningAlgValuesSupported,
        OpenIdSettingScopes.ResourceServer,
        true
    )]
    [InlineData(
        OpenIdSettingNames.AccessTokenSenderConstraintRequired,
        OpenIdSettingScopes.Client,
        true
    )]
    [InlineData(
        OpenIdSettingNames.AccessTokenSenderConstraintRequired,
        OpenIdSettingScopes.ResourceServer,
        true
    )]
    [InlineData(
        OpenIdSettingNames.AccessTokenConfirmationMethodsSupported,
        OpenIdSettingScopes.ResourceServer,
        true
    )]
    [InlineData(
        OpenIdSettingNames.AccessTokenConfirmationMethodsSupported,
        OpenIdSettingScopes.Client,
        false
    )]
    [InlineData(OpenIdSettingNames.AccessTokenType, OpenIdSettingScopes.ResourceServer, false)]
    [InlineData(OpenIdSettingNames.SessionIdleTimeout, OpenIdSettingScopes.Client, false)]
    [InlineData(OpenIdSettingNames.SessionIdleTimeout, OpenIdSettingScopes.Tenant, true)]
    [InlineData(OpenIdSettingNames.SubjectClaimTypes, OpenIdSettingScopes.Client, false)]
    [InlineData(
        OpenIdSettingNames.SendProfileMetadataInUserInfo,
        OpenIdSettingScopes.Client,
        false
    )]
    [InlineData(OpenIdSettingNames.SendProfileMetadataInIdToken, OpenIdSettingScopes.Client, true)]
    [InlineData(OpenIdSettingNames.ServiceDocumentation, OpenIdSettingScopes.Client, false)]
    [InlineData(
        OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported,
        OpenIdSettingScopes.Client,
        true
    )]
    [InlineData(OpenIdSettingNames.TokenEndpointAuthMethod, OpenIdSettingScopes.Client, true)]
    [InlineData(OpenIdSettingNames.CodeChallengeMethodsSupported, OpenIdSettingScopes.Client, true)]
    [InlineData(OpenIdSettingNames.ClientName, OpenIdSettingScopes.ResourceServer, false)]
    public void AddIdentityServer_ASettingAppliesWithinTheScopesThatReadIt(
        string settingName,
        string scopeName,
        bool expected
    )
    {
        using var host = BuildHost([]);
        var descriptors = host.Services.GetRequiredService<ISettingDescriptorCollectionProvider>();
        var scopes = host.Services.GetRequiredService<ISettingScopeCollectionProvider>().Collection;

        Assert.True(descriptors.Collection.TryGet(settingName, out var descriptor));
        Assert.Equal(expected, scopes.AppliesWithin(descriptor, scopeName));
    }

    [Fact]
    public void AddIdentityServer_WhenAnExtensionRegistersASettingScope_ItsSettingsCascadeFromTheTenant()
    {
        // An extension that stores settings for an entity of its own registers the entity's scope and its settings;
        // nothing in the library changes, and the scopes above the new one accept the settings while its siblings do
        // not (ADR-0149, ADR-0150).
        const string Organization = "organization";
        using var host = BuildHost(
            [],
            serviceCollection =>
            {
                serviceCollection.AddSingleton<ICollectionDataSource<SettingScopeDescriptor>>(
                    new StaticDataSource<SettingScopeDescriptor>([
                        new SettingScopeDescriptor
                        {
                            Name = Organization,
                            ParentName = OpenIdSettingScopes.Tenant,
                        },
                    ])
                );
                serviceCollection.AddSingleton<ICollectionDataSource<SettingDescriptor>>(
                    new StaticDataSource<SettingDescriptor>([
                        new SettingDescriptor<bool>
                        {
                            Name = "organization_mfa_required",
                            AppliesTo = [Organization],
                            Default = false,
                            OnMerge = SettingMerge.Or,
                        },
                    ])
                );
            }
        );
        var serializer = host.Services.GetRequiredService<ISettingSerializer>();
        var document = JsonElement.Parse("""{"organization_mfa_required":true}""");
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        Assert.Empty(
            serializer.ValidateSettings(document, OpenIdSettingScopes.Server, jsonOptions)
        );
        Assert.Empty(
            serializer.ValidateSettings(document, OpenIdSettingScopes.Tenant, jsonOptions)
        );
        Assert.Empty(serializer.ValidateSettings(document, Organization, jsonOptions));
        var error = Assert.Single(
            serializer.ValidateSettings(document, OpenIdSettingScopes.Client, jsonOptions)
        );
        Assert.Equal("organization_mfa_required", error.SettingName);
    }

    [Fact]
    public void AddIdentityServer_EverySettingThatAppliesWithinResourceServersMergesWithALatticeOperation()
    {
        // Access-token issuance combines a resource server's settings with the client's and those of every other
        // resource server the token targets, as siblings rather than parent and child; only a lattice merge combines
        // them in any order and binds both (ADR-0158).
        using var host = BuildHost([]);
        var descriptors = host.Services.GetRequiredService<ISettingDescriptorCollectionProvider>();
        var scopes = host.Services.GetRequiredService<ISettingScopeCollectionProvider>().Collection;

        var resourceServerSettings = descriptors
            .Collection.Where(descriptor =>
                scopes.AppliesWithin(descriptor, OpenIdSettingScopes.ResourceServer)
            )
            .ToList();

        Assert.NotEmpty(resourceServerSettings);
        foreach (var descriptor in resourceServerSettings)
        {
            Assert.True(
                descriptor.HasLatticeMerge,
                $"The setting '{descriptor.Name}' applies to resource servers but does not merge with a lattice operation."
            );
        }
    }

    [Fact]
    public void AddIdentityServer_EveryRegisteredSecurityProfileExpandsWithLatticeCeilings()
    {
        // A profile's ceiling is set in code, so the composed server pins every registered profile: each ceiling names a
        // registered setting that merges with a lattice operation, so it binds every scope below the one selecting it.
        using var host = BuildHost([]);
        var profiles = host.Services.GetServices<ISecurityProfile>().ToList();
        var catalog = host.Services.GetRequiredService<ISecurityProfileCatalog>();
        var settingCollectionFactory =
            host.Services.GetRequiredService<ISettingCollectionFactory>();

        Assert.NotEmpty(profiles);
        foreach (var profile in profiles)
        {
            var scopeSettings = settingCollectionFactory.Create();
            scopeSettings.Set(OpenIdSettingKeys.SecurityProfiles, [profile.ProfileName]);

            var expanded = settingCollectionFactory.Create(catalog.Expand(scopeSettings));

            Assert.True(expanded.TryGetValue(OpenIdSettingKeys.SecurityProfiles, out var selected));
            Assert.Contains(profile.ProfileName, selected);
        }
    }

    [Theory]
    [InlineData("security_profiles:0", "fapi-2")]
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
    [InlineData("access_token_confirmation_methods_supported:0", "x5t")]
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
                ["OpenId:Server:Settings:security_profiles:0"] = "fapi2",
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

    [Fact]
    public void AddIdentityServer_WhenTenantKeysAreConfigured_BindsThem()
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["OpenId:Tenant:KeyProvisioning:Keys:0:Algorithm"] = "ES384",
                ["OpenId:Tenant:KeyProvisioning:Keys:1:Algorithm"] = "RSA-OAEP-256",
                ["OpenId:Tenant:KeyProvisioning:Keys:1:KeySizeBits"] = "4096",
            }
        );

        // Resolving the options runs the key-provisioning validator against the registered algorithms and key types.
        var provisioning = host
            .Services.GetRequiredService<IOptions<OpenIdOptions>>()
            .Value.Tenant.KeyProvisioning;

        Assert.True(provisioning.Enabled);
        Assert.Collection(
            provisioning.Keys,
            key =>
            {
                Assert.Equal("ES384", key.Algorithm);
                Assert.Null(key.KeySizeBits);
            },
            key =>
            {
                Assert.Equal("RSA-OAEP-256", key.Algorithm);
                Assert.Equal(4096, key.KeySizeBits);
            }
        );
    }

    [Theory]
    [InlineData("XX999", null, "'XX999' is not a registered signature or key-management algorithm")]
    [InlineData("HS256", null, "'HS256' uses a symmetric key")]
    [InlineData("RS256", "1024", "'1024' bits is not valid for algorithm 'RS256'")]
    public async Task AddIdentityServer_WhenATenantKeyCannotBeProvisioned_FailsToStart(
        string algorithm,
        string? keySizeBits,
        string expectedReason
    )
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["OpenId:Tenant:KeyProvisioning:Keys:0:Algorithm"] = algorithm,
                ["OpenId:Tenant:KeyProvisioning:Keys:0:KeySizeBits"] = keySizeBits,
            }
        );

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync()
        );

        Assert.Contains("Tenant:KeyProvisioning", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expectedReason, exception.Message, StringComparison.Ordinal);
    }

    #endregion

    private sealed class StaticDataSource<T>(IEnumerable<T> items) : ICollectionDataSource<T>
    {
        public IEnumerable<T> Collection { get; } = [.. items];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }
}
