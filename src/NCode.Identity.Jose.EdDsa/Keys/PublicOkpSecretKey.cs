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
