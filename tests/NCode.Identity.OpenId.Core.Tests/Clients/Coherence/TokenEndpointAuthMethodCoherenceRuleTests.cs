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
