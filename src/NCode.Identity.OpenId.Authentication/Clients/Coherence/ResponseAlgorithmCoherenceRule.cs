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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks that a client's effective settings permit every algorithm it registers for the JWTs addressed to it — its ID
/// tokens, UserInfo responses, introspection responses, and authorization responses — as issuance selects them: a
/// registered signing algorithm must be in its signing list, and a registered key-management algorithm and its content
/// encryption, <c>A128CBC-HS256</c> unless registered, in the encryption lists. An unset list is unrestricted.
/// </summary>
// See ADR-0155 and ADR-0206.
internal sealed class ResponseAlgorithmCoherenceRule : IClientCoherenceRule
{
    private static ClientJwtProtectionSettings[] Responses { get; } =
    [
        ClientJwtProtectionSettings.IdToken,
        ClientJwtProtectionSettings.UserInfo,
        ClientJwtProtectionSettings.Introspection,
        ClientJwtProtectionSettings.Authorization,
    ];

    private static string[] AlgorithmSettingNames { get; } =
    [
        .. Responses.SelectMany(response =>
            new[]
            {
                response.SignedResponseAlg.SettingName,
                response.EncryptedResponseAlg.SettingName,
                response.EncryptedResponseEnc.SettingName,
                response.SigningAlgValuesSupported.SettingName,
                response.EncryptionAlgValuesSupported.SettingName,
                response.EncryptionEncValuesSupported.SettingName,
            }
        ),
    ];

    /// <summary>
    /// Gets the names of the registered algorithm settings and of the lists that permit them, for each response.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => AlgorithmSettingNames;

    /// <summary>
    /// Finds every algorithm a client registers that its effective <paramref name="clientSettings"/> do not permit.
    /// </summary>
    /// <param name="clientSettings">The effective settings of the client.</param>
    /// <returns>The violations, or an empty list when every registered algorithm is permitted.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(
        IReadOnlySettingCollection clientSettings
    )
    {
        List<ClientCoherenceViolation> violations = [];

        foreach (var response in Responses)
        {
            if (
                clientSettings.TryGetValue(response.SignedResponseAlg, out var signingAlgorithm)
                && !string.IsNullOrEmpty(signingAlgorithm)
            )
            {
                AddIfNotPermitted(
                    violations,
                    clientSettings,
                    response.SignedResponseAlg,
                    signingAlgorithm,
                    response.SigningAlgValuesSupported
                );
            }

            if (
                !clientSettings.TryGetValue(response.EncryptedResponseAlg, out var keyAlgorithm)
                || string.IsNullOrEmpty(keyAlgorithm)
            )
            {
                continue;
            }

            AddIfNotPermitted(
                violations,
                clientSettings,
                response.EncryptedResponseAlg,
                keyAlgorithm,
                response.EncryptionAlgValuesSupported
            );

            var contentAlgorithm =
                clientSettings.TryGetValue(response.EncryptedResponseEnc, out var registeredEnc)
                && !string.IsNullOrEmpty(registeredEnc)
                    ? registeredEnc
                    : AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256;

            AddIfNotPermitted(
                violations,
                clientSettings,
                response.EncryptedResponseEnc,
                contentAlgorithm,
                response.EncryptionEncValuesSupported
            );
        }

        return violations;
    }

    /// <summary>
    /// Adds a violation when the list that permits a registered algorithm is set and does not contain it.
    /// </summary>
    private static void AddIfNotPermitted(
        List<ClientCoherenceViolation> violations,
        IReadOnlySettingCollection clientSettings,
        SettingKey<string> registeredKey,
        string registeredValue,
        SettingKey<IReadOnlyCollection<string>> permittedKey
    )
    {
        if (
            clientSettings.TryGetValue(permittedKey, out var permitted)
            && !permitted.Contains(registeredValue, StringComparer.Ordinal)
        )
        {
            violations.Add(
                new ClientCoherenceViolation(
                    registeredKey.SettingName,
                    registeredValue,
                    permittedKey.SettingName
                )
            );
        }
    }
}
