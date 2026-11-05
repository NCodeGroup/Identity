// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Describes a master key without any of its material: its identity, its owner and version, its lifecycle and where it
/// is wrapped. No wrapped key and no key check is part of it.
/// </summary>
/// <param name="KeyId">The identifier of the master key.</param>
/// <param name="Owner">The owner whose stored secrets the master key protects.</param>
/// <param name="Version">The version of the master key within its owner.</param>
/// <param name="CreatedWhen">The instant the master key was created.</param>
/// <param name="RetiredWhen">The instant the master key was retired, or <see langword="null"/> while it is active.</param>
/// <param name="RootKeyProvider">The name of the root key provider that wraps the master key.</param>
/// <param name="RootKeyId">The identifier of the root key that wraps the master key.</param>
/// <param name="RecoveryKeyId">The identifier of the recovery key that also wraps the master key, or
/// <see langword="null"/> when no recovery copy exists.</param>
// See ADR-1407 and ADR-1411.
[PublicAPI]
public sealed record MasterKeyInfo(
    Guid KeyId,
    SecretOwner Owner,
    int Version,
    DateTimeOffset CreatedWhen,
    DateTimeOffset? RetiredWhen,
    string RootKeyProvider,
    string RootKeyId,
    string? RecoveryKeyId
)
{
    /// <summary>
    /// Gets the reference of the owner root key that wraps the master key, or <see langword="null"/> when the
    /// deployment's root key wraps it.
    /// </summary>
    public string? RootKeyReference { get; init; }

    /// <summary>
    /// Gets a value indicating whether the master key is active, which is the one that protects new values.
    /// </summary>
    public bool IsActive => RetiredWhen is null;

    /// <summary>
    /// Describes a persisted master key.
    /// </summary>
    /// <param name="masterKey">The persisted master key.</param>
    /// <returns>The description, which carries none of the key's material.</returns>
    public static MasterKeyInfo From(PersistedMasterKey masterKey)
    {
        ArgumentNullException.ThrowIfNull(masterKey);

        return new MasterKeyInfo(
            masterKey.KeyId,
            masterKey.Owner,
            masterKey.Version,
            masterKey.CreatedWhen,
            masterKey.RetiredWhen,
            masterKey.RootKeyProvider,
            masterKey.RootKeyId,
            masterKey.RecoveryKeyId
        )
        {
            RootKeyReference = masterKey.RootKeyReference,
        };
    }
}
