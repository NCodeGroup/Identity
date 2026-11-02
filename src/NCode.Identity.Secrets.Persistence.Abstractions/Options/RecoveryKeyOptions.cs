// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options for the offline recovery key, bound from the <c>Recovery</c> child of the
/// <see cref="SecretProtectionOptions.DefaultSectionName"/> configuration section. When a recovery public key is
/// configured, every master key is also wrapped under it, so the stored secrets can still be read after the root key
/// is lost.
/// </summary>
// See ADR-1235.
[PublicAPI]
public sealed class RecoveryKeyOptions
{
    /// <summary>
    /// Gets or sets the path of the file that holds the recovery RSA public key in PEM format, which must be at
    /// least 3072 bits. Every master key is also wrapped under this key; its private half is kept offline. Setting
    /// both this path and <see cref="Disabled"/> is an error.
    /// </summary>
    public string? PublicKeyPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether running without a recovery key is acknowledged. When <c>true</c> and no
    /// <see cref="PublicKeyPath"/> is set, losing the root key loses every stored secret. Setting both
    /// <see cref="PublicKeyPath"/> and this value is an error.
    /// </summary>
    public bool Disabled { get; set; }
}
