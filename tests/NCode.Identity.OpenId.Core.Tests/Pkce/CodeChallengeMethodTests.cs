// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Moq;
using NCode.Buffers;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Pkce;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Pkce;

public sealed class CodeChallengeMethodTests : IDisposable
{
    private MockRepository MockRepository { get; }

    public CodeChallengeMethodTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
    }

    public void Dispose() => MockRepository.Verify();

    #region S256CodeChallengeMethod

    [Fact]
    public void S256_Method_IsS256()
    {
        var mockCryptoService = MockRepository.Create<ICryptoService>();
        var method = new S256CodeChallengeMethod(mockCryptoService.Object);
        Assert.Equal(OpenIdProtocol.CodeChallengeMethods.S256, method.Method);
    }

    [Fact]
    public void S256_ComputeChallenge_HashesVerifierAsBase64UrlSha256()
    {
        var mockCryptoService = MockRepository.Create<ICryptoService>();
        mockCryptoService
            .Setup(x =>
                x.HashValue(
                    "the-verifier",
                    HashAlgorithmName.SHA256,
                    BinaryEncodings.Base64Url,
                    SecureEncoding.ASCII
                )
            )
            .Returns("the-challenge")
            .Verifiable();

        var method = new S256CodeChallengeMethod(mockCryptoService.Object);

        Assert.Equal("the-challenge", method.ComputeChallenge("the-verifier"));
    }

    #endregion

    #region DefaultCodeChallengeMethodCollectionProvider

    [Fact]
    public void Provider_SupportedMethods_ReflectsRegisteredMethods()
    {
        var s256 = CreateS256Method();
        var provider = new DefaultCodeChallengeMethodCollectionProvider([s256]);

        Assert.Equal([OpenIdProtocol.CodeChallengeMethods.S256], provider.SupportedMethods);
    }

    [Fact]
    public void Provider_TryGet_RegisteredMethod_ReturnsTrueAndHandler()
    {
        var s256 = CreateS256Method();
        var provider = new DefaultCodeChallengeMethodCollectionProvider([s256]);

        var found = provider.TryGet(OpenIdProtocol.CodeChallengeMethods.S256, out var method);

        Assert.True(found);
        Assert.Same(s256, method);
    }

    [Fact]
    public void Provider_TryGet_UnknownMethod_ReturnsFalse()
    {
        var s256 = CreateS256Method();
        var provider = new DefaultCodeChallengeMethodCollectionProvider([s256]);

        var found = provider.TryGet("plain", out var method);

        Assert.False(found);
        Assert.Null(method);
    }

    #endregion

    #region Scaffolding

    private S256CodeChallengeMethod CreateS256Method()
    {
        var mockCryptoService = MockRepository.Create<ICryptoService>();
        return new S256CodeChallengeMethod(mockCryptoService.Object);
    }

    #endregion
}
