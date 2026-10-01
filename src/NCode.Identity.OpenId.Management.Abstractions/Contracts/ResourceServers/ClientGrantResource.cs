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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the REST resource for a client grant (in Auth0 terms): a client's authorization to a resource server
/// with a set of granted scopes.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientGrantResource : ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the authorized client.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client is authorized to.
    /// </summary>
    public required string ResourceServerId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <summary>
    /// Gets the granted scope values.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }
}
