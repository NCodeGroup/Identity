// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.SharedState.Valkey.Options;

/// <summary>
/// Contains the options for the Valkey shared-state adapter, bound from the <see cref="DefaultSectionName"/>
/// configuration section. The server address and credentials are not options: they come from the
/// <see cref="ConnectionStringName"/> connection string, which accepts every StackExchange.Redis configuration
/// setting — for example <c>valkey.example:6380,ssl=true,user=identity,password=…</c> for TLS and an access-control
/// user — or from the <c>ValkeyConnection</c> parts.
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
    /// <see cref="DefaultKeyPrefix"/>. A prefix holds no braces: a clustered server takes the hash tag of a key from
    /// its first <c>{</c> and the next <c>}</c>, so braces in the prefix would put every key on one node.
    /// </summary>
    public string KeyPrefix { get; set; } = DefaultKeyPrefix;
}
