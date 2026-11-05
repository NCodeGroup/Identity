// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.SdJwt.Digests;

namespace NCode.Identity.Jose.SdJwt.Tests.Digests;

public class DefaultSdJwtDigestAlgorithmCatalogTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WhenNameRepeats_Throws()
    {
        var duplicate = new SdJwtDigestAlgorithm
        {
            Name = SdJwtDigestAlgorithms.Sha256Name,
            HashAlgorithmName = HashAlgorithmName.SHA512,
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultSdJwtDigestAlgorithmCatalog([SdJwtDigestAlgorithms.Sha256, duplicate])
        );

        Assert.Contains("sha-256", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenNameIsEmpty_Throws()
    {
        var unnamed = new SdJwtDigestAlgorithm
        {
            Name = " ",
            HashAlgorithmName = HashAlgorithmName.SHA256,
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultSdJwtDigestAlgorithmCatalog([unnamed])
        );
    }

    [Fact]
    public void Constructor_WhenHashIsUnsupported_Throws()
    {
        var unsupported = new SdJwtDigestAlgorithm
        {
            Name = "nope",
            HashAlgorithmName = new HashAlgorithmName("NOPE"),
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultSdJwtDigestAlgorithmCatalog([unsupported])
        );

        Assert.Contains("nope", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenAlgorithmIsNull_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultSdJwtDigestAlgorithmCatalog([null!])
        );
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_WhenRegistered_ReturnsTheInstance()
    {
        var catalog = new DefaultSdJwtDigestAlgorithmCatalog([
            SdJwtDigestAlgorithms.Sha256,
            SdJwtDigestAlgorithms.Sha384,
        ]);

        Assert.True(catalog.TryGet(SdJwtDigestAlgorithms.Sha384Name, out var algorithm));
        Assert.Same(SdJwtDigestAlgorithms.Sha384, algorithm);
        Assert.Equal(2, catalog.Algorithms.Count);
    }

    [Fact]
    public void TryGet_WhenNotRegistered_ReturnsFalse()
    {
        var catalog = new DefaultSdJwtDigestAlgorithmCatalog([SdJwtDigestAlgorithms.Sha256]);

        Assert.False(catalog.TryGet("SHA-256", out _));
    }

    #endregion
}
