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

namespace NCode.Identity.OpenId.Management.Contracts.Tenants;

/// <summary>
/// Represents the REST resource for a tenant's <b>effective</b> (merged) settings: the server baseline, the tenant's
/// persisted overrides, and descriptor defaults, resolved through the settings merge pipeline. This is the read-only
/// view the authorization server itself resolves at runtime, and — unlike the discovery document — it includes
/// settings that are not advertised (not discoverable). It is the gated replacement for the removed anonymous
/// discovery override.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantEffectiveSettingsResource : ISupportTenantId
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the tenant's effective settings as a flat JSON object of setting name to formatted value, using the same
    /// value representation as the discovery document.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
