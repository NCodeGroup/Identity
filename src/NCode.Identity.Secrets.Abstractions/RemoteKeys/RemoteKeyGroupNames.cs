// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Contains the prefix of the group name of every key the server creates in a key management service; a provider lists
/// only the keys whose group carries it, so the sweep never sees a key the server did not create.
/// </summary>
// See ADR-1478.
[PublicAPI]
public static class RemoteKeyGroupNames
{
    /// <summary>
    /// Contains the prefix that begins every group name.
    /// </summary>
    public const string Prefix = "ncid-";
}
