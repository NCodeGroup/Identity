#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets;

/// <summary>
/// Contains constants for the possible ways a secret's stored key material is encoded — an axis orthogonal to the
/// crypto family (<see cref="SecretKeyTypes"/>). A secret-key-type descriptor produces one of these when generating
/// or importing material and consumes it when deserializing, so public (SPKI) and private (PKCS#8) asymmetric
/// material are distinguished unambiguously.
/// </summary>
// See ADR-0104.
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
    /// Determines whether a secret encoding holds only public key material, which is stored without encryption.
    /// Every other encoding holds private or symmetric material and is always stored encrypted.
    /// </summary>
    /// <param name="secretEncoding">The secret encoding to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="secretEncoding"/> is <see cref="Spki"/> or <see cref="X509"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    // See ADR-0228.
    public static bool IsPublic(string secretEncoding)
    {
        ArgumentNullException.ThrowIfNull(secretEncoding);

        return string.Equals(secretEncoding, Spki, StringComparison.Ordinal)
            || string.Equals(secretEncoding, X509, StringComparison.Ordinal);
    }
}
