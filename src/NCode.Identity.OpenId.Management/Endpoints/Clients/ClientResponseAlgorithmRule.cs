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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Checks that a client's effective settings permit every algorithm it registers for the JWTs addressed to it — its ID
/// tokens, UserInfo responses, and introspection responses — as issuance selects them: a registered signing algorithm
/// must be in its signing list, and a registered key-management algorithm and its content encryption,
/// <c>A128CBC-HS256</c> unless registered, in the encryption lists. An unset list is unrestricted.
/// </summary>
// See ADR-0155. A pure function of the settings, shared by every management write that can change them.
internal static class ClientResponseAlgorithmRule
{
    private static ClientJwtProtectionSettings[] Responses { get; } =
    [
        ClientJwtProtectionSettings.IdToken,
        ClientJwtProtectionSettings.UserInfo,
        ClientJwtProtectionSettings.Introspection,
        ClientJwtProtectionSettings.Authorization,
    ];

    /// <summary>
    /// Finds every algorithm a client registers that its effective <paramref name="settings"/> do not permit.
    /// </summary>
    /// <param name="settings">The client's effective settings.</param>
    /// <returns>The violations, or an empty list when every registered algorithm is permitted.</returns>
    public static IReadOnlyList<ClientResponseAlgorithmViolation> FindViolations(
        IReadOnlySettingCollection settings
    )
    {
        List<ClientResponseAlgorithmViolation> violations = [];

        foreach (var response in Responses)
        {
            if (
                settings.TryGetValue(response.SignedResponseAlg, out var signingAlgorithm)
                && !string.IsNullOrEmpty(signingAlgorithm)
            )
            {
                AddIfNotPermitted(
                    violations,
                    settings,
                    response.SignedResponseAlg,
                    signingAlgorithm,
                    response.SigningAlgValuesSupported
                );
            }

            if (
                !settings.TryGetValue(response.EncryptedResponseAlg, out var keyAlgorithm)
                || string.IsNullOrEmpty(keyAlgorithm)
            )
            {
                continue;
            }

            AddIfNotPermitted(
                violations,
                settings,
                response.EncryptedResponseAlg,
                keyAlgorithm,
                response.EncryptionAlgValuesSupported
            );

            var contentAlgorithm =
                settings.TryGetValue(response.EncryptedResponseEnc, out var registeredEnc)
                && !string.IsNullOrEmpty(registeredEnc)
                    ? registeredEnc
                    : AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256;

            AddIfNotPermitted(
                violations,
                settings,
                response.EncryptedResponseEnc,
                contentAlgorithm,
                response.EncryptionEncValuesSupported
            );
        }

        return violations;
    }

    private static void AddIfNotPermitted(
        List<ClientResponseAlgorithmViolation> violations,
        IReadOnlySettingCollection settings,
        SettingKey<string> registeredKey,
        string registeredValue,
        SettingKey<IReadOnlyCollection<string>> permittedKey
    )
    {
        if (
            settings.TryGetValue(permittedKey, out var permitted)
            && !permitted.Contains(registeredValue, StringComparer.Ordinal)
        )
        {
            violations.Add(
                new ClientResponseAlgorithmViolation(registeredKey, registeredValue, permittedKey)
            );
        }
    }
}
