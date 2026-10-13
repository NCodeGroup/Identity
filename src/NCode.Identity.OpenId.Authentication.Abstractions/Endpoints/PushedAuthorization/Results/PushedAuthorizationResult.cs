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
    [JsonPropertyName(OpenIdConstants.Parameters.RequestUri)]
    public required string RequestUri { get; init; }

    /// <summary>
    /// Gets the lifetime in seconds of the pushed request, after which its <see cref="RequestUri"/> can no longer be
    /// used.
    /// </summary>
    [JsonPropertyName(OpenIdConstants.Parameters.ExpiresIn)]
    public required int ExpiresIn { get; init; }
}
