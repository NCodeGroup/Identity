// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;

/// <summary>
/// Represents the message for an <c>OAuth 2.0</c> token revocation request
/// (<see href="https://datatracker.ietf.org/doc/html/rfc7009">RFC 7009</see>).
/// </summary>
[PublicAPI]
public interface ITokenRevocationRequest : IOpenIdRequest
{
    /// <summary>
    /// Gets or sets the token that the client wants to revoke.
    /// </summary>
    string? Token { get; set; }

    /// <summary>
    /// Gets or sets the optional hint about the type of the token being revoked (for example, <c>access_token</c> or
    /// <c>refresh_token</c>).
    /// </summary>
    string? TokenTypeHint { get; set; }
}
