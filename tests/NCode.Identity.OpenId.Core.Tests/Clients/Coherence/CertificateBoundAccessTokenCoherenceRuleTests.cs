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

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenRequiredAndSupported_ReturnsEmpty()
    {
        SetupSettings(required: true, supported: true);

        var violations = new CertificateBoundAccessTokenCoherenceRule().FindViolations(
            MockSettings.Object
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenRequiredButNotSupported_ReturnsTheViolation()
    {
        SetupSettings(required: true, supported: false);

        var violations = new CertificateBoundAccessTokenCoherenceRule().FindViolations(
            MockSettings.Object
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
            MockSettings.Object
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
