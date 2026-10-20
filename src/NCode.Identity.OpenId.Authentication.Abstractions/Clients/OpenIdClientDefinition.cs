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
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Contains the resolved registration of a client that <see cref="IOpenIdClientFactory"/> builds an
/// <see cref="OpenIdClient"/> from.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class OpenIdClientDefinition
{
    /// <summary>
    /// Gets the client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the client's effective settings: the tenant's settings merged with the client's own.
    /// </summary>
    public required IReadOnlySettingCollection Settings { get; init; }

    /// <summary>
    /// Gets the secrets only known to the client.
    /// </summary>
    public required IReadOnlyCollection<SecretKey> Secrets { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client acts for, or <c>null</c> for an ordinary client.
    /// </summary>
    public string? ResourceServerId { get; init; }
}
