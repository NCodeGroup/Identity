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

using System.Buffers;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Keys;

// ML-DSA PKCS#8 / SubjectPublicKeyInfo DER container formats are marked [Experimental] (SYSLIB5006) in .NET 10, but
// their encoding is fixed by the published RFC 9881 (ML-DSA X.509 / PKCS#8 algorithm identifiers) and consumed by
// RFC 9964 (ML-DSA for JOSE). The suppression is the deliberate, documented decision in ADR-0118.
#pragma warning disable SYSLIB5006

/// <summary>
/// Represents an <c>AKP</c> (Algorithm Key Pair) secret key backing a post-quantum <c>ML-DSA</c> (FIPS 204 /
/// RFC 9964) signature. The key material is the raw public key (always present) and, for a private key, the
/// <see cref="System.Security.Cryptography.MLDsa"/> private key reached through <see cref="ExportMLDsa"/>. AKP keys
/// have no <see cref="AsymmetricAlgorithm"/> representation; their public material is produced as
/// <c>SubjectPublicKeyInfo</c> DER (RFC 9881).
/// </summary>
/// <param name="metadata">The <see cref="KeyMetadata"/> for the key.</param>
/// <param name="algorithm">The ML-DSA parameter set (<c>ML-DSA-44</c>, <c>ML-DSA-65</c>, or <c>ML-DSA-87</c>).</param>
/// <param name="publicKey">The raw ML-DSA public key bytes.</param>
/// <param name="certificateRawData">The optional DER-encoded certificate bytes, or <see langword="null"/>.</param>
[PublicAPI]
public abstract class AkpSecretKey(
    KeyMetadata metadata,
    MLDsaAlgorithm algorithm,
    ReadOnlyMemory<byte> publicKey,
    ReadOnlyMemory<byte>? certificateRawData
) : AsymmetricSecretKey(metadata, algorithm.PublicKeySizeInBytes * 8, certificateRawData)
{
    /// <summary>The <see cref="SecretKey.KeyType"/> value for an AKP key.</summary>
    public const string TypeName = SecretKeyTypes.Akp;

    private readonly ReadOnlyMemory<byte> _publicKey = publicKey;

    /// <inheritdoc />
    public override string KeyType => TypeName;

    /// <summary>Gets the ML-DSA parameter set this key uses.</summary>
    public MLDsaAlgorithm Algorithm { get; } = algorithm;

    /// <summary>Gets the JOSE <c>alg</c> / JWK <c>alg</c> identifier of the key (for example <c>ML-DSA-65</c>).</summary>
    public string AlgorithmName => Algorithm.Name;

    /// <summary>Gets a value indicating whether this key holds the private key (and can therefore sign).</summary>
    public abstract bool HasPrivateKey { get; }

    /// <summary>Gets the raw ML-DSA public key as read-only memory (the JWK <c>pub</c> material).</summary>
    protected ReadOnlyMemory<byte> RawPublicKey => _publicKey;

    /// <summary>Writes the raw ML-DSA public key bytes to the destination (the JWK <c>pub</c> material).</summary>
    public void ExportPublicKey<TWriter>(ref TWriter destination)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var source = _publicKey.Span;
        var span = destination.GetSpan(source.Length);
        source.CopyTo(span);
        destination.Advance(source.Length);
    }

    /// <summary>
    /// Creates an <see cref="MLDsa"/> instance from this key's material. A private key yields a signing instance; a
    /// public-only key yields a verify-only instance. The caller owns the returned instance and must dispose it.
    /// </summary>
    /// <returns>The newly created <see cref="MLDsa"/> instance.</returns>
    public abstract MLDsa ExportMLDsa();

    /// <inheritdoc />
    public override void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)
    {
        using var key = MLDsa.ImportMLDsaPublicKey(Algorithm, _publicKey.Span);
        var subjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo();
        var span = destination.GetSpan(subjectPublicKeyInfo.Length);
        subjectPublicKeyInfo.CopyTo(span);
        destination.Advance(subjectPublicKeyInfo.Length);
    }
}
