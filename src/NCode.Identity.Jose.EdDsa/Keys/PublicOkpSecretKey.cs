// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.EdDsa.Keys;

/// <summary>
/// An <see cref="OkpSecretKey"/> that holds only the public half. It can verify <c>EdDSA</c> signatures but not
/// produce them; it is used for keys a relying party registers for verification (for example a client's JWKS key).
/// </summary>
internal sealed class PublicOkpSecretKey(
    KeyMetadata metadata,
    OkpCurveInfo curve,
    ReadOnlyMemory<byte> publicKey
) : OkpSecretKey(metadata, curve.Name, curve.KeySizeBits, publicKey, certificateRawData: null)
{
    /// <inheritdoc />
    public override bool HasPrivateKey => false;

    /// <inheritdoc />
    public override void ExportPrivateKey<TWriter>(ref TWriter destination) =>
        throw new InvalidOperationException(
            "This OKP secret key holds only the public half and cannot export a private seed."
        );
}
