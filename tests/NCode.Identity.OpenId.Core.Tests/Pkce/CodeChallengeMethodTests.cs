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

    #region PlainCodeChallengeMethod

    [Fact]
    public void Plain_Method_IsPlain()
    {
        var method = new PlainCodeChallengeMethod();
        Assert.Equal(OpenIdConstants.CodeChallengeMethods.Plain, method.Method);
    }

    [Fact]
    public void Plain_ComputeChallenge_ReturnsVerifierVerbatim()
    {
        var method = new PlainCodeChallengeMethod();
        Assert.Equal("the-verifier", method.ComputeChallenge("the-verifier"));
    }

    #endregion

    #region S256CodeChallengeMethod

    [Fact]
    public void S256_Method_IsS256()
    {
        var mockCryptoService = MockRepository.Create<ICryptoService>();
        var method = new S256CodeChallengeMethod(mockCryptoService.Object);
        Assert.Equal(OpenIdConstants.CodeChallengeMethods.S256, method.Method);
    }

    [Fact]
    public void S256_ComputeChallenge_HashesVerifierAsBase64UrlSha256()
    {
        var mockCryptoService = MockRepository.Create<ICryptoService>();
        mockCryptoService
            .Setup(x =>
                x.HashValue(
                    "the-verifier",
                    HashAlgorithmType.Sha256,
                    BinaryEncodingType.Base64Url,
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
        var provider = new DefaultCodeChallengeMethodCollectionProvider([
            new PlainCodeChallengeMethod(),
        ]);

        Assert.Equal(
            [OpenIdConstants.CodeChallengeMethods.Plain],
            provider.SupportedMethods.Order()
        );
    }

    [Fact]
    public void Provider_TryGet_RegisteredMethod_ReturnsTrueAndHandler()
    {
        var plain = new PlainCodeChallengeMethod();
        var provider = new DefaultCodeChallengeMethodCollectionProvider([plain]);

        var found = provider.TryGet(OpenIdConstants.CodeChallengeMethods.Plain, out var method);

        Assert.True(found);
        Assert.Same(plain, method);
    }

    [Fact]
    public void Provider_TryGet_UnknownMethod_ReturnsFalse()
    {
        var provider = new DefaultCodeChallengeMethodCollectionProvider([
            new PlainCodeChallengeMethod(),
        ]);

        var found = provider.TryGet("unknown", out var method);

        Assert.False(found);
        Assert.Null(method);
    }

    #endregion
}
