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
