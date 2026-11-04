// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyRolloverOptionsValidatorTests : BaseTests
{
    private Mock<ISettingDescriptorCollectionProvider> MockDescriptorProvider { get; }
    private Mock<ISettingDescriptorCollection> MockDescriptors { get; }
    private Mock<ISettingScopeCollectionProvider> MockScopeProvider { get; }
    private Mock<ISettingScopeCollection> MockScopes { get; }
    private TenantKeyRolloverOptionsValidator Validator { get; }

    public TenantKeyRolloverOptionsValidatorTests()
    {
        MockDescriptorProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        MockDescriptors = CreateStrictMock<ISettingDescriptorCollection>();
        MockScopeProvider = CreateStrictMock<ISettingScopeCollectionProvider>();
        MockScopes = CreateStrictMock<ISettingScopeCollection>();
        // Every validation reads both collections before it walks the names, so each setup is verified.
        MockDescriptorProvider
            .Setup(x => x.Collection)
            .Returns(MockDescriptors.Object)
            .Verifiable();
        MockScopeProvider.Setup(x => x.Collection).Returns(MockScopes.Object).Verifiable();
        Validator = new TenantKeyRolloverOptionsValidator(
            MockDescriptorProvider.Object,
            MockScopeProvider.Object
        );
    }

    #region Validate Tests

    [Fact]
    public void Validate_WithTheDefaultTokenLifetimeSettings_Succeeds()
    {
        // The token lifetimes apply to clients, whose settings a tenant's effective settings cascade into. The
        // registration fills the defaults into the empty list when configuration names none.
        var options = new OpenIdOptions();
        options.Tenant.KeyRollover.TokenLifetimeSettings.AddRange(
            OpenIdTenantKeyRolloverOptions.DefaultTokenLifetimeSettings
        );
        Assert.NotEmpty(options.Tenant.KeyRollover.TokenLifetimeSettings);
        foreach (var settingName in options.Tenant.KeyRollover.TokenLifetimeSettings)
        {
            SetupDescriptor(
                settingName,
                CreateDescriptor<TimeSpan>(settingName, OpenIdSettingScopes.Client),
                appliesWithinTenant: true
            );
        }

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenTheIntervalIsNotATimerPeriod_FailsNamingTheSection(int seconds)
    {
        var options = new OpenIdOptions
        {
            Tenant =
            {
                KeyRollover =
                {
                    Interval = TimeSpan.FromSeconds(seconds),
                    TokenLifetimeSettings = [],
                },
            },
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith(
            "Tenant:KeyRollover:Interval must be between",
            failure,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenATokenLifetimeSettingIsBlank_FailsNamingTheSection()
    {
        var options = new OpenIdOptions
        {
            Tenant = { KeyRollover = { TokenLifetimeSettings = [" "] } },
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.Equal(
            "Tenant:KeyRollover:TokenLifetimeSettings must name a setting in every entry.",
            failure
        );
    }

    [Fact]
    public void Validate_WhenATokenLifetimeSettingIsNotRegistered_FailsNamingTheEntry()
    {
        // A misspelled name would otherwise be ignored, and the grace would stop covering that token's lifetime.
        var options = new OpenIdOptions
        {
            Tenant = { KeyRollover = { TokenLifetimeSettings = ["acess_token_lifetime"] } },
        };
        SetupDescriptor("acess_token_lifetime", null);

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.Equal(
            "Tenant:KeyRollover:TokenLifetimeSettings names 'acess_token_lifetime', which is not a registered setting that a tenant's effective settings read as a duration.",
            failure
        );
    }

    [Fact]
    public void Validate_WhenATokenLifetimeSettingIsNotADuration_FailsNamingTheEntry()
    {
        var options = new OpenIdOptions
        {
            Tenant = { KeyRollover = { TokenLifetimeSettings = ["max_concurrent_sessions"] } },
        };
        SetupDescriptor(
            "max_concurrent_sessions",
            CreateDescriptor<int>("max_concurrent_sessions"),
            appliesWithinTenant: true
        );

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.Contains("'max_concurrent_sessions'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenATokenLifetimeSettingDoesNotApplyWithinATenant_FailsNamingTheEntry()
    {
        var options = new OpenIdOptions
        {
            Tenant = { KeyRollover = { TokenLifetimeSettings = ["server_only_lifetime"] } },
        };
        SetupDescriptor(
            "server_only_lifetime",
            CreateDescriptor<TimeSpan>("server_only_lifetime", OpenIdSettingScopes.Server),
            appliesWithinTenant: false
        );

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.Contains("'server_only_lifetime'", failure, StringComparison.Ordinal);
    }

    #endregion

    #region Helpers

    private static SettingDescriptor<TValue> CreateDescriptor<TValue>(
        string settingName,
        string scope = OpenIdSettingScopes.Tenant
    )
        where TValue : notnull =>
        new() { Name = settingName, AppliesTo = ImmutableArray.Create(scope) };

    private void SetupDescriptor(
        string settingName,
        SettingDescriptor? descriptor,
        bool appliesWithinTenant = false
    )
    {
        MockDescriptors
            .Setup(x => x.TryGet(settingName, out descriptor))
            .Returns(descriptor is not null)
            .Verifiable();
        if (descriptor is not null && descriptor.ValueType == typeof(TimeSpan))
        {
            MockScopes
                .Setup(x => x.AppliesWithin(descriptor, OpenIdSettingScopes.Tenant))
                .Returns(appliesWithinTenant)
                .Verifiable();
        }
    }

    #endregion
}
