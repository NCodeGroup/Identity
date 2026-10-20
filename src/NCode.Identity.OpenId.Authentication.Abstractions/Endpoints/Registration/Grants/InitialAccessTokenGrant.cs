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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;

/// <summary>
/// Represents the payload of a persisted initial access token (RFC 7591). The token is persisted keyed by its value
/// and, while active, authorizes its bearer to register a new client at the dynamic client registration endpoint.
/// </summary>
[PublicAPI]
public readonly record struct InitialAccessTokenGrant
{
    /// <summary>
    /// Gets the optional human-readable description of the initial access token, for administrative purposes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the token is scoped to, or <c>null</c> when it registers ordinary
    /// clients. A client registered with a scoped token is bound to that resource server and introspects for it.
    /// </summary>
    public string? ResourceServerId { get; init; }
}
