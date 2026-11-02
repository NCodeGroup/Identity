// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Introspection.Messages;

/// <summary>
/// Represents the message for an <c>OAuth 2.0</c> token introspection request
/// (<see href="https://datatracker.ietf.org/doc/html/rfc7662">RFC 7662</see>).
/// </summary>
[PublicAPI]
public interface ITokenIntrospectionRequest : IOpenIdRequest
{
    /// <summary>
    /// Gets or sets the token for which introspection metadata is requested.
    /// </summary>
    string? Token { get; set; }

    /// <summary>
    /// Gets or sets the optional hint about the type of the token being introspected (for example, <c>access_token</c>
    /// or <c>refresh_token</c>).
    /// </summary>
    string? TokenTypeHint { get; set; }
}
