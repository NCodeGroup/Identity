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

using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;
using NCode.Json;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides the decryption policy shared by the validation of the JWTs the provider issues to the client: the encryption
/// algorithms the client registered for a kind of token and the keys that decrypt with them.
/// </summary>
internal static class ClientDecryptionPolicy
{
    /// <summary>
    /// Gets a value indicating whether the client registered encryption for the kind of token, which is the case when
    /// it registered at least one key management algorithm for it.
    /// </summary>
    public static bool IsEncryptionRegistered(ProviderJwtProfile profile) =>
        profile.KeyManagementAlgorithms is { Count: > 0 };

    /// <summary>
    /// Gets the key management algorithms the client registered that it may decrypt with, in the order of the
    /// registration. Only algorithms that use an asymmetric key are permitted, excluding <c>RSA1_5</c>, so the result
    /// is empty when the client registered no encryption.
    /// </summary>
    public static List<string> GetPermittedKeyManagementAlgorithms(
        IAlgorithmCollection algorithms,
        ProviderJwtProfile profile
    )
    {
        if (profile.KeyManagementAlgorithms is not { Count: > 0 } registered)
            return [];

        var result = new List<string>();
        foreach (var code in registered)
        {
            if (
                !algorithms.TryGetKeyManagementAlgorithm(code, out var algorithm)
                || !typeof(AsymmetricSecretKey).IsAssignableFrom(algorithm.KeyType)
            )
                continue;

            // The OP encrypts only to a public key the client published, so a shared-key algorithm is never
            // permitted, and RSA1_5 is vulnerable to padding-oracle attacks (RFC 8725 §3.2) (ADR-0280).
            // The default JOSE registry has no RSA1_5; this refuses one a host registers itself (ADR-0282).
            if (
                string.Equals(code, AlgorithmCodes.KeyManagement.RsaPkcs1, StringComparison.Ordinal)
            )
                continue;

            result.Add(code);
        }

        return result;
    }

    /// <summary>
    /// Gets the content encryption algorithms the client registered that it may decrypt with, in the order of the
    /// registration, or <c>A128CBC-HS256</c>, the registration default, when it listed none. The result is empty when
    /// the client registered no encryption.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The client lists content encryption algorithms but no key management algorithm.
    /// </exception>
    public static List<string> GetPermittedContentEncryptionAlgorithms(
        IAlgorithmCollection algorithms,
        ProviderJwtProfile profile
    )
    {
        if (!IsEncryptionRegistered(profile))
        {
            if (profile.ContentEncryptionAlgorithms is { Count: > 0 })
                throw new InvalidOperationException(
                    $"The client '{profile.Registration.ClientId}' lists {profile.EncryptionName} content encryption algorithms but no key management algorithm."
                );

            return [];
        }

        var configured = profile.ContentEncryptionAlgorithms;
        if (configured is not { Count: > 0 })
            configured = [AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256];

        var result = new List<string>();
        foreach (var code in configured)
        {
            if (algorithms.TryGetAuthenticatedEncryptionAlgorithm(code, out _))
                result.Add(code);
        }

        return result;
    }

    /// <summary>
    /// Gets the keys that decrypt the tokens encrypted to the client: the asymmetric keys of its key ring, which the
    /// JWT validator selects by their <c>use</c> and the <c>alg</c> of the token.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The client has no key ring, or the key ring is not registered.
    /// </exception>
    public static List<SecretKey> GetDecryptionKeys(
        IClientKeyRingProvider keyRingProvider,
        ProviderJwtProfile profile
    )
    {
        var keyRing = profile.Registration.KeyRing;
        if (string.IsNullOrEmpty(keyRing))
            throw new InvalidOperationException(
                $"The client '{profile.Registration.ClientId}' registers {profile.EncryptionName} encryption but has no key ring."
            );

        var keys = keyRingProvider.GetKeyRing(keyRing);
        return [.. keys.OfType<AsymmetricSecretKey>()];
    }

    /// <summary>
    /// Gets a value indicating whether the <c>iss</c> header parameter that the encryption header replicates, if any,
    /// equals the specified issuer. An unencrypted token or a header without <c>iss</c> is valid.
    /// </summary>
    public static bool IsReplicatedIssuerValid(JsonElement? encryptionHeader, string issuer)
    {
        // A JWE may replicate the claim of the enclosed JWT as a header parameter; when it does, it must agree with
        // the issuer (RFC 7519 §5.3, OpenID Connect Back-Channel Logout 1.0 §2.4).
        if (
            encryptionHeader is not { } header
            || !header.TryGetProperty(JoseClaimNames.Payload.Iss, out _)
        )
            return true;

        return header.TryGetPropertyValue<string>(JoseClaimNames.Payload.Iss, out var replicated)
            && string.Equals(replicated, issuer, StringComparison.Ordinal);
    }
}
