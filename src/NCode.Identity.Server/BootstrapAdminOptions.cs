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

namespace NCode.Identity.Server;

/// <summary>
/// Contains the options for the bootstrap administrator credential: a cold-start client, supplied out of band through
/// configuration, whose management-audience tokens are stamped with the <c>GlobalAdmin</c> role so an operator can make
/// the first authorized management call and provision durable administrators. The feature is opt-in: when
/// <see cref="ClientId"/> and <see cref="ClientSecret"/> are absent, nothing is seeded and no role is conferred.
/// </summary>
[PublicAPI]
public class BootstrapAdminOptions
{
    /// <summary>
    /// Gets the default configuration section name that holds the bootstrap administrator options.
    /// </summary>
    public const string DefaultSectionName = "BootstrapAdmin";

    /// <summary>
    /// Gets or sets the client identifier of the bootstrap administrator client.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret of the bootstrap administrator client. It is protected before it is persisted and
    /// is never logged.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets the identifier of the tenant the bootstrap administrator client is seeded into. When absent, the configured
    /// root (control-plane) tenant is used.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets a value indicating whether the bootstrap administrator is configured (both a client id and secret are
    /// present), which both enables seeding and confers the <c>GlobalAdmin</c> role on the client's tokens.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
}
