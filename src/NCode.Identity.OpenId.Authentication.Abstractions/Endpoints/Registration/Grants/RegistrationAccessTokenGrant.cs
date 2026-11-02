// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;

/// <summary>
/// Represents the payload of a persisted registration access token (RFC 7592). The token is persisted keyed by its
/// value and binds the bearer to the single client it is permitted to read, update, or delete.
/// </summary>
[PublicAPI]
public readonly record struct RegistrationAccessTokenGrant
{
    /// <summary>
    /// Gets the identifier of the client that the registration access token manages.
    /// </summary>
    public required string ClientId { get; init; }
}
