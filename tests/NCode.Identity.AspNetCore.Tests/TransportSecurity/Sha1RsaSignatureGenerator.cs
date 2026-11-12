// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.AspNetCore.Tests.TransportSecurity;

/// <summary>
/// Signs with RSA PKCS#1 and SHA-1 (sha1WithRSAEncryption), which the transport certificate inspector must refuse. The
/// framework's signature generator does not sign with SHA-1.
/// </summary>
internal sealed class Sha1RsaSignatureGenerator(RSA rsa) : X509SignatureGenerator
{
    private static ReadOnlySpan<byte> AlgorithmIdentifier =>
        [0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x05, 0x05, 0x00];

    protected override PublicKey BuildPublicKey() =>
        CreateForRSA(rsa, RSASignaturePadding.Pkcs1).PublicKey;

    public override byte[] GetSignatureAlgorithmIdentifier(HashAlgorithmName hashAlgorithm) =>
        AlgorithmIdentifier.ToArray();

    public override byte[] SignData(byte[] data, HashAlgorithmName hashAlgorithm) =>
        rsa.SignData(data, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
}
