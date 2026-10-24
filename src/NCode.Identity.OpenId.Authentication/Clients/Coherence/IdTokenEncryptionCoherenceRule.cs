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

namespace NCode.Identity.OpenId.Authentication.Clients.Coherence;

/// <summary>
/// Checks that a client's effective settings state the key-management algorithm of its ID tokens,
/// <c>id_token_encrypted_response_alg</c>, whenever they require ID tokens to be encrypted,
/// <c>id_token_encryption_required</c>: without an algorithm no ID token could be issued to the client.
/// </summary>
// See ADR-0155 and ADR-0206.
internal sealed class IdTokenEncryptionCoherenceRule : IClientCoherenceRule
{
    private static string[] EncryptionSettingNames { get; } =
    [OpenIdSettingNames.IdTokenEncryptionRequired, OpenIdSettingNames.IdTokenEncryptedResponseAlg];

    /// <summary>
    /// Gets the names of the setting that requires ID tokens to be encrypted and of the setting that registers the
    /// algorithm that encrypts them.
    /// </summary>
    public IReadOnlyCollection<string> SettingNames => EncryptionSettingNames;

    /// <summary>
    /// Finds the missing algorithm of a client whose effective settings require its ID tokens to be encrypted.
    /// </summary>
    /// <param name="context">The registered and effective settings of the client.</param>
    /// <returns>The violation, or an empty list when encryption is not required or the algorithm is registered.</returns>
    public IReadOnlyList<ClientCoherenceViolation> FindViolations(ClientCoherenceContext context)
    {
        var settings = context.EffectiveSettings;

        if (
            !settings.TryGetValue(OpenIdSettingKeys.IdTokenEncryptionRequired, out var required)
            || !required
        )
        {
            return [];
        }

        if (
            settings.TryGetValue(OpenIdSettingKeys.IdTokenEncryptedResponseAlg, out var algorithm)
            && !string.IsNullOrEmpty(algorithm)
        )
        {
            return [];
        }

        return
        [
            ClientCoherenceViolation.Missing(
                OpenIdSettingNames.IdTokenEncryptedResponseAlg,
                OpenIdSettingNames.IdTokenEncryptionRequired,
                "true"
            ),
        ];
    }
}
