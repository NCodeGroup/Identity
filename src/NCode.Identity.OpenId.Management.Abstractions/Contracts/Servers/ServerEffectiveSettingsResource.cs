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
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.Servers;

/// <summary>
/// Represents the REST resource for an OpenID Server's <b>effective</b> (resolved) settings: the server's own settings
/// with descriptor defaults applied. The server is the root of the settings merge hierarchy, so this is the baseline
/// that tenants (and in turn clients) inherit from, including settings that are not advertised in discovery.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ServerEffectiveSettingsResource : ISupportServerId
{
    /// <inheritdoc cref="ISupportServerId.ServerId"/>
    public required string ServerId { get; init; }

    /// <summary>
    /// Gets the server's effective settings as a flat JSON object of setting name to formatted value, using the same
    /// value representation as the discovery document.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
