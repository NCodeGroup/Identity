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
/// Represents the payload of a persisted registration access token (RFC 7592). The token is persisted keyed by its
/// value and binds the bearer to the single client it is permitted to read, update, or delete.
/// </summary>
[PublicAPI]
public readonly record struct RegistrationAccessTokenGrant
{
    /// <summary>
    /// Gets the identifier of the client that the registration access token manages.
    /// </summary>
    public required string ClientId { get; init; }
}
