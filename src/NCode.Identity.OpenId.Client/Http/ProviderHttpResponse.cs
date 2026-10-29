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

using System.Text.Json;

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// The status code and the parsed JSON body (when the response is JSON) of a provider response.
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
}
