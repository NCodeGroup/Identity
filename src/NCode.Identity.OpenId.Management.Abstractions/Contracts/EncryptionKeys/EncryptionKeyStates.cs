// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Contains the states that an encryption key reports. The set is open: a client treats a state it does not know as
/// retired.
/// </summary>
// See ADR-1407.
[PublicAPI]
public static class EncryptionKeyStates
{
    /// <summary>
    /// The key protects new values.
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// The key was replaced by a newer version and only protects values that have not been rewrapped yet.
    /// </summary>
    public const string Retired = "retired";
}
