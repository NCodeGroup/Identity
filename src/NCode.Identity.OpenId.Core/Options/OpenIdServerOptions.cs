#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options used to configure the OpenID server.
/// </summary>
[PublicAPI]
public sealed class OpenIdServerOptions
{
    /// <summary>
    /// Contains the default value for the server's identifier.
    /// </summary>
    public const string DefaultServerId = "default";

    /// <summary>
    /// Gets or sets the identifier for the server.
    /// </summary>
    public string ServerId { get; set; } = DefaultServerId;

    /// <summary>
    /// Gets or sets the period of time after which the settings for the server are refreshed.
    /// The default value is 5 minutes.
    /// </summary>
    public TimeSpan SettingsPeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(5.0);

    /// <summary>
    /// Gets or sets the period of time after which the secrets for the server are refreshed.
    /// The default value is 15 minutes.
    /// </summary>
    public TimeSpan SecretsPeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(15.0);

    /// <summary>
    /// Gets or sets the configuration section that holds the server's settings, keyed by setting name (for example
    /// <c>grant_types_supported</c>). These are the root of the settings hierarchy that every tenant and client
    /// inherits from.
    /// </summary>
    /// <remarks>
    /// Settings are an open vocabulary, so the section is carried as-is rather than bound to a fixed shape: each value
    /// is typed by its registered setting descriptor when the settings are loaded, and the section is watched so a
    /// configuration reload refreshes them. When unset, the server has no configured settings beyond the defaults.
    /// </remarks>
    public IConfigurationSection? Settings { get; set; }
}
