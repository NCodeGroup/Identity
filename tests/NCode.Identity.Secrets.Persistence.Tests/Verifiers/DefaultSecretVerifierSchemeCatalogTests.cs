// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

public class DefaultSecretVerifierSchemeCatalogTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_IndexesTheFamilysSchemesByName()
    {
        var catalog = TestSecretVerifierSchemes.CreateCatalog();

        Assert.True(catalog.TryGet(SecretVerifierSchemes.Sha256, out var sha256));
        Assert.Equal(SecretVerifierSchemes.Sha256, sha256.Name);
        Assert.True(catalog.TryGet(SecretVerifierSchemes.Pbkdf2Sha256, out var pbkdf2));
        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, pbkdf2.Name);
        Assert.Equal(2, catalog.Schemes.Count);
        Assert.False(catalog.TryGet("NCODE-SHA256", out _));
    }

    [Fact]
    public void Constructor_WhenTwoSchemesShareAName_KeepsTheLaterOne()
    {
        var first = new NamedSecretVerifierScheme("custom");
        var second = new NamedSecretVerifierScheme("custom");

        var catalog = new DefaultSecretVerifierSchemeCatalog(
            [first, second],
            [TestSecretVerifierSchemes.CreateDataSource("custom")]
        );

        Assert.True(catalog.TryGet("custom", out var scheme));
        Assert.Same(second, scheme);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has$dollar")]
    [InlineData("has space")]
    [InlineData("non-ascii-é")]
    public void Constructor_WhenANameIsInvalid_Throws(string name)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultSecretVerifierSchemeCatalog(
                [new NamedSecretVerifierScheme(name)],
                [TestSecretVerifierSchemes.CreateDataSource(name)]
            )
        );
    }

    [Fact]
    public void Constructor_WhenASchemeHasNoDescriptor_ThrowsNamingIt()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultSecretVerifierSchemeCatalog(
                [new NamedSecretVerifierScheme("custom")],
                [TestSecretVerifierSchemes.CreateDataSource()]
            )
        );

        Assert.Equal(
            "The secret verifier scheme 'custom' has no registered SecretVerifierSchemeDescriptor.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_WhenADescriptorNamesNoScheme_ThrowsNamingIt()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultSecretVerifierSchemeCatalog(
                [],
                [TestSecretVerifierSchemes.CreateDataSource("custom")]
            )
        );

        Assert.Equal(
            "The SecretVerifierSchemeDescriptor 'custom' names no registered secret verifier scheme.",
            exception.Message
        );
    }

    #endregion
}
