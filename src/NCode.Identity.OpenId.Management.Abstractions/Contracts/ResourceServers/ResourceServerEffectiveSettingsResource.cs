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
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the REST resource for a resource server's effective (merged) settings — the server baseline merged with
/// the tenant's and the resource server's persisted overrides — that the authorization server resolves at runtime.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ResourceServerEffectiveSettingsResource
    : ISupportResourceServerId,
        ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc cref="ISupportResourceServerId.ResourceServerId"/>
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets the read-only merged (server baseline + tenant overrides + resource server overrides) flat JSON settings
    /// object the authorization server resolves at runtime.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
