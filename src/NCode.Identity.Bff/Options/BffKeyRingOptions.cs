// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures one key ring that the BFF reads from PEM files. Bound from the <c>Bff:KeyRings:{name}</c> configuration
/// section, where <c>{name}</c> is the key ring's name.
/// </summary>
[PublicAPI]
public sealed class BffKeyRingOptions
{
    /// <summary>
    /// Gets or sets the PEM files that hold the key ring's private keys. Each entry is the path of a PEM-encoded EC or
    /// RSA private key, relative to the content root or absolute. A key's id is its file name without the extension.
    /// The configuration section is <c>Bff:KeyRings:{name}:PemFiles</c>.
    /// </summary>
    public List<string> PemFiles { get; set; } = [];
}
