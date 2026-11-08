// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// The outcome of binding a key that a key management service holds: either the <c>remote</c> secret that references
/// the key, or the error that describes why the key is refused.
/// </summary>
// See ADR-1476.
internal sealed class RemoteSecretImportResult
{
    private RemoteSecretImportResult(PersistedSecret? secret, ManagementError? error)
    {
        Secret = secret;
        Error = error;
    }

    /// <summary>
    /// Creates the outcome of a key that is bound.
    /// </summary>
    /// <param name="secret">The <c>remote</c> secret that references the key.</param>
    /// <returns>The <see cref="RemoteSecretImportResult"/>.</returns>
    public static RemoteSecretImportResult Bound(PersistedSecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        return new RemoteSecretImportResult(secret, error: null);
    }

    /// <summary>
    /// Creates the outcome of a key that is refused.
    /// </summary>
    /// <param name="error">The error that describes why the key is refused.</param>
    /// <returns>The <see cref="RemoteSecretImportResult"/>.</returns>
    public static RemoteSecretImportResult Refused(ManagementError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new RemoteSecretImportResult(secret: null, error);
    }

    /// <summary>
    /// Gets a value indicating whether the key is bound.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Secret))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsBound => Error is null;

    /// <summary>
    /// Gets the <c>remote</c> secret that references the key, or <c>null</c> when the key is refused.
    /// </summary>
    public PersistedSecret? Secret { get; }

    /// <summary>
    /// Gets the error that describes why the key is refused, or <c>null</c> when the key is bound.
    /// </summary>
    public ManagementError? Error { get; }
}
