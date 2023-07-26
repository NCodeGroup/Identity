#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Cryptography.Keys;
using NCode.Encoders;
using NCode.Jose.Exceptions;
using NCode.Jose.KeyManagement;

namespace NCode.Jose.Tests.KeyManagement;

public class EcdhKeyManagementAlgorithmTests : BaseTests
{
    private EcdhKeyManagementAlgorithm Algorithm { get; } = new();

    private static ECCurve GetCurve(int curveSizeBits)
    {
        var curve = curveSizeBits switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => throw new NotSupportedException()
        };
        return curve;
    }

    [Fact]
    public void Code_Valid()
    {
        Assert.Equal("ECDH-ES", Algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        Assert.Equal(typeof(EccSecretKey), Algorithm.KeyType);
    }

    [Fact]
    [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
    public void KeyBitSizes_Valid()
    {
        Assert.Collection(
            Algorithm.KeyBitSizes,
            size =>
            {
                Assert.Equal(256, size.MinSize);
                Assert.Equal(384, size.MaxSize);
                Assert.Equal(128, size.SkipSize);
            },
            size =>
            {
                Assert.Equal(521, size.MinSize);
                Assert.Equal(521, size.MaxSize);
                Assert.Equal(0, size.SkipSize);
            });
    }

    [Theory]
    [InlineData(256)]
    [InlineData(384)]
    [InlineData(521)]
    public void ExportKey_Valid(int curveSizeBits)
    {
        var curve = GetCurve(curveSizeBits);

        using var key = ECDiffieHellman.Create(curve);
        var parameters = key.ExportParameters(includePrivateParameters: true);

        var headers = new Dictionary<string, object>();
        EcdhKeyManagementAlgorithm.ExportKey(curveSizeBits, key, headers);

        var epk = Assert.IsType<Dictionary<string, object>>(Assert.Contains("epk", headers));
        var kty = Assert.IsType<string>(Assert.Contains("kty", epk));
        var crv = Assert.IsType<string>(Assert.Contains("crv", epk));
        var x = Assert.IsType<string>(Assert.Contains("x", epk));
        var y = Assert.IsType<string>(Assert.Contains("y", epk));
        var d = Assert.IsType<string>(Assert.Contains("d", epk));

        Assert.Equal("EC", kty);
        Assert.Equal($"P-{curveSizeBits}", crv);
        Assert.Equal(Base64Url.Encode(parameters.Q.X), x);
        Assert.Equal(Base64Url.Encode(parameters.Q.Y), y);
        Assert.Equal(Base64Url.Encode(parameters.D), d);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_Valid()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);
        using var expectedKey = ECDiffieHellman.Create(curve);
        var expectedParameters = expectedKey.ExportParameters(includePrivateParameters: false);
        var encodedX = Base64Url.Encode(expectedParameters.Q.X);
        var encodedY = Base64Url.Encode(expectedParameters.Q.Y);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var encodedApu = Base64Url.Encode(expectedApu);
        var encodedApv = Base64Url.Encode(expectedApv);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = encodedX,
                ["y"] = encodedY
            },
            ["apu"] = encodedApu,
            ["apv"] = encodedApv
        };

        using var key = Algorithm.ValidateHeaderForUnwrap(
            curve,
            curveSizeBits,
            header,
            out var algorithm,
            out var apu,
            out var apv);

        Assert.Equal(expectedAlgorithm, algorithm);
        Assert.Equal(encodedApu, apu);
        Assert.Equal(encodedApv, apv);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingAlgorithm()
    {
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new Dictionary<string, object>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                header,
                out _,
                out _,
                out _));

        Assert.Equal("The JWT header is missing the 'enc' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingKey()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm
        };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                header,
                out _,
                out _,
                out _));

        Assert.Equal("The JWT header is missing the 'epk' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingKeyType()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new Dictionary<string, object>()
        };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                header,
                out _,
                out _,
                out _));

        Assert.Equal("The 'epk' header is missing the 'kty' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_InvalidKeyType()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = "NotEC"
            }
        };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                header,
                out _,
                out _,
                out _));

        Assert.Equal("The 'kty' field was expected to be 'EC'.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingCurve()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = "EC"
            }
        };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                header,
                out _,
                out _,
                out _));

        Assert.Equal("The 'epk' header is missing the 'crv' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingX()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}"
            }
        };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                header,
                out _,
                out _,
                out _));

        Assert.Equal("The 'epk' header is missing the 'x' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingY()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);
        using var expectedKey = ECDiffieHellman.Create(curve);
        var expectedParameters = expectedKey.ExportParameters(includePrivateParameters: false);
        var encodedX = Base64Url.Encode(expectedParameters.Q.X);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = encodedX
            }
        };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                header,
                out _,
                out _,
                out _));

        Assert.Equal("The 'epk' header is missing the 'y' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingApu()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);
        using var expectedKey = ECDiffieHellman.Create(curve);
        var expectedParameters = expectedKey.ExportParameters(includePrivateParameters: false);
        var encodedX = Base64Url.Encode(expectedParameters.Q.X);
        var encodedY = Base64Url.Encode(expectedParameters.Q.Y);

        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApv);

        var encodedApv = Base64Url.Encode(expectedApv);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = encodedX,
                ["y"] = encodedY
            },
            ["apv"] = encodedApv
        };

        using var key = Algorithm.ValidateHeaderForUnwrap(
            curve,
            curveSizeBits,
            header,
            out var algorithm,
            out var apu,
            out var apv);

        Assert.Equal(expectedAlgorithm, algorithm);
        Assert.Null(apu);
        Assert.Equal(encodedApv, apv);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_MissingApv()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);
        using var expectedKey = ECDiffieHellman.Create(curve);
        var expectedParameters = expectedKey.ExportParameters(includePrivateParameters: false);
        var encodedX = Base64Url.Encode(expectedParameters.Q.X);
        var encodedY = Base64Url.Encode(expectedParameters.Q.Y);

        Span<byte> expectedApu = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);

        var encodedApu = Base64Url.Encode(expectedApu);

        var header = new Dictionary<string, object>
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = encodedX,
                ["y"] = encodedY
            },
            ["apu"] = encodedApu
        };

        using var key = Algorithm.ValidateHeaderForUnwrap(
            curve,
            curveSizeBits,
            header,
            out var algorithm,
            out var apu,
            out var apv);

        Assert.Equal(expectedAlgorithm, algorithm);
        Assert.Equal(encodedApu, apu);
        Assert.Null(apv);
    }

    [Fact]
    public void GetLegalCekByteSizes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var results = Algorithm.GetLegalCekByteSizes(kekSizeBits);
        var result = Assert.Single(results);
        Assert.Equal(1, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(1, result.SkipSize);
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var result = Algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(0, result);
    }

    [Fact]
    public void TryWrapKey_Valid()
    {
        Assert.Throws<JoseException>(() =>
            Algorithm.TryWrapKey(null!, null!, Span<byte>.Empty, Span<byte>.Empty, out _));
    }

    public static IEnumerable<object[]> RoundTripTestData
    {
        get
        {
            var curves = new[]
            {
                ECCurve.NamedCurves.nistP256,
                ECCurve.NamedCurves.nistP384,
                ECCurve.NamedCurves.nistP521
            };

            var keySizes = new[] { 16, 24, 32 };

            foreach (var curve in curves)
            foreach (var keySize in keySizes)
                yield return new object[] { curve, keySize };
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripTestData))]
    public void RoundTrip_Valid(ECCurve curve, int keySizeBytes)
    {
        const string keyId = nameof(keyId);
        const string enc = "dir";
        const string kty = "EC";

        var apu = new byte[16];
        RandomNumberGenerator.Fill(apu);

        var apv = new byte[32];
        RandomNumberGenerator.Fill(apv);

        // party 1

        using var key1 = ECDiffieHellman.Create(curve);
        using var secretKey1 = EccSecretKey.Create(keyId, key1);
        var parameters1 = key1.ExportParameters(includePrivateParameters: false);

        var header1 = new Dictionary<string, object>
        {
            ["enc"] = enc,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv)
        };

        var cek1 = new byte[keySizeBytes];
        Algorithm.NewKey(secretKey1, header1, cek1);

        // party 2

        var epk = (IDictionary<string, object>)header1["epk"];
        var parameters2 = new ECParameters
        {
            Curve = curve,
            Q = new ECPoint
            {
                X = Base64Url.Decode((string)epk["x"]),
                Y = Base64Url.Decode((string)epk["y"]),
            },
            D = Base64Url.Decode((string)epk["d"])
        };
        using var key2 = ECDiffieHellman.Create(parameters2);
        using var secretKey2 = EccSecretKey.Create(keyId, key2);

        var crv = $"P-{secretKey2.KeySizeBits}";
        var header2 = new Dictionary<string, object>
        {
            ["enc"] = enc,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv),
            ["epk"] = new Dictionary<string, object>
            {
                ["kty"] = kty,
                ["crv"] = crv,
                ["x"] = Base64Url.Encode(parameters1.Q.X),
                ["y"] = Base64Url.Encode(parameters1.Q.Y)
            }
        };

        var cek2 = new byte[keySizeBytes];
        var result = Algorithm.TryUnwrapKey(secretKey2, header2, Array.Empty<byte>(), cek2, out var bytesWritten);

        // assert

        Assert.True(result);
        Assert.Equal(keySizeBytes, bytesWritten);
        Assert.Equal(cek1, cek2);

        // control

        var keySizeBits = keySizeBytes << 3;
        var controlKey = global::Jose.keys.EccKey.New(parameters2.Q.X, parameters2.Q.Y, parameters2.D, CngKeyUsages.KeyAgreement);
        var controlAlgorithm = new global::Jose.EcdhKeyManagement(true);
        var controlResult = controlAlgorithm.Unwrap(Array.Empty<byte>(), controlKey, keySizeBits, header2);
        Assert.Equal(controlResult, cek1);
    }
}
