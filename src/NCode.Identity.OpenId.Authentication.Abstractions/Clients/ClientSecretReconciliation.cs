// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Contains the result of <see cref="IClientSecretIssuer.ReconcileClientSecrets"/>: a client's secrets in the form its
/// registered method needs, and the changes to store.
/// </summary>
// See ADR-1391.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientSecretReconciliation
{
    /// <summary>
    /// Gets the client's secrets afterward.
    /// </summary>
    public required IReadOnlyList<PersistedSecret> Secrets { get; init; }

    /// <summary>
    /// Gets the secrets to remove from the store.
    /// </summary>
    public required IReadOnlyList<PersistedSecret> RemovedSecrets { get; init; }

    /// <summary>
    /// Gets the secrets to add to the store.
    /// </summary>
    public required IReadOnlyList<PersistedSecret> AddedSecrets { get; init; }

    /// <summary>
    /// Gets the secret issued because none of the client's shared secrets could serve its method, whose
    /// <see cref="GeneratedSecret.SecretMaterial"/> is revealed once, in the response to the change; or
    /// <see langword="null"/> when the client keeps a secret it holds.
    /// </summary>
    public GeneratedSecret? IssuedSecret { get; init; }

    /// <summary>
    /// Gets a value indicating whether the client's secrets change.
    /// </summary>
    public bool HasChanges => RemovedSecrets.Count > 0 || AddedSecrets.Count > 0;
}
