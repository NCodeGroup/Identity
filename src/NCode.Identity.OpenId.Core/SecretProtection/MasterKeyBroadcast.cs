// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Names the replica broadcast that announces a change to the master keys of an owner. The message is the owner in the
/// form that <c>SecretOwner.ToString()</c> writes and <c>SecretOwner.TryParse</c> reads; it carries no key material.
/// </summary>
// See ADR-1407.
[PublicAPI]
public static class MasterKeyBroadcast
{
    /// <summary>
    /// The channel on which a replica announces that the master keys of an owner changed, so that every replica reads
    /// them again.
    /// </summary>
    public const string ChannelName = "master-key-changed";
}
