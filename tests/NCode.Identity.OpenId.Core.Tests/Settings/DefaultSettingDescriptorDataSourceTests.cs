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

using Microsoft.Extensions.DependencyInjection;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class DefaultSettingDescriptorDataSourceTests : BaseTests
{
    // The algorithm rules consult the registered algorithms, so the descriptors are evaluated against the real JOSE
    // algorithm registrations.
    private static IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        CreateAlgorithmCollectionProvider();

    private static IAlgorithmCollectionProvider CreateAlgorithmCollectionProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        return serviceCollection
            .BuildServiceProvider()
            .GetRequiredService<IAlgorithmCollectionProvider>();
    }

    private DefaultSettingDescriptorDataSource CreateDataSource(params string[] profileNames)
    {
        var profiles = profileNames.Select(ISecurityProfile (name) => new FakeProfile(name));

        return new DefaultSettingDescriptorDataSource(
            CreateStrictMock<INullChangeToken>().Object,
            AlgorithmCollectionProvider,
            profiles,
            [
                new HttpsRedirectUriKind(),
                new LoopbackRedirectUriKind(),
                new HttpRedirectUriKind(),
                new PrivateUseRedirectUriKind(),
            ]
        );
    }

    private static SettingDescriptor GetDescriptor(
        DefaultSettingDescriptorDataSource dataSource,
        string settingName
    ) => dataSource.Collection.Single(descriptor => descriptor.Name == settingName);

    #region Collection Property Tests

    [Theory]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "fapi2" }, true)]
    [InlineData(new[] { "fapi2", "custom" }, true)]
    [InlineData(new[] { "fapi2", "fapi2" }, true)]
    [InlineData(new[] { "fapi-2" }, false)]
    [InlineData(new[] { "FAPI2" }, false)]
    [InlineData(new[] { "" }, false)]
    [InlineData(new[] { "fapi2", "other" }, false)]
    public void Collection_SecurityProfiles_AcceptsOnlyRegisteredProfiles(
        string[] value,
        bool expected
    )
    {
        var dataSource = CreateDataSource("fapi2", "custom");
        var descriptor = GetDescriptor(dataSource, OpenIdSettingNames.SecurityProfiles);

        var valid = descriptor.TryValidate((IReadOnlyCollection<string>)value, out _);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Collection_SecurityProfiles_WhenNoProfileIsRegistered_AcceptsOnlyAnEmptyList()
    {
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor(dataSource, OpenIdSettingNames.SecurityProfiles);

        var emptyIsValid = descriptor.TryValidate((IReadOnlyCollection<string>)[], out _);
        var fapi2IsValid = descriptor.TryValidate(
            (IReadOnlyCollection<string>)["fapi2"],
            out var failure
        );

        Assert.True(emptyIsValid);
        Assert.False(fapi2IsValid);
        Assert.Equal("must be empty, because no security profile is registered", failure);
    }

    [Theory]
    [InlineData(new[] { "fapi2" }, new string[0], new[] { "fapi2" })]
    [InlineData(new[] { "fapi2" }, new[] { "custom" }, new[] { "fapi2", "custom" })]
    [InlineData(new[] { "custom" }, new[] { "fapi2" }, new[] { "custom", "fapi2" })]
    [InlineData(new string[0], new[] { "custom" }, new[] { "custom" })]
    [InlineData(new[] { "custom" }, new[] { "custom" }, new[] { "custom" })]
    public void Collection_SecurityProfiles_ChildCannotRemoveAProfileItsParentSelects(
        string[] parent,
        string[] child,
        string[] expected
    )
    {
        var dataSource = CreateDataSource("fapi2", "custom");
        var descriptor = Assert.IsType<SettingDescriptor<IReadOnlyCollection<string>>>(
            GetDescriptor(dataSource, OpenIdSettingNames.SecurityProfiles)
        );
        var parentSetting = descriptor.Create(parent);
        var childSetting = descriptor.Create(child);

        var merged = descriptor.Merge(parentSetting, childSetting);

        Assert.Equal(expected, merged.Value);
    }

    [Theory]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "https" }, true)]
    [InlineData(new[] { "https", "loopback", "private_use" }, true)]
    [InlineData(new[] { "HTTPS" }, false)]
    [InlineData(new[] { "claimed_https" }, false)]
    [InlineData(new[] { "" }, false)]
    public void Collection_RedirectUriKindsSupported_AcceptsOnlyRegisteredKinds(
        string[] value,
        bool expected
    )
    {
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor(dataSource, OpenIdSettingNames.RedirectUriKindsSupported);

        var valid = descriptor.TryValidate((IReadOnlyCollection<string>)value, out _);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Collection_RedirectUriKindsSupported_IntersectsDownTheCascade()
    {
        var dataSource = CreateDataSource();
        var descriptor = Assert.IsType<SettingDescriptor<IReadOnlyCollection<string>>>(
            GetDescriptor(dataSource, OpenIdSettingNames.RedirectUriKindsSupported)
        );
        var parentSetting = descriptor.Create(["https", "loopback"]);
        var childSetting = descriptor.Create(["loopback", "private_use"]);

        var merged = descriptor.Merge(parentSetting, childSetting);

        Assert.Equal(new[] { "loopback" }, merged.Value);
    }

    [Fact]
    public void Collection_TlsClientCertificateBoundAccessTokens_IsTenantSupportAChildReplaces()
    {
        var dataSource = CreateDataSource();
        var descriptor = Assert.IsType<SettingDescriptor<bool>>(
            GetDescriptor(dataSource, OpenIdSettingNames.TlsClientCertificateBoundAccessTokens)
        );
        var parentSetting = descriptor.Create(true);
        var childSetting = descriptor.Create(false);

        var merged = descriptor.Merge(parentSetting, childSetting);

        Assert.Equal([OpenIdSettingScopes.Tenant], descriptor.AppliesTo.AsEnumerable());
        Assert.True(descriptor.IsDiscoverable);
        Assert.False(merged.Value);
    }

    [Fact]
    public void Collection_RequireTlsClientCertificateBoundAccessTokens_IsAClientRequirementAParentFloors()
    {
        var dataSource = CreateDataSource();
        var descriptor = Assert.IsType<SettingDescriptor<bool>>(
            GetDescriptor(
                dataSource,
                OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens
            )
        );
        var parentSetting = descriptor.Create(true);
        var childSetting = descriptor.Create(false);

        var merged = descriptor.Merge(parentSetting, childSetting);

        Assert.Equal([OpenIdSettingScopes.Client], descriptor.AppliesTo.AsEnumerable());
        Assert.False(descriptor.IsDiscoverable);
        Assert.True(merged.Value);
    }

    [Fact]
    public void Collection_RefreshTokenRotationPolicy_DefaultsToUnboundAndAChildReplaces()
    {
        var dataSource = CreateDataSource();
        var descriptor = Assert.IsType<SettingDescriptor<string>>(
            GetDescriptor(dataSource, OpenIdSettingNames.RefreshTokenRotationPolicy)
        );
        var parentSetting = descriptor.Create("always");
        var childSetting = descriptor.Create("unbound");

        var merged = descriptor.Merge(parentSetting, childSetting);

        Assert.Equal("unbound", descriptor.Default);
        Assert.Equal([OpenIdSettingScopes.Client], descriptor.AppliesTo.AsEnumerable());
        Assert.False(descriptor.IsDiscoverable);
        Assert.Equal("unbound", merged.Value);
    }

    [Fact]
    public void Collection_RefreshTokenReuseInterval_IsOffByDefaultAndAChildReplaces()
    {
        var dataSource = CreateDataSource();
        var descriptor = Assert.IsType<SettingDescriptor<TimeSpan>>(
            GetDescriptor(dataSource, OpenIdSettingNames.RefreshTokenReuseInterval)
        );
        var parentSetting = descriptor.Create(TimeSpan.FromSeconds(10));
        var childSetting = descriptor.Create(TimeSpan.FromSeconds(30));

        var merged = descriptor.Merge(parentSetting, childSetting);

        Assert.Equal(TimeSpan.Zero, descriptor.Default);
        Assert.Equal([OpenIdSettingScopes.Client], descriptor.AppliesTo.AsEnumerable());
        Assert.False(descriptor.IsDiscoverable);
        Assert.Equal(TimeSpan.FromSeconds(30), merged.Value);
    }

    [Fact]
    public void Collection_EveryDefaultIsInItsOwnDomain()
    {
        var dataSource = CreateDataSource("fapi2");

        foreach (var descriptor in dataSource.Collection.Where(descriptor => descriptor.HasDefault))
        {
            var setting = descriptor.CreateDefault();
            var valid = descriptor.TryValidate(setting.GetValue(), out var failure);

            Assert.True(valid, $"The default of '{descriptor.Name}' {failure}.");
        }
    }

    [Theory]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseAlg, "RSA-OAEP-256", true)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseAlg, "ECDH-ES+A128KW", true)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseAlg, "A128KW", false)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseAlg, "PBES2-HS256+A128KW", false)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseAlg, "RSA1_5", false)]
    [InlineData(OpenIdSettingNames.IdTokenEncryptedResponseAlg, "dir", false)]
    [InlineData(OpenIdSettingNames.IdTokenSignedResponseAlg, "PS256", true)]
    [InlineData(OpenIdSettingNames.IdTokenSignedResponseAlg, "HS256", false)]
    [InlineData(OpenIdSettingNames.IdTokenSignedResponseAlg, "none", false)]
    [InlineData(OpenIdSettingNames.UserInfoSignedResponseAlg, "ES256", true)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseEnc, "A256GCM", true)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseEnc, "RSA-OAEP-256", false)]
    public void Collection_ResponseProtectionRegistration_AcceptsOnlyItsDomain(
        string settingName,
        string value,
        bool expected
    )
    {
        // A response is signed with an asymmetric key and encrypted to the client's public key (ADR-0155).
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor(dataSource, settingName);

        var valid = descriptor.TryValidate(value, out _);

        Assert.Equal(expected, valid);
    }

    [Theory]
    [InlineData(OpenIdSettingNames.IdTokenEncryptionAlgValuesSupported, "RSA-OAEP", true)]
    [InlineData(OpenIdSettingNames.IdTokenEncryptionAlgValuesSupported, "A256KW", false)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported, "ECDH-ES", true)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported, "A128GCMKW", false)]
    [InlineData(OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported, "RSA-OAEP-256", true)]
    [InlineData(OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported, "A128KW", false)]
    [InlineData(OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported, "A128KW", true)]
    [InlineData(OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported, "RSA1_5", false)]
    [InlineData(OpenIdSettingNames.RequestObjectSigningAlgValuesSupported, "HS256", true)]
    [InlineData(
        OpenIdSettingNames.BackchannelAuthenticationRequestSigningAlgValuesSupported,
        "PS256",
        true
    )]
    [InlineData(
        OpenIdSettingNames.BackchannelAuthenticationRequestSigningAlgValuesSupported,
        "HS256",
        false
    )]
    public void Collection_AlgorithmList_AcceptsOnlyItsDomain(
        string settingName,
        string value,
        bool expected
    )
    {
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor(dataSource, settingName);

        var valid = descriptor.TryValidate((IReadOnlyCollection<string>)[value], out _);

        Assert.Equal(expected, valid);
    }

    [Theory]
    [InlineData(OpenIdSettingNames.IdTokenSignedResponseAlg, "RS256")]
    [InlineData(OpenIdSettingNames.IdTokenEncryptedResponseEnc, "A128CBC-HS256")]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseEnc, "A128CBC-HS256")]
    public void Collection_ResponseProtectionRegistration_DefaultsToTheOpenIdRegistrationDefault(
        string settingName,
        string expected
    )
    {
        // OpenID Connect Dynamic Client Registration 1.0 §2 defines these defaults.
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor(dataSource, settingName);

        Assert.True(descriptor.HasDefault);
        Assert.Equal(expected, descriptor.CreateDefault().GetValue());
    }

    [Theory]
    [InlineData(OpenIdSettingNames.IdTokenEncryptedResponseAlg)]
    [InlineData(OpenIdSettingNames.UserInfoSignedResponseAlg)]
    [InlineData(OpenIdSettingNames.UserInfoEncryptedResponseAlg)]
    public void Collection_ResponseProtectionRegistration_IsAbsentUnlessRegistered(
        string settingName
    )
    {
        // Absent means the response is not signed or not encrypted, so these have no default.
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor(dataSource, settingName);

        Assert.False(descriptor.HasDefault);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(300, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void Collection_AuthorizationResponseLifetime_AcceptsOneSecondToTenMinutes(
        int seconds,
        bool expected
    )
    {
        // JARM §2.1 recommends a short lifetime, so the setting is capped at 10 minutes.
        var descriptor = GetDescriptor(
            CreateDataSource(),
            OpenIdSettingNames.AuthorizationResponseLifetime
        );

        var valid = descriptor.TryValidate(TimeSpan.FromSeconds(seconds), out _);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Collection_DoesNotDescribeTheClientAuthenticationMethod()
    {
        // The registered client-authentication handlers define its domain, so the authentication slice describes it.
        var dataSource = CreateDataSource();

        Assert.DoesNotContain(
            dataSource.Collection,
            descriptor => descriptor.Name == OpenIdSettingNames.TokenEndpointAuthMethod
        );
    }

    #endregion

    #region Scaffolding

    private sealed class FakeProfile(string name) : ISecurityProfile
    {
        public string ProfileName => name;
        public IReadOnlyCollection<ISecurityProfileRequirement> Requirements => [];

        public void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling) { }

        public void ApplyDefaultSettings(ISecurityProfileDefaultsBuilder defaults) { }
    }

    #endregion
}
