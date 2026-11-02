// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Clients.Coherence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Coherence;

public class ResponseAlgorithmCoherenceRuleTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }

    public ResponseAlgorithmCoherenceRuleTests()
    {
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
    }

    #region Helpers

    private void SetupValue(SettingKey<string> key, string value)
    {
        string? output = value;
        MockSettings.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

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
    public void FindViolations_WhenNothingIsRegistered_ReturnsEmpty()
    {
        SetupValues(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, "PS256");

        var violations = new ResponseAlgorithmCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheSigningListIsUnset_ReturnsEmpty()
    {
        SetupValue(OpenIdSettingKeys.UserInfoSignedResponseAlg, "RS256");

        var violations = new ResponseAlgorithmCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheSigningAlgorithmIsPermitted_ReturnsEmpty()
    {
        SetupValue(OpenIdSettingKeys.UserInfoSignedResponseAlg, "PS256");
        SetupValues(OpenIdSettingKeys.UserInfoSigningAlgValuesSupported, "PS256", "ES256");

        var violations = new ResponseAlgorithmCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheSigningAlgorithmIsNotPermitted_ReturnsTheViolation()
    {
        SetupValue(OpenIdSettingKeys.IntrospectionSignedResponseAlg, "RS256");
        SetupValues(OpenIdSettingKeys.IntrospectionSigningAlgValuesSupported, "PS256");

        var violations = new ResponseAlgorithmCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientCoherenceViolation(
                OpenIdSettingNames.IntrospectionSignedResponseAlg,
                "RS256",
                OpenIdSettingNames.IntrospectionSigningAlgValuesSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenTheKeyAlgorithmIsNotPermitted_ReturnsTheViolation()
    {
        SetupValue(OpenIdSettingKeys.UserInfoEncryptedResponseAlg, "RSA1_5");
        SetupValues(OpenIdSettingKeys.UserInfoEncryptionAlgValuesSupported, "RSA-OAEP-256");

        var violations = new ResponseAlgorithmCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(OpenIdSettingNames.UserInfoEncryptedResponseAlg, violation.SettingName);
        Assert.Equal("RSA1_5", violation.Value);
        Assert.Equal(
            OpenIdSettingNames.UserInfoEncryptionAlgValuesSupported,
            violation.GoverningSettingName
        );
    }

    [Fact]
    public void FindViolations_WhenTheContentAlgorithmIsUnregistered_ChecksTheDefault()
    {
        SetupValue(OpenIdSettingKeys.UserInfoEncryptedResponseAlg, "RSA-OAEP-256");
        SetupValues(OpenIdSettingKeys.UserInfoEncryptionAlgValuesSupported, "RSA-OAEP-256");
        SetupValues(OpenIdSettingKeys.UserInfoEncryptionEncValuesSupported, "A256GCM");

        var violations = new ResponseAlgorithmCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        var violation = Assert.Single(violations);
        Assert.Equal(OpenIdSettingNames.UserInfoEncryptedResponseEnc, violation.SettingName);
        Assert.Equal("A128CBC-HS256", violation.Value);
        Assert.Equal(
            OpenIdSettingNames.UserInfoEncryptionEncValuesSupported,
            violation.GoverningSettingName
        );
    }

    [Fact]
    public void FindViolations_WhenTheContentAlgorithmIsRegisteredWithoutAKeyAlgorithm_ReturnsEmpty()
    {
        SetupValue(OpenIdSettingKeys.UserInfoEncryptedResponseEnc, "A128GCM");
        SetupValues(OpenIdSettingKeys.UserInfoEncryptionEncValuesSupported, "A256GCM");

        var violations = new ResponseAlgorithmCoherenceRule().FindViolations(
            CreateContext(MockSettings.Object)
        );

        Assert.Empty(violations);
    }

    #endregion

    #region SettingNames Tests

    [Fact]
    public void SettingNames_ListsEveryRegisteredAlgorithmAndItsList()
    {
        var names = new ResponseAlgorithmCoherenceRule().SettingNames;

        Assert.Equal(24, names.Count);
        Assert.Contains(OpenIdSettingNames.IdTokenSignedResponseAlg, names);
        Assert.Contains(OpenIdSettingNames.AuthorizationEncryptionEncValuesSupported, names);
    }

    #endregion
}
