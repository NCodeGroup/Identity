// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains the wraps of a master key under the root of its owner.
/// </summary>
/// <param name="Root">The master key wrapped by the root key provider of its owner.</param>
/// <param name="RootKeyReference">The reference of the owner root key that produced <paramref name="Root"/>, or
/// <see langword="null"/> when the deployment's root key produced it.</param>
/// <param name="Recovery">The master key wrapped under the recovery public key, or <see langword="null"/> when none
/// applies.</param>
/// <param name="ClearsRecovery"><see langword="true"/> when the master key has no recovery copy, because an owner root
/// key wraps it, so any existing recovery copy is removed.</param>
// See ADR-1411.
internal readonly record struct MasterKeyWraps(
    WrappedKey Root,
    string? RootKeyReference,
    WrappedKey? Recovery,
    bool ClearsRecovery
);
