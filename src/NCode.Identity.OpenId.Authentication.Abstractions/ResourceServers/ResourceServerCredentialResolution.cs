// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.ResourceServers;

/// <summary>
/// The result of resolving the resource servers an access token targets: they bind the token's audience, their settings
/// bound the token together with the client's, and their own keys are the ones a token encrypted to one of them uses.
/// Each resolved resource server's secret-key provider is owned by this instance and released on disposal. Access tokens
/// are always signed with the tenant's key, never a resource server's.
/// </summary>
// See ADR-1075 and ADR-1154.
[PublicAPI]
public sealed class ResourceServerCredentialResolution : IAsyncDisposable
{
    /// <summary>
    /// Gets the resource servers the token targets, ordered by audience identifier. Empty when no resource server owns
    /// any of the token's effective scopes (for example, a scopeless token).
    /// </summary>
    public required IReadOnlyList<ResolvedResourceServer> ResourceServers { get; init; }

    /// <summary>
    /// Gets the single targeted resource server, or <c>null</c> when the token targets zero or more than one resource
    /// server. A compact JWE has one recipient, so a token can be encrypted to a resource server's own key only when it
    /// targets that resource server alone.
    /// </summary>
    public ResolvedResourceServer? Single => ResourceServers.Count == 1 ? ResourceServers[0] : null;

    /// <summary>
    /// Gets the distinct audience identifiers of the targeted resource servers, ordered by identifier.
    /// </summary>
    public IReadOnlyList<string> Audiences =>
        ResourceServers.Select(resourceServer => resourceServer.Identifier).ToList();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var resourceServer in ResourceServers)
        {
            await resourceServer.SecretsProvider.DisposeAsync();
        }
    }
}
