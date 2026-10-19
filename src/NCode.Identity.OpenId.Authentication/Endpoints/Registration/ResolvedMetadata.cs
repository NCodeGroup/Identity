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

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Contains the client metadata values resolved from a dynamic client registration request after applying the
/// protocol defaults.
/// </summary>
internal readonly record struct ResolvedMetadata
{
    /// <summary>
    /// Gets the resolved client authentication method for the token endpoint.
    /// </summary>
    public required string TokenEndpointAuthMethod { get; init; }

    /// <summary>
    /// Gets the resolved OAuth 2.0 grant types.
    /// </summary>
    public required IReadOnlyList<string> GrantTypes { get; init; }

    /// <summary>
    /// Gets the resolved OAuth 2.0 response types, each a whole <c>response_type</c> value in canonical form; empty for
    /// a client that does not use the authorization endpoint.
    /// </summary>
    public required IReadOnlyList<string> ResponseTypes { get; init; }

    /// <summary>
    /// Gets the resolved redirection URIs.
    /// </summary>
    public required IReadOnlyList<string> RedirectUris { get; init; }

    /// <summary>
    /// Gets the resolved subject identifier type (<c>public</c> or <c>pairwise</c>), or <see langword="null"/> when the
    /// client did not specify one and therefore inherits the resolved default.
    /// </summary>
    public string? SubjectType { get; init; }

    /// <summary>
    /// Gets the resolved sector identifier URI, or <see langword="null"/> when the client did not specify one.
    /// </summary>
    public string? SectorIdentifierUri { get; init; }
}
