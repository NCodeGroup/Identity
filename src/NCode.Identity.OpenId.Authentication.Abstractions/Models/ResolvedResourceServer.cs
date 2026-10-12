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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Models;

/// <summary>
/// Represents a resource server resolved at access-token issuance as one of the audiences a token targets, carrying the
/// effective settings used to govern per-audience encryption of the token when it targets this resource server alone
/// (ADR-0075).
/// </summary>
[PublicAPI]
public sealed class ResolvedResourceServer
{
    /// <summary>
    /// Gets the opaque identifier of the resource server.
    /// </summary>
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets the audience identifier of the resource server (its <c>aud</c> value).
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the resource server's effective settings — the server baseline merged with the tenant's and then the
    /// resource server's persisted overrides — used to select the access-token encryption algorithm (and whether
    /// encryption is required) when the token targets this resource server alone.
    /// </summary>
    public required IReadOnlySettingCollection EffectiveSettings { get; init; }
}
