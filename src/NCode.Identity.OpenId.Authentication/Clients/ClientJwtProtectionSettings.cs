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

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Names the settings that govern how the server protects one kind of JWT it addresses to a client: the algorithms the
/// client registers (OpenID Connect Dynamic Client Registration 1.0 §2) and the lists that bound them.
/// </summary>
// See ADR-0155.
internal readonly record struct ClientJwtProtectionSettings
{
    /// <summary>
    /// Gets the settings for an ID token, which also govern a logout token (OpenID Connect Back-Channel Logout 1.0
    /// §2.4). The client's <c>id_token_signed_response_alg</c> defaults to <c>RS256</c>, so an ID token is always signed.
    /// </summary>
    public static ClientJwtProtectionSettings IdToken { get; } =
        new()
        {
            ResponseName = "ID token",
            SignedResponseAlg = OpenIdSettingKeys.IdTokenSignedResponseAlg,
            SigningAlgValuesSupported = OpenIdSettingKeys.IdTokenSigningAlgValuesSupported,
            EncryptedResponseAlg = OpenIdSettingKeys.IdTokenEncryptedResponseAlg,
            EncryptedResponseEnc = OpenIdSettingKeys.IdTokenEncryptedResponseEnc,
            EncryptionAlgValuesSupported = OpenIdSettingKeys.IdTokenEncryptionAlgValuesSupported,
            EncryptionEncValuesSupported = OpenIdSettingKeys.IdTokenEncryptionEncValuesSupported,
            EncryptionRequired = OpenIdSettingKeys.IdTokenEncryptionRequired,
        };

    /// <summary>
    /// Gets the settings for a UserInfo response, which is a JWT only when the client registers a signing or an
    /// encryption algorithm for it (OpenID Connect Core 1.0 §5.3.2).
    /// </summary>
    public static ClientJwtProtectionSettings UserInfo { get; } =
        new()
        {
            ResponseName = "UserInfo response",
            SignedResponseAlg = OpenIdSettingKeys.UserInfoSignedResponseAlg,
            SigningAlgValuesSupported = OpenIdSettingKeys.UserInfoSigningAlgValuesSupported,
            EncryptedResponseAlg = OpenIdSettingKeys.UserInfoEncryptedResponseAlg,
            EncryptedResponseEnc = OpenIdSettingKeys.UserInfoEncryptedResponseEnc,
            EncryptionAlgValuesSupported = OpenIdSettingKeys.UserInfoEncryptionAlgValuesSupported,
            EncryptionEncValuesSupported = OpenIdSettingKeys.UserInfoEncryptionEncValuesSupported,
        };

    /// <summary>
    /// Gets the settings for a JWT introspection response (RFC 9701), addressed to a client acting for a resource
    /// server. It is signed with the client's <c>introspection_signed_response_alg</c>, which defaults to <c>RS256</c>,
    /// and encrypted when the client registers an encryption algorithm for it.
    /// </summary>
    public static ClientJwtProtectionSettings Introspection { get; } =
        new()
        {
            ResponseName = "introspection response",
            SignedResponseAlg = OpenIdSettingKeys.IntrospectionSignedResponseAlg,
            SigningAlgValuesSupported = OpenIdSettingKeys.IntrospectionSigningAlgValuesSupported,
            EncryptedResponseAlg = OpenIdSettingKeys.IntrospectionEncryptedResponseAlg,
            EncryptedResponseEnc = OpenIdSettingKeys.IntrospectionEncryptedResponseEnc,
            EncryptionAlgValuesSupported =
                OpenIdSettingKeys.IntrospectionEncryptionAlgValuesSupported,
            EncryptionEncValuesSupported =
                OpenIdSettingKeys.IntrospectionEncryptionEncValuesSupported,
        };

    /// <summary>
    /// Gets the name of the protected response, used in error descriptions.
    /// </summary>
    public required string ResponseName { get; init; }

    /// <summary>
    /// Gets the key of the signature algorithm the client registers; when the setting is absent, the response is not
    /// signed.
    /// </summary>
    public required SettingKey<string> SignedResponseAlg { get; init; }

    /// <summary>
    /// Gets the key of the list that bounds the registered signature algorithm.
    /// </summary>
    public required SettingKey<IReadOnlyCollection<string>> SigningAlgValuesSupported { get; init; }

    /// <summary>
    /// Gets the key of the key-management algorithm the client registers; when the setting is absent, the response is
    /// not encrypted.
    /// </summary>
    public required SettingKey<string> EncryptedResponseAlg { get; init; }

    /// <summary>
    /// Gets the key of the content-encryption algorithm the client registers.
    /// </summary>
    public required SettingKey<string> EncryptedResponseEnc { get; init; }

    /// <summary>
    /// Gets the key of the list that bounds the registered key-management algorithm.
    /// </summary>
    public required SettingKey<
        IReadOnlyCollection<string>
    > EncryptionAlgValuesSupported { get; init; }

    /// <summary>
    /// Gets the key of the list that bounds the registered content-encryption algorithm.
    /// </summary>
    public required SettingKey<
        IReadOnlyCollection<string>
    > EncryptionEncValuesSupported { get; init; }

    /// <summary>
    /// Gets the key of the policy that requires the response be encrypted, or <c>null</c> when no such policy exists for
    /// the response.
    /// </summary>
    public SettingKey<bool>? EncryptionRequired { get; init; }
}
