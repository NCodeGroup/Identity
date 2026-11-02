// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains constants for the possible values of the <see cref="PersistedSecret.SecretType"/> property — the
/// crypto family, shared with the runtime key-type taxonomy (<see cref="SecretKeyTypes"/>). Whether the stored
/// material is a certificate, a bare public key, or a private key is an orthogonal axis
/// (<see cref="SecretEncodings"/>, ADR-1104), not a distinct secret type.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretTypes
{
    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents a <c>symmetric</c> secret key.
    /// </summary>
    public const string Symmetric = SecretKeyTypes.Symmetric;

    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents an <c>RSA</c> secret key.
    /// </summary>
    public const string Rsa = SecretKeyTypes.Rsa;

    /// <summary>
    /// Indicates that a <see cref="PersistedSecret"/> represents an <c>Elliptic-Curve</c> secret key.
    /// </summary>
    public const string Ecc = SecretKeyTypes.Ecc;
}
