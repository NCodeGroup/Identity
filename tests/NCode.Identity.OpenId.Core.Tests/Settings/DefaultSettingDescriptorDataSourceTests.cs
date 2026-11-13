// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Locales;
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

    private DefaultSettingDescriptorDataSource CreateDataSource(params string[] profileNames) =>
        CreateDataSource(profileNames, uiLocaleTags: ["en"]);

    private DefaultSettingDescriptorDataSource CreateDataSource(
        string[] profileNames,
        string[] uiLocaleTags
    )
    {
        var profiles = profileNames.Select(ISecurityProfile (name) => new FakeProfile(name));
        var uiLocales = uiLocaleTags.Select(IUiLocale (tag) => new FakeUiLocale(tag));

        return new DefaultSettingDescriptorDataSource(
            CreateStrictMock<INullChangeToken>().Object,
            AlgorithmCollectionProvider,
            profiles,
            [
                new HttpsRedirectUriKind(),
                new LoopbackRedirectUriKind(),
                new HttpRedirectUriKind(),
                new PrivateUseRedirectUriKind(),
            ],
            new DefaultUiLocaleCatalog(uiLocales)
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
    public void Collection_RefreshTokenRotationRetryRequired_IsAClientRequirementAParentFloors()
    {
        var dataSource = CreateDataSource();
        var descriptor = Assert.IsType<SettingDescriptor<bool>>(
            GetDescriptor(dataSource, OpenIdSettingNames.RefreshTokenRotationRetryRequired)
        );
        var parentSetting = descriptor.Create(true);
        var childSetting = descriptor.Create(false);

        var merged = descriptor.Merge(parentSetting, childSetting);

        Assert.False(descriptor.Default);
        Assert.Equal([OpenIdSettingScopes.Client], descriptor.AppliesTo.AsEnumerable());
        Assert.False(descriptor.IsDiscoverable);
        Assert.True(merged.Value);
    }

    [Fact]
    public void Collection_SessionAbsoluteLifetime_DefaultsTo30DaysAndAcceptsAtMostAYear()
    {
        var descriptor = Assert.IsType<SettingDescriptor<TimeSpan>>(
            GetDescriptor(CreateDataSource(), OpenIdSettingNames.SessionAbsoluteLifetime)
        );

        Assert.Equal(TimeSpan.FromDays(30), descriptor.Default);
        Assert.False(descriptor.TryValidate(TimeSpan.Zero, out _));
        Assert.True(descriptor.TryValidate(TimeSpan.FromDays(365), out _));
        Assert.False(descriptor.TryValidate(TimeSpan.FromDays(366), out _));
    }

    [Fact]
    public void Collection_SessionIdleTimeout_DefaultsTo3DaysAndAcceptsAtMost100Days()
    {
        var descriptor = Assert.IsType<SettingDescriptor<TimeSpan>>(
            GetDescriptor(CreateDataSource(), OpenIdSettingNames.SessionIdleTimeout)
        );

        Assert.Equal(TimeSpan.FromDays(3), descriptor.Default);
        Assert.False(descriptor.TryValidate(TimeSpan.Zero, out _));
        Assert.True(descriptor.TryValidate(TimeSpan.FromDays(100), out _));
        Assert.False(descriptor.TryValidate(TimeSpan.FromDays(101), out _));
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
        // A response is signed with an asymmetric key and encrypted to the client's public key (ADR-1155).
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

    [Fact]
    public void Collection_EnvironmentBoundSettings_AreTheEnvironmentsAddressesIdentitiesAndSalt()
    {
        var dataSource = CreateDataSource();

        var actual = dataSource
            .Collection.Where(descriptor => descriptor.IsEnvironmentBound)
            .ToList();

        Assert.Equal(
            new[]
            {
                OpenIdSettingNames.RedirectUris,
                OpenIdSettingNames.PostLogoutRedirectUris,
                OpenIdSettingNames.BackChannelLogoutUri,
                OpenIdSettingNames.FrontChannelLogoutUri,
                OpenIdSettingNames.JwksUri,
                OpenIdSettingNames.SectorIdentifierUri,
                OpenIdSettingNames.ClientNotificationEndpoint,
                OpenIdSettingNames.TlsClientAuthSubjectDn,
                OpenIdSettingNames.TlsClientAuthSanDns,
                OpenIdSettingNames.TlsClientAuthSanUri,
                OpenIdSettingNames.TlsClientAuthSanIp,
                OpenIdSettingNames.TlsClientAuthSanEmail,
                OpenIdSettingNames.TlsClientAuthTrustAnchors,
                OpenIdSettingNames.PairwiseSalt,
            }.Order(),
            actual.Select(descriptor => descriptor.Name).Order()
        );
        Assert.DoesNotContain(actual, descriptor => descriptor.IsLimit);
    }

    [Fact]
    public void GetDescriptors_GrantTypesEnabled_AppliesToPlatformOnlyAndUnions()
    {
        var descriptor = Assert.IsType<SettingDescriptor<IReadOnlyCollection<string>>>(
            GetDescriptor(CreateDataSource(), OpenIdSettingNames.GrantTypesEnabled)
        );

        Assert.Equal([OpenIdSettingScopes.Platform], descriptor.AppliesTo.AsEnumerable());
        Assert.False(descriptor.IsDiscoverable);
        Assert.True(
            descriptor.TryValidate((IReadOnlyCollection<string>)["client_credentials"], out _)
        );
        Assert.False(descriptor.TryValidate((IReadOnlyCollection<string>)["not_a_grant"], out _));
        Assert.Equal(
            ["authorization_code", "client_credentials"],
            descriptor
                .Merge(
                    descriptor.Create(["authorization_code"]),
                    descriptor.Create(["authorization_code", "client_credentials"])
                )
                .Value
        );
    }

    #endregion

    #region UI locale settings

    [Theory]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "en" }, true)]
    [InlineData(new[] { "en", "fr" }, true)]
    [InlineData(new[] { "FR" }, true)]
    [InlineData(new[] { "fr-CA" }, false)]
    [InlineData(new[] { "" }, false)]
    [InlineData(new[] { "en", "it" }, false)]
    public void Collection_UiLocalesSupported_AcceptsOnlyRegisteredUiLocales(
        string[] value,
        bool expected
    )
    {
        var dataSource = CreateDataSource([], ["en", "fr"]);
        var descriptor = GetDescriptor(dataSource, OpenIdSettingNames.UiLocalesSupported);

        var valid = descriptor.TryValidate((IReadOnlyCollection<string>)value, out _);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Collection_UiLocalesSupported_IsADiscoverableTenantCeiling()
    {
        var dataSource = CreateDataSource();
        var descriptor = Assert.IsType<SettingDescriptor<IReadOnlyCollection<string>>>(
            GetDescriptor(dataSource, OpenIdSettingNames.UiLocalesSupported)
        );

        Assert.Equal([OpenIdSettingScopes.Tenant], descriptor.AppliesTo.AsEnumerable());
        Assert.True(descriptor.IsDiscoverable);
        Assert.False(descriptor.HasDefault);
        Assert.Equal(
            ["en"],
            descriptor.Merge(descriptor.Create(["en", "fr"]), descriptor.Create(["en"])).Value
        );
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("en", true)]
    [InlineData("Fr", true)]
    [InlineData("fr-CA", false)]
    [InlineData("it", false)]
    public void Collection_DefaultUiLocale_AcceptsEmptyOrARegisteredUiLocale(
        string value,
        bool expected
    )
    {
        var dataSource = CreateDataSource([], ["en", "fr"]);
        var descriptor = GetDescriptor(dataSource, OpenIdSettingNames.DefaultUiLocale);

        var valid = descriptor.TryValidate(value, out _);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Collection_DefaultUiLocale_IsAnUnsetNonDiscoverableTenantSetting()
    {
        var dataSource = CreateDataSource();
        var descriptor = Assert.IsType<SettingDescriptor<string>>(
            GetDescriptor(dataSource, OpenIdSettingNames.DefaultUiLocale)
        );

        Assert.Equal([OpenIdSettingScopes.Tenant], descriptor.AppliesTo.AsEnumerable());
        Assert.False(descriptor.IsDiscoverable);
        Assert.Equal(string.Empty, descriptor.Default);
        Assert.Equal(
            "fr",
            descriptor.Merge(descriptor.Create("en"), descriptor.Create("fr")).Value
        );
    }

    #endregion

    #region Scaffolding

    private sealed record FakeUiLocale(string LanguageTag) : IUiLocale;

    private sealed class FakeProfile(string name) : ISecurityProfile
    {
        public string ProfileName => name;
        public IReadOnlyCollection<ISecurityProfileRequirement> Requirements => [];

        public void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling) { }

        public void ApplyDefaultSettings(ISecurityProfileDefaultsBuilder defaults) { }
    }

    #endregion
}
