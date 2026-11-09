// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using NCode.Identity.OpenId;

namespace NCode.Identity.Server.OpenApi;

/// <summary>
/// The OpenAPI tags of the management APIs: every endpoint tag except the OpenID protocol tag.
/// </summary>
internal static class ManagementOpenApiTags
{
    /// <summary>
    /// The names of the management tags.
    /// </summary>
    public static FrozenSet<string> Names { get; } =
        new[]
        {
            OpenIdConstants.EndpointTags.Clients,
            OpenIdConstants.EndpointTags.Grants,
            OpenIdConstants.EndpointTags.AuditRecords,
            OpenIdConstants.EndpointTags.Sessions,
            OpenIdConstants.EndpointTags.ClientGrants,
            OpenIdConstants.EndpointTags.ResourceServers,
            OpenIdConstants.EndpointTags.Platform,
            OpenIdConstants.EndpointTags.Tenants,
            OpenIdConstants.EndpointTags.Teams,
            OpenIdConstants.EndpointTags.LocalAccounts,
            OpenIdConstants.EndpointTags.Principals,
            OpenIdConstants.EndpointTags.Capabilities,
            OpenIdConstants.EndpointTags.Caller,
        }.ToFrozenSet(StringComparer.Ordinal);
}
