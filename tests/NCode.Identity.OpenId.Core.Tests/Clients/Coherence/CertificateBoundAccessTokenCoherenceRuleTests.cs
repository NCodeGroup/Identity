// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Coherence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Coherence;

public class CertificateBoundAccessTokenCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }

    public CertificateBoundAccessTokenCoherenceRuleTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
    }

    #region Helpers

    private void SetupSettings(bool required, bool supported)
    {
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RequireTlsClientCertificateBoundAccessTokens))
            .Returns(required);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TlsClientCertificateBoundAccessTokens))
            .Returns(supported);
    }

    private static ClientCoherenceContext CreateContext(
        IReadOnlySettingCollection effectiveSettings
    ) => new() { RegisteredSettings = effectiveSettings, EffectiveSettings = effectiveSettings };

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenRequiredAndSupported_ReturnsEmpty()
    {
        SetupSettings(required: true, supported: true);

        var violations = new CertificateBoundAccessTokenCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenRequiredButNotSupported_ReturnsTheViolation()
    {
        SetupSettings(required: true, supported: false);

        var violations = new CertificateBoundAccessTokenCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens,
                "true",
                OpenIdSettingNames.TlsClientCertificateBoundAccessTokens
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenNotRequired_ReturnsEmpty()
    {
        SetupSettings(required: false, supported: false);

        var violations = new CertificateBoundAccessTokenCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    #endregion

    #region SettingNames Tests

    [Fact]
    public void SettingNames_ListsTheRequirementAndTheSupport()
    {
        var names = new CertificateBoundAccessTokenCoherenceRule().SettingNames;

        Assert.Equal(2, names.Count);
        Assert.Contains(OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens, names);
        Assert.Contains(OpenIdSettingNames.TlsClientCertificateBoundAccessTokens, names);
    }

    #endregion
}
