// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.SdJwt.Digests;

namespace NCode.Identity.Jose.SdJwt.Tests.Digests;

public class SdJwtDigestAlgorithmTests
{
    #region ComputeDigest Tests

    [Fact]
    public void ComputeDigest_WithSha256_MatchesSpecVector()
    {
        var digest = SdJwtDigestAlgorithms.Sha256.ComputeDigest(SpecVectors.FamilyNameDisclosure);

        Assert.Equal(SpecVectors.FamilyNameDigest, digest);
    }

    [Fact]
    public void ComputeDigest_WithValueOverTheStackLimit_HashesWholeValue()
    {
        var value = new string('a', 1000);
        var expected = Base64Url.Encode(SHA512.HashData(SecureEncoding.ASCII.GetBytes(value)));

        var digest = SdJwtDigestAlgorithms.Sha512.ComputeDigest(value);

        Assert.Equal(expected, digest);
    }

    [Fact]
    public void ComputeDigest_WhenValueIsNotAscii_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SdJwtDigestAlgorithms.Sha256.ComputeDigest("M\u00f6bius")
        );
    }

    #endregion

    #region DigestSizeBytes Property Tests

    [Fact]
    public void DigestSizeBytes_ForBuiltInAlgorithms_MatchesHash()
    {
        Assert.Equal(32, SdJwtDigestAlgorithms.Sha256.DigestSizeBytes);
        Assert.Equal(48, SdJwtDigestAlgorithms.Sha384.DigestSizeBytes);
        Assert.Equal(64, SdJwtDigestAlgorithms.Sha512.DigestSizeBytes);
    }

    [Fact]
    public void DigestSizeBytes_WhenHashIsUnsupported_Throws()
    {
        var algorithm = new SdJwtDigestAlgorithm
        {
            Name = "nope",
            HashAlgorithmName = new HashAlgorithmName("NOPE"),
        };

        Assert.Throws<ArgumentException>(() => algorithm.DigestSizeBytes);
    }

    #endregion
}
