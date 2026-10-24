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

public class IdTokenEncryptionCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }

    public IdTokenEncryptionCoherenceRuleTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
    }

    #region Helpers

    private void SetupRequired(bool required)
    {
        var output = required;
        MockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.IdTokenEncryptionRequired, out output))
            .Returns(true);
    }

    private void SetupAlgorithm(string algorithm)
    {
        string? output = algorithm;
        MockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.IdTokenEncryptedResponseAlg, out output))
            .Returns(true);
    }

    private static ClientCoherenceContext CreateContext(
        IReadOnlySettingCollection effectiveSettings
    ) => new() { RegisteredSettings = effectiveSettings, EffectiveSettings = effectiveSettings };

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenEncryptionIsRequiredWithoutAnAlgorithm_ReportsTheMissingAlgorithm()
    {
        SetupRequired(true);

        var violations = new IdTokenEncryptionCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(
            ClientCoherenceViolation.Missing(
                OpenIdSettingNames.IdTokenEncryptedResponseAlg,
                OpenIdSettingNames.IdTokenEncryptionRequired,
                "true"
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenEncryptionIsRequiredWithAnAlgorithm_ReturnsEmpty()
    {
        SetupRequired(true);
        SetupAlgorithm("RSA-OAEP");

        var violations = new IdTokenEncryptionCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenEncryptionIsNotRequired_ReturnsEmpty()
    {
        SetupRequired(false);

        var violations = new IdTokenEncryptionCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    #endregion
}
