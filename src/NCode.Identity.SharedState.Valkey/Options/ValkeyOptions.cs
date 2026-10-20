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

namespace NCode.Identity.SharedState.Valkey.Options;

/// <summary>
/// Contains the options for the Valkey shared-state adapter, bound from the <see cref="DefaultSectionName"/>
/// configuration section. The server address and credentials are not options: they come from the
/// <see cref="ConnectionStringName"/> connection string, which accepts every StackExchange.Redis configuration
/// setting — for example <c>valkey.example:6380,ssl=true,user=identity,password=…</c> for TLS and an access-control
/// user.
/// </summary>
[PublicAPI]
public sealed class ValkeyOptions
{
    /// <summary>
    /// Gets the name of the configuration section the options are bound from.
    /// </summary>
    public const string DefaultSectionName = "Valkey";

    /// <summary>
    /// Gets the name of the connection string, under <c>ConnectionStrings</c>, that addresses the Valkey server.
    /// </summary>
    public const string ConnectionStringName = "valkey";

    /// <summary>
    /// Gets the default value of <see cref="KeyPrefix"/>.
    /// </summary>
    public const string DefaultKeyPrefix = "ncode-identity:";

    /// <summary>
    /// Gets or sets the prefix prepended to every key this adapter writes, so that several deployments, or unrelated
    /// applications, can share one Valkey server without their keys colliding. Defaults to
    /// <see cref="DefaultKeyPrefix"/>.
    /// </summary>
    public string KeyPrefix { get; set; } = DefaultKeyPrefix;
}
