// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Describes the root key of an owner without any key material: which key it is and the state of its last validation.
/// </summary>
/// <param name="Owner">The owner whose master keys the root key wraps.</param>
/// <param name="KeyWrapperName">The name of the key wrapper that wraps under the root key.</param>
/// <param name="KeyReference">The reference to the root key that the owner gave.</param>
/// <param name="State">The state of the root key: one of the values in <see cref="OwnerRootKeyStates"/>.</param>
/// <param name="CreatedWhen">The instant the key reference was set.</param>
/// <param name="StateChangedWhen">The instant the state last changed.</param>
/// <param name="ValidatedWhen">The instant of the last successful probe, or <see langword="null"/>.</param>
/// <param name="FailedWhen">The instant of the last failed probe or completion, or <see langword="null"/>.</param>
/// <param name="Failure">The description of the last failure, or <see langword="null"/>.</param>
// See ADR-1414.
[PublicAPI]
public sealed record OwnerRootKeyInfo(
    SecretOwner Owner,
    string KeyWrapperName,
    string KeyReference,
    string State,
    DateTimeOffset CreatedWhen,
    DateTimeOffset StateChangedWhen,
    DateTimeOffset? ValidatedWhen,
    DateTimeOffset? FailedWhen,
    string? Failure
)
{
    /// <summary>
    /// Gets the identity the server takes on to reach the root key, or <see langword="null"/> when the deployment's own
    /// identity reaches it.
    /// </summary>
    public string? AccessIdentity { get; init; }

    /// <summary>
    /// Describes a persisted owner root key.
    /// </summary>
    /// <param name="rootKey">The persisted owner root key.</param>
    /// <returns>The description, which carries none of the key's material.</returns>
    public static OwnerRootKeyInfo From(PersistedOwnerRootKey rootKey)
    {
        ArgumentNullException.ThrowIfNull(rootKey);

        return new OwnerRootKeyInfo(
            rootKey.Owner,
            rootKey.KeyWrapperName,
            rootKey.KeyReference,
            rootKey.State,
            rootKey.CreatedWhen,
            rootKey.StateChangedWhen,
            rootKey.ValidatedWhen,
            rootKey.FailedWhen,
            rootKey.Failure
        )
        {
            AccessIdentity = rootKey.AccessIdentity,
        };
    }
}
