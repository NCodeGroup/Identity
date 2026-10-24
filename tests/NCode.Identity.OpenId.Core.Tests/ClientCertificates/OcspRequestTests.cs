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

using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class OcspRequestTests
{
    #region Create Tests

    [Fact]
    public void Create_HashesTheIssuersNameAndKeyWithSha1()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");

        var certificateId = OcspCertificateId.Create(leaf, root);

        Assert.Equal(OcspCertificateId.Sha1Oid, certificateId.HashAlgorithm);
        Assert.Equal(
            CryptographicOperations.HashData(HashAlgorithmName.SHA1, leaf.IssuerName.RawData),
            certificateId.IssuerNameHash.ToArray()
        );
        Assert.Equal(
            CryptographicOperations.HashData(
                HashAlgorithmName.SHA1,
                root.PublicKey.EncodedKeyValue.RawData
            ),
            certificateId.IssuerKeyHash.ToArray()
        );
    }

    [Fact]
    public void Create_UsesTheCertificatesSerialNumber()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");

        var certificateId = OcspCertificateId.Create(leaf, root);

        Assert.Equal(
            new BigInteger(leaf.SerialNumberBytes.Span, isUnsigned: false, isBigEndian: true),
            certificateId.SerialNumber
        );
    }

    #endregion

    #region Encode Tests

    [Fact]
    public void Encode_WritesOneRequestForTheCertificateId()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var certificateId = OcspCertificateId.Create(leaf, root);

        var encoded = OcspRequest.Encode(certificateId);

        var reader = new AsnReader(encoded, AsnEncodingRules.DER);
        var ocspRequest = reader.ReadSequence();
        reader.ThrowIfNotEmpty();
        var tbsRequest = ocspRequest.ReadSequence();
        ocspRequest.ThrowIfNotEmpty();
        var requestList = tbsRequest.ReadSequence();
        tbsRequest.ThrowIfNotEmpty();
        var request = requestList.ReadSequence();
        requestList.ThrowIfNotEmpty();
        var actual = OcspCertificateId.Read(request);
        request.ThrowIfNotEmpty();
        Assert.True(certificateId.Matches(actual));
    }

    #endregion

    #region Matches Tests

    [Fact]
    public void Matches_WhenTheSerialNumberDiffers_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        using var other = TestCertificates.CreateClientCertificate(root, "CN=c");
        var certificateId = OcspCertificateId.Create(leaf, root);
        var otherId = OcspCertificateId.Create(other, root);

        var result = certificateId.Matches(otherId);

        Assert.False(result);
    }

    #endregion
}
