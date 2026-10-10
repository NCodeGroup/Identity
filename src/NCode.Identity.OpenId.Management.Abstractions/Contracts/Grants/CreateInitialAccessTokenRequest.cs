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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Grants;

/// <summary>
/// Represents the request to mint an initial access token (RFC 7591) that authorizes its bearer to register a new
/// client at the protected dynamic client registration endpoint.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateInitialAccessTokenRequest
{
    /// <summary>
    /// Gets the optional human-readable description of the initial access token, recorded for administrative purposes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional lifetime of the token, in seconds. When <c>null</c>, the token does not expire. When
    /// specified, it must be greater than zero.
    /// </summary>
    public long? LifetimeSeconds { get; init; }
}
