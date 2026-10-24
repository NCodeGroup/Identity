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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class Fapi1AdvancedSecurityProfileTests : BaseTests
{
    #region Properties Tests

    [Fact]
    public void ProfileName_IsFapi1Advanced()
    {
        var profile = new Fapi1AdvancedSecurityProfile();

        Assert.Equal(OpenIdConstants.SecurityProfiles.Fapi1Advanced, profile.ProfileName);
    }

    [Fact]
    public void Requirements_RequireAConfidentialClientAJwtSecuredCodeResponseANonceOrStateAndTheIssuerAudience()
    {
        var profile = new Fapi1AdvancedSecurityProfile();

        Assert.Collection(
            profile.Requirements,
            requirement => Assert.IsType<ConfidentialClientRequirement>(requirement),
            requirement => Assert.IsType<JwtSecuredAuthorizationResponseRequirement>(requirement),
            requirement => Assert.IsType<NonceOrStateRequirement>(requirement),
            requirement => Assert.IsType<IssuerOnlyRequestObjectAudienceRequirement>(requirement)
        );
    }

    #endregion

    #region ApplyCeilingSettings Tests

    [Fact]
    public void ApplyCeilingSettings_TurnsOffLoopbackPortChanges()
    {
        var mockCeiling = CreateLooseMock<ISecurityProfileCeilingBuilder>();
        mockCeiling.Setup(x => x.Set(OpenIdSettingKeys.AllowLoopbackRedirect, false)).Verifiable();

        new Fapi1AdvancedSecurityProfile().ApplyCeilingSettings(mockCeiling.Object);
    }

    [Fact]
    public void ApplyCeilingSettings_PermitsOnlyHttpsRedirectUris()
    {
        var mockCeiling = CreateLooseMock<ISecurityProfileCeilingBuilder>();
        mockCeiling
            .Setup(x =>
                x.Set(
                    OpenIdSettingKeys.RedirectUriKindsSupported,
                    It.Is<IReadOnlyCollection<string>>(kinds =>
                        kinds.SequenceEqual(new[] { OpenIdConstants.RedirectUriKinds.Https })
                    )
                )
            )
            .Verifiable();

        new Fapi1AdvancedSecurityProfile().ApplyCeilingSettings(mockCeiling.Object);
    }

    [Fact]
    public void ApplyCeilingSettings_SetsEveryFapi1AdvancedCeiling()
    {
        const string ps256 = "PS256";
        const string es256 = "ES256";

        var recorder = new RecordingBuilder();

        new Fapi1AdvancedSecurityProfile().ApplyCeilingSettings(recorder);

        Assert.Equal(23, recorder.Values.Count);

        Assert.True(
            Assert.IsType<bool>(
                recorder.Values[OpenIdSettingKeys.AccessTokenSenderConstraintRequired.SettingName]
            )
        );
        Assert.True(
            Assert.IsType<bool>(
                recorder.Values[
                    OpenIdSettingKeys.RequireTlsClientCertificateBoundAccessTokens.SettingName
                ]
            )
        );
        Assert.True(
            Assert.IsType<bool>(recorder.Values[OpenIdSettingKeys.RequireCodeChallenge.SettingName])
        );
        Assert.True(
            Assert.IsType<bool>(
                recorder.Values[OpenIdSettingKeys.RequirePushedAuthorizationRequests.SettingName]
            )
        );
        Assert.True(
            Assert.IsType<bool>(recorder.Values[OpenIdSettingKeys.RequireRequestObject.SettingName])
        );
        Assert.True(
            Assert.IsType<bool>(
                recorder.Values[OpenIdSettingKeys.RequestObjectNotBeforeRequired.SettingName]
            )
        );
        Assert.False(
            Assert.IsType<bool>(
                recorder.Values[OpenIdSettingKeys.AllowLoopbackRedirect.SettingName]
            )
        );

        string[] signingAlgorithms = [ps256, es256];
        Assert.Equal<string>(
            signingAlgorithms,
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[OpenIdSettingKeys.IdTokenSigningAlgValuesSupported.SettingName]
            )
        );
        Assert.Equal<string>(
            signingAlgorithms,
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported.SettingName]
            )
        );
        Assert.Equal<string>(
            signingAlgorithms,
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[OpenIdSettingKeys.UserInfoSigningAlgValuesSupported.SettingName]
            )
        );
        Assert.Equal<string>(
            signingAlgorithms,
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[
                    OpenIdSettingKeys.IntrospectionSigningAlgValuesSupported.SettingName
                ]
            )
        );
        Assert.Equal<string>(
            signingAlgorithms,
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[
                    OpenIdSettingKeys.AuthorizationSigningAlgValuesSupported.SettingName
                ]
            )
        );
        Assert.Equal<string>(
            signingAlgorithms,
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[
                    OpenIdSettingKeys.RequestObjectSigningAlgValuesSupported.SettingName
                ]
            )
        );
        Assert.Equal<string>(
            signingAlgorithms,
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[
                    OpenIdSettingKeys.TokenEndpointAuthSigningAlgValuesSupported.SettingName
                ]
            )
        );

        Assert.Equal<string>(
            [
                OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
                OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth,
                OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
            ],
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[OpenIdSettingKeys.TokenEndpointAuthMethodsSupported.SettingName]
            )
        );
        Assert.Equal<string>(
            [OpenIdConstants.ResponseTypes.Code, OpenIdConstants.ResponseTypes.CodeIdToken],
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[OpenIdSettingKeys.ResponseTypesSupported.SettingName]
            )
        );
        Assert.Equal<string>(
            [OpenIdConstants.CodeChallengeMethods.S256],
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[OpenIdSettingKeys.CodeChallengeMethodsSupported.SettingName]
            )
        );
        Assert.Equal<string>(
            [OpenIdConstants.RedirectUriKinds.Https],
            Assert.IsAssignableFrom<IReadOnlyCollection<string>>(
                recorder.Values[OpenIdSettingKeys.RedirectUriKindsSupported.SettingName]
            )
        );

        Assert.Equal(
            TimeSpan.FromSeconds(60),
            Assert.IsType<TimeSpan>(
                recorder.Values[OpenIdSettingKeys.AuthorizationCodeLifetime.SettingName]
            )
        );
        Assert.Equal(
            TimeSpan.FromSeconds(90),
            Assert.IsType<TimeSpan>(
                recorder.Values[OpenIdSettingKeys.PushedAuthorizationRequestLifetime.SettingName]
            )
        );
        Assert.Equal(
            TimeSpan.FromSeconds(60),
            Assert.IsType<TimeSpan>(recorder.Values[OpenIdSettingKeys.ClockSkew.SettingName])
        );
        Assert.Equal(
            TimeSpan.FromMinutes(5),
            Assert.IsType<TimeSpan>(
                recorder.Values[OpenIdSettingKeys.ClientAssertionMaxLifetime.SettingName]
            )
        );
        Assert.Equal(
            TimeSpan.FromMinutes(60),
            Assert.IsType<TimeSpan>(
                recorder.Values[OpenIdSettingKeys.RequestObjectMaxLifetime.SettingName]
            )
        );
    }

    #endregion

    #region ApplyDefaultSettings Tests

    [Fact]
    public void ApplyDefaultSettings_SignsTheResponsesWithPs256ByDefault()
    {
        const string ps256 = "PS256";

        var recorder = new RecordingBuilder();

        new Fapi1AdvancedSecurityProfile().ApplyDefaultSettings(recorder);

        Assert.Equal(4, recorder.Values.Count);
        Assert.Equal(
            ps256,
            Assert.IsType<string>(
                recorder.Values[OpenIdSettingKeys.IdTokenSignedResponseAlg.SettingName]
            )
        );
        Assert.Equal(
            ps256,
            Assert.IsType<string>(
                recorder.Values[OpenIdSettingKeys.IntrospectionSignedResponseAlg.SettingName]
            )
        );
        Assert.Equal(
            ps256,
            Assert.IsType<string>(
                recorder.Values[OpenIdSettingKeys.AuthorizationSignedResponseAlg.SettingName]
            )
        );
        Assert.True(
            Assert.IsType<bool>(
                recorder.Values[OpenIdSettingKeys.TlsClientCertificateBoundAccessTokens.SettingName]
            )
        );
    }

    #endregion

    private sealed class RecordingBuilder
        : ISecurityProfileCeilingBuilder,
            ISecurityProfileDefaultsBuilder
    {
        public Dictionary<string, object> Values { get; } = new(StringComparer.Ordinal);

        public void Set<TValue>(SettingKey<TValue> key, TValue value)
            where TValue : notnull => Values[key.SettingName] = value;
    }
}
