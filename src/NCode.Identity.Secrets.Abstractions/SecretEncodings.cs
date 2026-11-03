// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets;

/// <summary>
/// Contains constants for the possible ways a secret's stored key material is encoded — an axis orthogonal to the
/// crypto family (<see cref="SecretKeyTypes"/>). A secret-key-type descriptor produces one of these when generating
/// or importing material and consumes it when deserializing, so public (SPKI) and private (PKCS#8) asymmetric
/// material are distinguished unambiguously.
/// </summary>
// See ADR-1104.
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretEncodings
{
    /// <summary>
    /// Indicates that the stored material is the raw bytes of a <c>symmetric</c> key.
    /// </summary>
    public const string SymmetricRaw = "symmetric_raw";

    /// <summary>
    /// Indicates that the stored material is an asymmetric <b>private</b> key encoded as <c>PKCS#8</c> DER.
    /// </summary>
    public const string Pkcs8 = "pkcs8";

    /// <summary>
    /// Indicates that the stored material is an asymmetric <b>public</b> key encoded as
    /// <c>SubjectPublicKeyInfo</c> (SPKI) DER.
    /// </summary>
    public const string Spki = "spki";

    /// <summary>
    /// Indicates that the stored material is an <c>X.509</c> certificate encoded as DER. The certificate carries a
    /// public key only (a relying party's registered certificate).
    /// </summary>
    public const string X509 = "x509";

    /// <summary>
    /// Indicates that the stored material is a self-describing, one-way <b>verifier</b> of a shared secret, such as a
    /// <c>client_secret</c> that is only ever compared: it confirms a presented secret and the secret cannot be read
    /// back from it. A verifier is not key material; it is still stored encrypted, like every encoding that is not
    /// public.
    /// </summary>
    // See ADR-1391.
    public const string Verifier = "verifier";

    /// <summary>
    /// Determines whether a secret encoding holds only public key material, which is stored without encryption.
    /// Every other encoding holds private or symmetric material and is always stored encrypted.
    /// </summary>
    /// <param name="secretEncoding">The secret encoding to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="secretEncoding"/> is <see cref="Spki"/> or <see cref="X509"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    // See ADR-1235.
    public static bool IsPublic(string secretEncoding)
    {
        ArgumentNullException.ThrowIfNull(secretEncoding);

        return string.Equals(secretEncoding, Spki, StringComparison.Ordinal)
            || string.Equals(secretEncoding, X509, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether a secret encoding holds a one-way verifier of a shared secret rather than key material.
    /// </summary>
    /// <param name="secretEncoding">The secret encoding to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="secretEncoding"/> is <see cref="Verifier"/>; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    // See ADR-1391.
    public static bool IsVerifier(string secretEncoding)
    {
        ArgumentNullException.ThrowIfNull(secretEncoding);

        return string.Equals(secretEncoding, Verifier, StringComparison.Ordinal);
    }
}
