// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// The status code and the parsed JSON body (when the response is JSON) or the compact JWT (when the response is a JWT)
/// of a provider response.
/// </summary>
internal readonly record struct ProviderHttpResponse(int StatusCode, JsonElement? Json)
{
    /// <summary>
    /// The <c>DPoP-Nonce</c> response header, if any (RFC 9449 §8).
    /// </summary>
    public string? DPoPNonce { get; init; }

    /// <summary>
    /// The media type of the response body, if any, such as <c>application/json</c> or <c>application/jwt</c>.
    /// </summary>
    public string? MediaType { get; init; }

    /// <summary>
    /// The <c>WWW-Authenticate</c> response header, if any, with which a protected resource such as the UserInfo
    /// endpoint reports an error (RFC 6750 §3, RFC 9449 §7.1).
    /// </summary>
    public string? WwwAuthenticate { get; init; }

    /// <summary>
    /// The compact JWT of an <c>application/jwt</c> body, such as a signed UserInfo response, if any.
    /// </summary>
    public string? Jwt { get; init; }
}
