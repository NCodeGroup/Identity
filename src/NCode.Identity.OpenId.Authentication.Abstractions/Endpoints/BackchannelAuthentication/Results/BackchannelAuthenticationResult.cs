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

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Results;

/// <summary>
/// Contains the parameters for a CIBA backchannel authentication response (OpenID Connect CIBA §7.3).
/// </summary>
[PublicAPI]
public sealed class BackchannelAuthenticationResult
{
    /// <summary>
    /// Gets the unique identifier the client polls the token endpoint with.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.AuthReqId)]
    public required string AuthReqId { get; init; }

    /// <summary>
    /// Gets the lifetime in seconds of the <c>auth_req_id</c>.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.ExpiresIn)]
    public required int ExpiresIn { get; init; }

    /// <summary>
    /// Gets the minimum amount of time in seconds the client should wait between polling requests to the token
    /// endpoint.
    /// </summary>
    [JsonPropertyName(OpenIdProtocol.Parameters.Interval)]
    public required int Interval { get; init; }
}
