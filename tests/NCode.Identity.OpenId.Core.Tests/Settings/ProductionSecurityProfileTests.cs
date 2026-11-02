// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class ProductionSecurityProfileTests : BaseTests
{
    #region ProfileName Property Tests

    [Fact]
    public void ProfileName_IsTheProductionProfileName()
    {
        var profile = new ProductionSecurityProfile();

        Assert.Equal(OpenIdConstants.SecurityProfiles.Production, profile.ProfileName);
    }

    #endregion

    #region Requirements Property Tests

    [Fact]
    public void Requirements_IsEmpty()
    {
        var profile = new ProductionSecurityProfile();

        Assert.Empty(profile.Requirements);
    }

    #endregion

    #region ApplyCeilingSettings Tests

    [Fact]
    public void ApplyCeilingSettings_DropsPlainHttpRedirectUris()
    {
        var mockCeiling = CreateLooseMock<ISecurityProfileCeilingBuilder>();
        mockCeiling
            .Setup(x =>
                x.Set(
                    OpenIdSettingKeys.RedirectUriKindsSupported,
                    It.Is<IReadOnlyCollection<string>>(kinds =>
                        kinds.SequenceEqual(
                            new[]
                            {
                                OpenIdConstants.RedirectUriKinds.Https,
                                OpenIdConstants.RedirectUriKinds.Loopback,
                                OpenIdConstants.RedirectUriKinds.PrivateUse,
                            }
                        )
                    )
                )
            )
            .Verifiable();

        new ProductionSecurityProfile().ApplyCeilingSettings(mockCeiling.Object);
    }

    [Fact]
    public void ApplyCeilingSettings_ForbidsAnonymousRegistration()
    {
        var mockCeiling = CreateLooseMock<ISecurityProfileCeilingBuilder>();
        mockCeiling
            .Setup(x => x.Set(OpenIdSettingKeys.RegistrationEndpointAllowAnonymous, false))
            .Verifiable();

        new ProductionSecurityProfile().ApplyCeilingSettings(mockCeiling.Object);
    }

    #endregion

    #region ApplyDefaultSettings Tests

    [Fact]
    public void ApplyDefaultSettings_TurnsOnDeletionProtection()
    {
        var mockDefaults = CreateLooseMock<ISecurityProfileDefaultsBuilder>();
        mockDefaults.Setup(x => x.Set(OpenIdSettingKeys.DeletionProtection, true)).Verifiable();

        new ProductionSecurityProfile().ApplyDefaultSettings(mockDefaults.Object);
    }

    #endregion
}
