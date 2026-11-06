// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Defines a service that answers the questions behind the rule that a client's public keys come from one source: the
/// keys registered for it or the keys it publishes at its <c>jwks_uri</c>.
/// </summary>
// See ADR-1161.
internal interface IClientKeySourceChecker
{
    /// <summary>
    /// Determines whether a client's settings document names a <c>jwks_uri</c>.
    /// </summary>
    /// <param name="settings">The client's settings document.</param>
    /// <returns><see langword="true"/> when the document names a non-empty <c>jwks_uri</c>; otherwise, <see langword="false"/>.</returns>
    bool HasJwksUri(JsonElement settings);

    /// <summary>
    /// Determines whether a secret type holds a public key.
    /// </summary>
    /// <param name="secretType">The secret type to check.</param>
    /// <returns><see langword="true"/> when the secret type is asymmetric; otherwise, <see langword="false"/>.</returns>
    bool IsPublicKeyType(string secretType);

    /// <summary>
    /// Determines whether any of the secrets holds a public key.
    /// </summary>
    /// <param name="secrets">The secrets to check.</param>
    /// <returns><see langword="true"/> when any secret is of a public key type; otherwise, <see langword="false"/>.</returns>
    bool HasPublicKeys(IEnumerable<PersistedSecret> secrets);
}
