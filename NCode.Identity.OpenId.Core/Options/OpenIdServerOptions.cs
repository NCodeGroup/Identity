﻿#region Copyright Preamble

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

namespace NCode.Identity.OpenId.Options;

/// <summary>
/// Contains the options used to configure the OpenID server.
/// </summary>
[PublicAPI]
public class OpenIdServerOptions
{
    /// <summary>
    /// Contains the default value for the server's identifier.
    /// </summary>
    public const string DefaultServerId = "default";

    /// <summary>
    /// Contains the configuration subsection name for the settings of the server.
    /// </summary>
    public const string SettingsSubsection = "Server:Settings";

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
}
