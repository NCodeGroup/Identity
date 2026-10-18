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

using JetBrains.Annotations;
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.OpenId.Authentication.ResourceServers;

/// <summary>
/// The result of resolving the resource servers an access token targets, used to bind the token's audience and — when
/// it targets a single resource server — encrypt it to that resource server's own key. When the token
/// targets exactly one resource server, <see cref="SingleSecretsProvider"/> exposes that resource server's own secret
/// keys; otherwise it is <c>null</c> and the token uses the tenant's keys. The transient provider is owned by this
/// instance and released on disposal. Access tokens are always signed with the tenant's key, never a resource server's.
/// </summary>
// See ADR-0075.
[PublicAPI]
public sealed class ResourceServerCredentialResolution : IAsyncDisposable
{
    /// <summary>
    /// Gets the resource servers the token targets, ordered by audience identifier. Empty when no resource server owns
    /// any of the token's effective scopes (for example, a scopeless token).
    /// </summary>
    public required IReadOnlyList<ResolvedResourceServer> ResourceServers { get; init; }

    /// <summary>
    /// Gets the secret-key collection provider for the single targeted resource server, or <c>null</c> when the token
    /// targets zero or more than one resource server. Owned by this instance.
    /// </summary>
    public ISecretKeyCollectionProvider? SingleSecretsProvider { get; init; }

    /// <summary>
    /// Gets the single targeted resource server, or <c>null</c> when the token targets zero or more than one resource
    /// server. Per-audience encryption material is only well-defined for a single-audience token.
    /// </summary>
    public ResolvedResourceServer? Single => ResourceServers.Count == 1 ? ResourceServers[0] : null;

    /// <summary>
    /// Gets the distinct audience identifiers of the targeted resource servers, ordered by identifier.
    /// </summary>
    public IReadOnlyList<string> Audiences =>
        ResourceServers.Select(resourceServer => resourceServer.Identifier).ToList();

    /// <inheritdoc />
    public ValueTask DisposeAsync() =>
        SingleSecretsProvider?.DisposeAsync() ?? ValueTask.CompletedTask;
}
