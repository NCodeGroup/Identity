// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides the ability to import a caller-supplied <b>public</b> asymmetric key (JWK, PEM, or X.509 certificate) as
/// a new <see cref="PersistedSecret"/>. Only public material is accepted — a private or symmetric key is rejected —
/// so no secret material is ever supplied to, or revealed by, this path.
/// </summary>
// See ADR-1104.
[PublicAPI]
public interface ISecretImporter
{
    /// <summary>
    /// Parses and validates the caller-supplied public key material described by <paramref name="request"/>, and
    /// returns a <see cref="PersistedSecret"/> ready to be persisted. The returned secret's
    /// <see cref="PersistedSecret.ConcurrencyToken"/> is left unset for the store to assign.
    /// </summary>
    /// <param name="request">The <see cref="SecretKeyImportRequest"/> describing the secret to import.</param>
    /// <returns>The imported <see cref="PersistedSecret"/> with data-protected public key material.</returns>
    /// <exception cref="System.ArgumentException">The supplied material is malformed, is not public-only, or uses an
    /// unsupported key type.</exception>
    PersistedSecret ImportSecret(SecretKeyImportRequest request);
}
