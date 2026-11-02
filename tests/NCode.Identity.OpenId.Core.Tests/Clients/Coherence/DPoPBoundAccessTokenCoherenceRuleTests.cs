// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Coherence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Coherence;

public class DPoPBoundAccessTokenCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }

    public DPoPBoundAccessTokenCoherenceRuleTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
    }

    #region Helpers

    private void SetupRequired(bool required)
    {
        var output = required;
        MockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.RequireDPoPBoundAccessTokens, out output))
            .Returns(true);
    }

    private void SetupAlgorithms(params string[] algorithms)
    {
        IReadOnlyCollection<string>? output = algorithms;
        MockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.DPoPSigningAlgValuesSupported, out output))
            .Returns(true);
    }

    private static ClientCoherenceContext CreateContext(
        IReadOnlySettingCollection effectiveSettings
    ) => new() { RegisteredSettings = effectiveSettings, EffectiveSettings = effectiveSettings };

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenRequiredAndNoProofAlgorithmIsPermitted_ReportsTheRequirement()
    {
        SetupRequired(true);
        SetupAlgorithms();

        var violations = new DPoPBoundAccessTokenCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.RequireDPoPBoundAccessTokens,
                "true",
                OpenIdSettingNames.DPoPSigningAlgValuesSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenRequiredAndAnAlgorithmIsPermitted_ReturnsEmpty()
    {
        SetupRequired(true);
        SetupAlgorithms("ES256");

        var violations = new DPoPBoundAccessTokenCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheListIsUnset_ReturnsEmpty()
    {
        SetupRequired(true);

        var violations = new DPoPBoundAccessTokenCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenNotRequired_ReturnsEmpty()
    {
        SetupRequired(false);
        SetupAlgorithms();

        var violations = new DPoPBoundAccessTokenCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    #endregion
}
