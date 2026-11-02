// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Coherence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Coherence;

public class TokenEndpointAuthMethodCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }

    public TokenEndpointAuthMethodCoherenceRuleTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns("client_secret_basic");
    }

    #region Helpers

    private void SetupValues(SettingKey<IReadOnlyCollection<string>> key, params string[] values)
    {
        IReadOnlyCollection<string>? output = values;
        MockSettings.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

    private static ClientCoherenceContext CreateContext(
        IReadOnlySettingCollection effectiveSettings
    ) => new() { RegisteredSettings = effectiveSettings, EffectiveSettings = effectiveSettings };

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenTheCeilingPermitsTheMethod_ReturnsEmpty()
    {
        SetupValues(
            OpenIdSettingKeys.TokenEndpointAuthMethodsSupported,
            "private_key_jwt",
            "client_secret_basic"
        );

        var violations = new TokenEndpointAuthMethodCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheCeilingExcludesTheMethod_ReturnsTheViolation()
    {
        SetupValues(OpenIdSettingKeys.TokenEndpointAuthMethodsSupported, "private_key_jwt");

        var violations = new TokenEndpointAuthMethodCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.TokenEndpointAuthMethod,
                "client_secret_basic",
                OpenIdSettingNames.TokenEndpointAuthMethodsSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenTheCeilingIsUnset_ReturnsEmpty()
    {
        var violations = new TokenEndpointAuthMethodCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    #endregion

    #region SettingNames Tests

    [Fact]
    public void SettingNames_ListsTheMethodAndItsCeiling()
    {
        var names = new TokenEndpointAuthMethodCoherenceRule().SettingNames;

        Assert.Equal(2, names.Count);
        Assert.Contains(OpenIdSettingNames.TokenEndpointAuthMethod, names);
        Assert.Contains(OpenIdSettingNames.TokenEndpointAuthMethodsSupported, names);
    }

    #endregion
}
