// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

public class DefaultSecretVerifierSchemeCatalogTests
{
    private sealed class NamedScheme(string name) : ISecretVerifierScheme
    {
        public string Name { get; } = name;

        public void WriteParameters(ReadOnlySpan<byte> secret, IBufferWriter<byte> destination) =>
            throw new NotSupportedException();

        public bool TryReadParameters(
            KeyMetadata metadata,
            ReadOnlySpan<byte> parameters,
            [NotNullWhen(true)] out SecretVerifier? verifier
        ) => throw new NotSupportedException();
    }

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
        var first = new NamedScheme("custom");
        var second = new NamedScheme("custom");

        var catalog = new DefaultSecretVerifierSchemeCatalog([first, second]);

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
            new DefaultSecretVerifierSchemeCatalog([new NamedScheme(name)])
        );
    }

    #endregion
}
