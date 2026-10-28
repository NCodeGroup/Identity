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

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides the decryption policy shared by ID-token and logout-token validation: the encryption algorithms the
/// client registered and the keys that decrypt with them.
/// </summary>
internal static class ClientDecryptionPolicy
{
    /// <summary>
    /// Gets a value indicating whether the client registered ID-token encryption, which is the case when it
    /// registered at least one key management algorithm.
    /// </summary>
    public static bool IsEncryptionRegistered(ClientRegistration registration) =>
        registration.IdTokenKeyManagementAlgorithms is { Count: > 0 };

    /// <summary>
    /// Gets the key management algorithms the client registered that it may decrypt with, in the order of the
    /// registration. Only algorithms that use an asymmetric key are permitted, excluding <c>RSA1_5</c>, so the result
    /// is empty when the client registered no encryption.
    /// </summary>
    public static List<string> GetPermittedKeyManagementAlgorithms(
        IAlgorithmCollection algorithms,
        ClientRegistration registration
    )
    {
        if (registration.IdTokenKeyManagementAlgorithms is not { Count: > 0 } registered)
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
            // permitted, and RSA1_5 is vulnerable to padding-oracle attacks (RFC 8725 §3.2) (ADR-0279).
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
        ClientRegistration registration
    )
    {
        if (!IsEncryptionRegistered(registration))
        {
            if (registration.IdTokenContentEncryptionAlgorithms is { Count: > 0 })
                throw new InvalidOperationException(
                    $"The client '{registration.ClientId}' lists ID-token content encryption algorithms but no key management algorithm."
                );

            return [];
        }

        var configured = registration.IdTokenContentEncryptionAlgorithms;
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
}
