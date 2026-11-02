// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization.Results;

/// <summary>
/// Contains the parameters for a pushed authorization response (RFC 9126 §2.2).
/// </summary>
[PublicAPI]
public sealed class PushedAuthorizationResult
{
    /// <summary>
    /// Gets the request URI the client supplies to the authorization endpoint to invoke the pushed request. Its value
    /// is the <c>urn:ietf:params:oauth:request_uri:</c> URN followed by an opaque, single-use reference.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.RequestUri)]
    public required string RequestUri { get; init; }

    /// <summary>
    /// Gets the lifetime in seconds of the pushed request, after which its <see cref="RequestUri"/> can no longer be
    /// used.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.ExpiresIn)]
    public required int ExpiresIn { get; init; }
}
