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

public class EccJsonWebKeyConverterTests : BaseTests
{
    private static EccJsonWebKeyConverter CreateConverter() =>
        new(new DefaultEccCurveSpecificationRegistry([]));

    #region TryConvert Tests

    [Fact]
    public void TryConvert_GivenSupportedEccSecretKey_ThenReturnsEccJsonWebKey()
    {
        var converter = CreateConverter();

        const int curveSizeBits = 256;
        var metadata = new KeyMetadata { KeyId = "kid-2", Use = "sig" };
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var mockSecretKey = CreateStrictMock<EccSecretKey>();
        mockSecretKey.Setup(x => x.KeySizeBits).Returns(curveSizeBits).Verifiable();
        mockSecretKey.Setup(x => x.ExportECDsa()).Returns(ecdsa).Verifiable();
        mockSecretKey.Setup(x => x.Metadata).Returns(metadata).Verifiable();

        var result = converter.TryConvert(mockSecretKey.Object, out var jsonWebKey);

        Assert.True(result);
        var eccJsonWebKey = Assert.IsType<EccJsonWebKey>(jsonWebKey);
        Assert.Equal("EC", eccJsonWebKey.KeyType);
        Assert.Equal("P-256", eccJsonWebKey.Curve);
        Assert.Equal("kid-2", eccJsonWebKey.KeyId);
        Assert.False(string.IsNullOrEmpty(eccJsonWebKey.X));
        Assert.False(string.IsNullOrEmpty(eccJsonWebKey.Y));
    }

    [Fact]
    public void TryConvert_GivenUnsupportedCurveSize_ThenReturnsFalse()
    {
        var converter = CreateConverter();

        const int unsupportedCurveSizeBits = 999;

        var mockSecretKey = CreateStrictMock<EccSecretKey>();
        mockSecretKey.Setup(x => x.KeySizeBits).Returns(unsupportedCurveSizeBits).Verifiable();

        var result = converter.TryConvert(mockSecretKey.Object, out var jsonWebKey);

        Assert.False(result);
        Assert.Null(jsonWebKey);
    }

    [Fact]
    public void TryConvert_GivenNonEccSecretKey_ThenReturnsFalse()
    {
        var converter = CreateConverter();
        var mockSecretKey = CreateStrictMock<RsaSecretKey>();

        var result = converter.TryConvert(mockSecretKey.Object, out var jsonWebKey);

        Assert.False(result);
        Assert.Null(jsonWebKey);
    }

    #endregion
}
