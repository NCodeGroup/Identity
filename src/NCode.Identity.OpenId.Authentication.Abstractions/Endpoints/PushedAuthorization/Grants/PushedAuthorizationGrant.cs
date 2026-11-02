// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization.Grants;

/// <summary>
/// Represents the payload of a pushed authorization request (RFC 9126). The request parameters the client pushed to
/// the pushed authorization request endpoint are persisted keyed by the opaque reference embedded in the issued
/// <c>request_uri</c>, then consumed once at the authorization endpoint to replay the request.
/// </summary>
[PublicAPI]
public readonly record struct PushedAuthorizationGrant
{
    /// <summary>
    /// Gets the identifier of the client that pushed the authorization request.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the authorization request parameters the client pushed, keyed by parameter name, so the authorization
    /// endpoint can replay them.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Parameters { get; init; }
}
