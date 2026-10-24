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
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class RevocationSignatureTests
{
    private const string EcdsaWithSha256Oid = "1.2.840.10045.4.3.2";
    private const string EcdsaWithSha1Oid = "1.2.840.10045.4.1";

    private static readonly byte[] SignedData = [0x01, 0x02, 0x03, 0x04];

    private static byte[] Sign(X509Certificate2 signer, byte[] data)
    {
        using var key = signer.GetECDsaPrivateKey();
        Assert.NotNull(key);
        return key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }

    #region Verify Tests

    [Fact]
    public void Verify_WithTheSignersKey_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var signature = Sign(root, SignedData);

        var result = RevocationSignature.Verify(
            root,
            EcdsaWithSha256Oid,
            ReadOnlyMemory<byte>.Empty,
            SignedData,
            signature
        );

        Assert.True(result);
    }

    [Fact]
    public void Verify_WithAnotherKey_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var other = TestCertificates.CreateRootAuthority("Other");
        var signature = Sign(root, SignedData);

        var result = RevocationSignature.Verify(
            other,
            EcdsaWithSha256Oid,
            ReadOnlyMemory<byte>.Empty,
            SignedData,
            signature
        );

        Assert.False(result);
    }

    [Fact]
    public void Verify_WithAnUnsupportedAlgorithm_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var signature = Sign(root, SignedData);

        var result = RevocationSignature.Verify(
            root,
            EcdsaWithSha1Oid,
            ReadOnlyMemory<byte>.Empty,
            SignedData,
            signature
        );

        Assert.False(result);
    }

    #endregion

    #region VerifyCertificateSignature Tests

    [Fact]
    public void VerifyCertificateSignature_WhenTheIssuerSignedIt_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");

        var result = RevocationSignature.VerifyCertificateSignature(leaf, root);

        Assert.True(result);
    }

    [Fact]
    public void VerifyCertificateSignature_WhenAnotherAuthoritySignedIt_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var other = TestCertificates.CreateRootAuthority("Other");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");

        var result = RevocationSignature.VerifyCertificateSignature(leaf, other);

        Assert.False(result);
    }

    #endregion

    #region TryGetPssHashAlgorithm Tests

    [Fact]
    public void TryGetPssHashAlgorithm_WithSha256ParametersWithoutNull_ReturnsTheHash()
    {
        var parameters = Convert.FromHexString(
            "3030A00D300B0609608648016503040201A11A301806092A864886F70D010108300B0609608648016503040201A203020120"
        );

        var result = RevocationSignature.TryGetPssHashAlgorithm(parameters, out var hashAlgorithm);

        Assert.True(result);
        Assert.Equal(HashAlgorithmName.SHA256, hashAlgorithm);
    }

    [Fact]
    public void TryGetPssHashAlgorithm_WithNullHashParameters_ReturnsTheHash()
    {
        var parameters = Convert.FromHexString(
            "3034A00F300D06096086480165030402010500A11C301A06092A864886F70D010108300D06096086480165030402010500A203020120"
        );

        var result = RevocationSignature.TryGetPssHashAlgorithm(parameters, out var hashAlgorithm);

        Assert.True(result);
        Assert.Equal(HashAlgorithmName.SHA256, hashAlgorithm);
    }

    [Theory]
    [InlineData(
        "3030A00D300B0609608648016503040201A11A301806092A864886F70D010108300B0609608648016503040202A203020120"
    )]
    [InlineData(
        "3030A00D300B0609608648016503040201A11A301806092A864886F70D010108300B0609608648016503040201A203020121"
    )]
    [InlineData(
        "3035A00D300B0609608648016503040201A11A301806092A864886F70D010108300B0609608648016503040201A203020120A303020102"
    )]
    [InlineData(
        "3028A009300706052B0E03021AA116301406092A864886F70D010108300706052B0E03021AA203020114"
    )]
    [InlineData(
        "3030A00D300B0609608648016503040201A11A301806092A864886F70D010109300B0609608648016503040201A203020120"
    )]
    [InlineData("3000")]
    public void TryGetPssHashAlgorithm_WithParametersThatAreNotAccepted_ReturnsFalse(
        string parametersHex
    )
    {
        var parameters = Convert.FromHexString(parametersHex);

        var result = RevocationSignature.TryGetPssHashAlgorithm(parameters, out _);

        Assert.False(result);
    }

    #endregion
}
