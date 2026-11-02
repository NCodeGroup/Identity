// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
