// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Contains the client metadata values resolved from a dynamic client registration request after applying the
/// protocol defaults.
/// </summary>
internal readonly record struct ResolvedMetadata
{
    /// <summary>
    /// Gets the resolved client authentication method for the token endpoint.
    /// </summary>
    public required string TokenEndpointAuthMethod { get; init; }

    /// <summary>
    /// Gets the resolved OAuth 2.0 grant types.
    /// </summary>
    public required IReadOnlyList<string> GrantTypes { get; init; }

    /// <summary>
    /// Gets the resolved OAuth 2.0 response types, each a whole <c>response_type</c> value in canonical form; empty for
    /// a client that does not use the authorization endpoint.
    /// </summary>
    public required IReadOnlyList<string> ResponseTypes { get; init; }

    /// <summary>
    /// Gets the resolved redirection URIs.
    /// </summary>
    public required IReadOnlyList<string> RedirectUris { get; init; }

    /// <summary>
    /// Gets the resolved subject identifier type (<c>public</c> or <c>pairwise</c>), or <see langword="null"/> when the
    /// client did not specify one and therefore inherits the resolved default.
    /// </summary>
    public string? SubjectType { get; init; }

    /// <summary>
    /// Gets the resolved sector identifier URI, or <see langword="null"/> when the client did not specify one.
    /// </summary>
    public string? SectorIdentifierUri { get; init; }
}
