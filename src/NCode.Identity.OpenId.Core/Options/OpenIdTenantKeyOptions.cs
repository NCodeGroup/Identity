// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options for one key every tenant is provisioned with. The key's type and intended use follow from its
/// algorithm: a signature algorithm yields a signing key, a key-management algorithm an encryption key.
/// </summary>
[PublicAPI]
public sealed class OpenIdTenantKeyOptions
{
    /// <summary>
    /// Gets or sets the code of the JSON Web Algorithm the key is generated for and bound to (for example
    /// <c>ES256</c>). It must name a registered signature or key-management algorithm whose key type is asymmetric.
    /// </summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size, in bits, of the key to generate, or <c>null</c> to generate the smallest size the
    /// algorithm accepts. The size must be one the algorithm accepts.
    /// </summary>
    public int? KeySizeBits { get; set; }
}
