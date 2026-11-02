// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets;

/// <summary>
/// Contains constants for the <see cref="KeyMetadata.Use"/> property.
/// Its values are the JSON Web Key <c>use</c> values of <see cref="JsonWebKeyUses"/>.
/// https://datatracker.ietf.org/doc/html/rfc7517#section-4.2
/// </summary>
[PublicAPI]
public static class SecretKeyUses
{
    /// <summary>
    /// Contains the constant value for the <see cref="KeyMetadata.Use"/> property when the key is intended for digital signatures.
    /// </summary>
    public const string Signature = JsonWebKeyUses.Signature;

    /// <summary>
    /// Contains the constant value for the <see cref="KeyMetadata.Use"/> property when the key is intended for encryption.
    /// </summary>
    public const string Encryption = JsonWebKeyUses.Encryption;
}
