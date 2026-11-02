// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides an <see cref="AkpSecretKey"/> that holds only the <b>public</b> half of an ML-DSA key. It can verify
/// signatures but not produce them; it is used for keys a relying party registers for verification (for example a
/// client's <c>private_key_jwt</c> / JAR key resolved from its JWKS). The public key is not secret, so it is stored
/// unprotected.
/// </summary>
internal sealed class PublicAkpSecretKey(
    KeyMetadata metadata,
    MLDsaAlgorithm algorithm,
    ReadOnlyMemory<byte> publicKey,
    ReadOnlyMemory<byte>? certificateRawData
) : AkpSecretKey(metadata, algorithm, publicKey, certificateRawData)
{
    /// <inheritdoc />
    public override bool HasPrivateKey => false;

    /// <inheritdoc />
    public override MLDsa ExportMLDsa() => MLDsa.ImportMLDsaPublicKey(Algorithm, RawPublicKey.Span);
}
