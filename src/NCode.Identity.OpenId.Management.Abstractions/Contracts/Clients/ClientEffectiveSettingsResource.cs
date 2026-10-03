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

namespace NCode.Identity.OpenId.Management.Contracts.Clients;

/// <summary>
/// Represents the REST resource for an OpenID Client's <b>effective</b> (merged) settings: the client's persisted
/// settings merged onto the tenant's effective settings through the settings merge pipeline (the same view the
/// authorization server resolves for the client at runtime), including settings that are not advertised in discovery.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientEffectiveSettingsResource : ISupportClientId, ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <inheritdoc cref="ISupportClientId.ClientId"/>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the client's effective settings as a flat JSON object of setting name to formatted value, using the same
    /// value representation as the discovery document.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
