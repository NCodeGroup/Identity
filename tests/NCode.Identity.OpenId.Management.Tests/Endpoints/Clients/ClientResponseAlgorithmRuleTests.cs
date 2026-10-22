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

using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public class ClientResponseAlgorithmRuleTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; } = new(MockBehavior.Loose);

    private Mock<IReadOnlySettingCollection> MockCurrentSettings { get; } = new(MockBehavior.Loose);

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

    private void SetupCurrentValue(SettingKey<string> key, string value)
    {
        string? output = value;
        MockCurrentSettings.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

    private void SetupCurrentValues(
        SettingKey<IReadOnlyCollection<string>> key,
        params string[] values
    )
    {
        IReadOnlyCollection<string>? output = values;
        MockCurrentSettings.Setup(x => x.TryGetValue(key, out output)).Returns(true);
    }

    #endregion

    #region FindViolations Tests

    [Fact]
    public void FindViolations_WhenNothingIsRegistered_ReturnsEmpty()
    {
        SetupValues(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, "PS256");

        var violations = ClientResponseAlgorithmRule.FindViolations(MockSettings.Object);

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheSigningListIsUnset_ReturnsEmpty()
    {
        SetupValue(OpenIdSettingKeys.UserInfoSignedResponseAlg, "RS256");

        var violations = ClientResponseAlgorithmRule.FindViolations(MockSettings.Object);

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheSigningAlgorithmIsPermitted_ReturnsEmpty()
    {
        SetupValue(OpenIdSettingKeys.UserInfoSignedResponseAlg, "PS256");
        SetupValues(OpenIdSettingKeys.UserInfoSigningAlgValuesSupported, "PS256", "ES256");

        var violations = ClientResponseAlgorithmRule.FindViolations(MockSettings.Object);

        Assert.Empty(violations);
    }

    [Fact]
    public void FindViolations_WhenTheSigningAlgorithmIsNotPermitted_ReturnsTheViolation()
    {
        SetupValue(OpenIdSettingKeys.IntrospectionSignedResponseAlg, "RS256");
        SetupValues(OpenIdSettingKeys.IntrospectionSigningAlgValuesSupported, "PS256");

        var violations = ClientResponseAlgorithmRule.FindViolations(MockSettings.Object);

        var violation = Assert.Single(violations);
        Assert.Equal(
            new ClientResponseAlgorithmViolation(
                OpenIdSettingKeys.IntrospectionSignedResponseAlg,
                "RS256",
                OpenIdSettingKeys.IntrospectionSigningAlgValuesSupported
            ),
            violation
        );
    }

    [Fact]
    public void FindViolations_WhenTheKeyAlgorithmIsNotPermitted_ReturnsTheViolation()
    {
        SetupValue(OpenIdSettingKeys.UserInfoEncryptedResponseAlg, "RSA1_5");
        SetupValues(OpenIdSettingKeys.UserInfoEncryptionAlgValuesSupported, "RSA-OAEP-256");

        var violations = ClientResponseAlgorithmRule.FindViolations(MockSettings.Object);

        var violation = Assert.Single(violations);
        Assert.Equal(OpenIdSettingKeys.UserInfoEncryptedResponseAlg, violation.RegisteredKey);
        Assert.Equal("RSA1_5", violation.Value);
        Assert.Equal(
            OpenIdSettingKeys.UserInfoEncryptionAlgValuesSupported,
            violation.PermittedKey
        );
    }

    [Fact]
    public void FindViolations_WhenTheContentAlgorithmIsUnregistered_ChecksTheDefault()
    {
        SetupValue(OpenIdSettingKeys.UserInfoEncryptedResponseAlg, "RSA-OAEP-256");
        SetupValues(OpenIdSettingKeys.UserInfoEncryptionAlgValuesSupported, "RSA-OAEP-256");
        SetupValues(OpenIdSettingKeys.UserInfoEncryptionEncValuesSupported, "A256GCM");

        var violations = ClientResponseAlgorithmRule.FindViolations(MockSettings.Object);

        var violation = Assert.Single(violations);
        Assert.Equal(OpenIdSettingKeys.UserInfoEncryptedResponseEnc, violation.RegisteredKey);
        Assert.Equal("A128CBC-HS256", violation.Value);
        Assert.Equal(
            OpenIdSettingKeys.UserInfoEncryptionEncValuesSupported,
            violation.PermittedKey
        );
    }

    [Fact]
    public void FindViolations_WhenTheContentAlgorithmIsRegisteredWithoutAKeyAlgorithm_ReturnsEmpty()
    {
        SetupValue(OpenIdSettingKeys.UserInfoEncryptedResponseEnc, "A128GCM");
        SetupValues(OpenIdSettingKeys.UserInfoEncryptionEncValuesSupported, "A256GCM");

        var violations = ClientResponseAlgorithmRule.FindViolations(MockSettings.Object);

        Assert.Empty(violations);
    }

    #endregion

    #region ReadsChangedSetting Tests

    [Fact]
    public void ReadsChangedSetting_WhenAPermittedListChanges_ReturnsTrue()
    {
        SetupValues(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, "PS256");
        SetupCurrentValues(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, "PS256", "ES256");

        var changed = ClientResponseAlgorithmRule.ReadsChangedSetting(
            MockSettings.Object,
            MockCurrentSettings.Object
        );

        Assert.True(changed);
    }

    [Fact]
    public void ReadsChangedSetting_WhenARegisteredAlgorithmChanges_ReturnsTrue()
    {
        SetupValue(OpenIdSettingKeys.UserInfoSignedResponseAlg, "RS256");
        SetupCurrentValue(OpenIdSettingKeys.UserInfoSignedResponseAlg, "PS256");

        var changed = ClientResponseAlgorithmRule.ReadsChangedSetting(
            MockSettings.Object,
            MockCurrentSettings.Object
        );

        Assert.True(changed);
    }

    [Fact]
    public void ReadsChangedSetting_WhenASettingIsRemoved_ReturnsTrue()
    {
        SetupValue(OpenIdSettingKeys.UserInfoEncryptedResponseAlg, "RSA-OAEP-256");

        var changed = ClientResponseAlgorithmRule.ReadsChangedSetting(
            MockSettings.Object,
            MockCurrentSettings.Object
        );

        Assert.True(changed);
    }

    [Fact]
    public void ReadsChangedSetting_WhenOnlyTheOrderOfAListChanges_ReturnsFalse()
    {
        SetupValues(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, "PS256", "ES256");
        SetupCurrentValues(OpenIdSettingKeys.IdTokenSigningAlgValuesSupported, "ES256", "PS256");

        var changed = ClientResponseAlgorithmRule.ReadsChangedSetting(
            MockSettings.Object,
            MockCurrentSettings.Object
        );

        Assert.False(changed);
    }

    [Fact]
    public void ReadsChangedSetting_WhenNoSettingItReadsChanges_ReturnsFalse()
    {
        SetupValue(OpenIdSettingKeys.UserInfoSignedResponseAlg, "PS256");
        SetupCurrentValue(OpenIdSettingKeys.UserInfoSignedResponseAlg, "PS256");
        SetupValues(OpenIdSettingKeys.UserInfoSigningAlgValuesSupported, "PS256");
        SetupCurrentValues(OpenIdSettingKeys.UserInfoSigningAlgValuesSupported, "PS256");
        SetupValue(OpenIdSettingKeys.DefaultAcr, "urn:acr:one");
        SetupCurrentValue(OpenIdSettingKeys.DefaultAcr, "urn:acr:two");

        var changed = ClientResponseAlgorithmRule.ReadsChangedSetting(
            MockSettings.Object,
            MockCurrentSettings.Object
        );

        Assert.False(changed);
    }

    #endregion
}
