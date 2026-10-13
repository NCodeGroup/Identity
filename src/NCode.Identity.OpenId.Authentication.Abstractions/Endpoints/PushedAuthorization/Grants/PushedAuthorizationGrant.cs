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
