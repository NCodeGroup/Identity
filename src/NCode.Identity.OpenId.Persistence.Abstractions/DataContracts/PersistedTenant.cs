#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> Tenant instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedTenant : BasePersistedTenantResource
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType => ResourceTypePrefix;

    /// <summary>
    /// Gets or sets the issuer identifier for this tenant — its request-independent identity, and therefore also the
    /// origin of every absolute URL the server publishes for it (the discovery endpoint URLs, the RFC 7592
    /// <c>registration_client_uri</c>, the device <c>verification_uri</c>, the <c>check_session_iframe</c>). It is
    /// also what the host-based tenant strategy routes on, via the host component.
    /// </summary>
    /// <remarks>
    /// Optional. When <c>null</c>, the origin is derived from the incoming request, which is a development-only
    /// convenience: it lets a caller's <c>Host</c> header choose where the server tells clients to send their
    /// credentials. Any deployment reachable by someone other than its developer must set this.
    /// </remarks>
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public required string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the RFC 8705 §5 mutual-TLS endpoint-alias authority for this tenant — an <c>https</c> origin (for
    /// example <c>https://acme.mtls.example</c>) the discovery document mirrors the tenant's endpoints under in
    /// <c>mtls_endpoint_aliases</c>, so mutual-TLS clients reach them on a host that requests a client certificate. The
    /// host-based tenant strategy routes a request to this host to the tenant, exactly as it routes the issuer's host.
    /// </summary>
    /// <remarks>
    /// Optional. When <c>null</c>, the tenant publishes no aliases and mutual-TLS clients call the issuer's endpoints.
    /// </remarks>
    [MaxLength(OpenIdMaxLengths.Issuer)]
    public string? MtlsEndpointAliasAuthority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tenant is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the display name for the tenant.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the JSON settings for an OpenID Tenant instance.
    /// </summary>
    public required PersistedTenantSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the collection of secrets only known to an OpenID Tenant instance.
    /// </summary>
    public required PersistedTenantSecrets Secrets { get; set; }
}
