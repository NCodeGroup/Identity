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
using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Jwks.Converters;

public class RsaJsonWebKeyConverterTests : BaseTests
{
    #region TryConvert Tests

    [Fact]
    public void TryConvert_GivenRsaSecretKey_ThenReturnsRsaJsonWebKey()
    {
        var converter = new RsaJsonWebKeyConverter();

        var metadata = new KeyMetadata
        {
            KeyId = "kid-1",
            Use = "sig",
            Algorithm = "RS256",
        };
        var rsa = RSA.Create(2048);

        var mockSecretKey = CreateStrictMock<RsaSecretKey>();
        mockSecretKey.Setup(x => x.Metadata).Returns(metadata).Verifiable();
        mockSecretKey.Setup(x => x.ExportRSA()).Returns(rsa).Verifiable();

        var result = converter.TryConvert(mockSecretKey.Object, out var jsonWebKey);

        Assert.True(result);
        var rsaJsonWebKey = Assert.IsType<RsaJsonWebKey>(jsonWebKey);
        Assert.Equal("RSA", rsaJsonWebKey.KeyType);
        Assert.Equal("kid-1", rsaJsonWebKey.KeyId);
        Assert.Equal("sig", rsaJsonWebKey.Use);
        Assert.Equal("RS256", rsaJsonWebKey.Algorithm);
        Assert.False(string.IsNullOrEmpty(rsaJsonWebKey.Modulus));
        Assert.False(string.IsNullOrEmpty(rsaJsonWebKey.Exponent));
    }

    [Fact]
    public void TryConvert_GivenRsaSecretKeyWithoutMetadata_ThenOmitsOptionalMembers()
    {
        var converter = new RsaJsonWebKeyConverter();

        var metadata = new KeyMetadata();
        var rsa = RSA.Create(2048);

        var mockSecretKey = CreateStrictMock<RsaSecretKey>();
        mockSecretKey.Setup(x => x.Metadata).Returns(metadata).Verifiable();
        mockSecretKey.Setup(x => x.ExportRSA()).Returns(rsa).Verifiable();

        converter.TryConvert(mockSecretKey.Object, out var jsonWebKey);

        var rsaJsonWebKey = Assert.IsType<RsaJsonWebKey>(jsonWebKey);
        Assert.Null(rsaJsonWebKey.KeyId);
        Assert.Null(rsaJsonWebKey.Use);
        Assert.Null(rsaJsonWebKey.Algorithm);
    }

    [Fact]
    public void TryConvert_GivenNonRsaSecretKey_ThenReturnsFalse()
    {
        var converter = new RsaJsonWebKeyConverter();
        var mockSecretKey = CreateStrictMock<EccSecretKey>();

        var result = converter.TryConvert(mockSecretKey.Object, out var jsonWebKey);

        Assert.False(result);
        Assert.Null(jsonWebKey);
    }

    #endregion
}
