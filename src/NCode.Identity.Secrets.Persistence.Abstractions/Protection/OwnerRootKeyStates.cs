// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains the known states of an owner root key. The set is open, and a state that the code does not know is treated
/// as <see cref="Unavailable"/>, so that the owner fails closed.
/// </summary>
// See ADR-1414.
[PublicAPI]
public static class OwnerRootKeyStates
{
    /// <summary>
    /// The key wraps every master key of the owner and the next ones.
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// The last probe of the key failed, so the owner fails closed until a probe succeeds.
    /// </summary>
    public const string Unavailable = "unavailable";

    /// <summary>
    /// The key is being removed: new master keys are wrapped under the deployment's root key while the existing ones
    /// move back under it.
    /// </summary>
    public const string Removing = "removing";
}
