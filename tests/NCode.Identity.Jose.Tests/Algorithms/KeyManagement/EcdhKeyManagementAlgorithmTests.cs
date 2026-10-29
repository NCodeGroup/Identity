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

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jose;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Secrets.Keys;
using Base64Url = NCode.Encoders.Base64Url;
using JoseException = NCode.Identity.Jose.Exceptions.JoseException;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

public class EcdhKeyManagementAlgorithmTests : BaseTests
{
    private static EcdhKeyManagementAlgorithm Algorithm => EcdhKeyManagementAlgorithm.Singleton;

    private static ECCurve GetCurve(int curveSizeBits)
    {
        var curve = curveSizeBits switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => throw new NotSupportedException(),
        };
        return curve;
    }

    [Fact]
    public void Code_ReturnsEcdhEs()
    {
        Assert.Equal("ECDH-ES", Algorithm.Code);
    }

    [Fact]
    public void KeyType_ReturnsEccSecretKey()
    {
        Assert.Equal(typeof(EccSecretKey), Algorithm.KeyType);
    }

    [Fact]
    [SuppressMessage("ReSharper", "ParameterOnlyUsedForPreconditionCheck.Local")]
    public void KeyBitSizes_ReturnsExpected()
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
            }
        );
    }

    [Theory]
    [InlineData(256)]
    [InlineData(384)]
    [InlineData(521)]
    public void ExportKey_WhenGivenValidCurve_ExportsOnlyThePublicKeyToHeader(int curveSizeBits)
    {
        var curve = GetCurve(curveSizeBits);

        using var key = ECDiffieHellman.Create(curve);
        var parameters = key.ExportParameters(includePrivateParameters: false);

        var header = new Dictionary<string, object>();
        EcdhKeyManagementAlgorithm.ExportKey(curveSizeBits, key, header);

        var epk = Assert.IsType<Dictionary<string, object>>(Assert.Contains("epk", header));
        var kty = Assert.IsType<string>(Assert.Contains("kty", epk));
        var crv = Assert.IsType<string>(Assert.Contains("crv", epk));
        var x = Assert.IsType<string>(Assert.Contains("x", epk));
        var y = Assert.IsType<string>(Assert.Contains("y", epk));

        Assert.Equal("EC", kty);
        Assert.Equal($"P-{curveSizeBits}", crv);
        Assert.Equal(Base64Url.Encode(parameters.Q.X), x);
        Assert.Equal(Base64Url.Encode(parameters.Q.Y), y);

        // RFC 7518 §4.6.1.1: the ephemeral private key never leaves the sender.
        Assert.Equal(["crv", "kty", "x", "y"], epk.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenTheEphemeralKeyCarriesItsPrivateKey_Throws()
    {
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);
        using var ephemeralKey = ECDiffieHellman.Create(curve);
        var parameters = ephemeralKey.ExportParameters(includePrivateParameters: true);

        var header = new JsonObject
        {
            ["enc"] = "A128GCM",
            ["epk"] = new JsonObject
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = Base64Url.Encode(parameters.Q.X),
                ["y"] = Base64Url.Encode(parameters.Q.Y),
                ["d"] = Base64Url.Encode(parameters.D),
            },
        };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                headerForUnwrap,
                out _,
                out _,
                out _
            )
        );

        Assert.Equal(
            "The 'epk' header must contain only public key parameters, but it contains 'd'.",
            exception.Message
        );
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenValidHeader_Succeeds()
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

        var header = new JsonObject
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new JsonObject
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = encodedX,
                ["y"] = encodedY,
            },
            ["apu"] = encodedApu,
            ["apv"] = encodedApv,
        };

        var headerForUnwrap = header.Deserialize<JsonElement>();

        using var key = Algorithm.ValidateHeaderForUnwrap(
            curve,
            curveSizeBits,
            headerForUnwrap,
            out var algorithm,
            out var apu,
            out var apv
        );

        Assert.Equal(expectedAlgorithm, algorithm);
        Assert.Equal(encodedApu, apu);
        Assert.Equal(encodedApv, apv);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingAlgorithm_Throws()
    {
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new JsonObject();
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                headerForUnwrap,
                out _,
                out _,
                out _
            )
        );

        Assert.Equal("The JWT header is missing the 'enc' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingKey_Throws()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new JsonObject { ["enc"] = expectedAlgorithm };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                headerForUnwrap,
                out _,
                out _,
                out _
            )
        );

        Assert.Equal("The JWT header is missing the 'epk' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingKeyType_Throws()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new JsonObject { ["enc"] = expectedAlgorithm, ["epk"] = new JsonObject() };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                headerForUnwrap,
                out _,
                out _,
                out _
            )
        );

        Assert.Equal("The 'epk' header is missing the 'kty' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenInvalidKeyType_Throws()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new JsonObject
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new JsonObject { ["kty"] = "NotEC" },
        };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                headerForUnwrap,
                out _,
                out _,
                out _
            )
        );

        Assert.Equal("The 'kty' field was expected to be 'EC'.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingCurve_Throws()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new JsonObject
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new JsonObject { ["kty"] = "EC" },
        };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                headerForUnwrap,
                out _,
                out _,
                out _
            )
        );

        Assert.Equal("The 'epk' header is missing the 'crv' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingX_Throws()
    {
        const string expectedAlgorithm = nameof(expectedAlgorithm);
        const int curveSizeBits = 256;

        var curve = GetCurve(curveSizeBits);

        Span<byte> expectedApu = new byte[32];
        Span<byte> expectedApv = new byte[32];
        RandomNumberGenerator.Fill(expectedApu);
        RandomNumberGenerator.Fill(expectedApv);

        var header = new JsonObject
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new JsonObject { ["kty"] = "EC", ["crv"] = $"P-{curveSizeBits}" },
        };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                headerForUnwrap,
                out _,
                out _,
                out _
            )
        );

        Assert.Equal("The 'epk' header is missing the 'x' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingY_Throws()
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

        var header = new JsonObject
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new JsonObject
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = encodedX,
            },
        };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(
                curve,
                curveSizeBits,
                headerForUnwrap,
                out _,
                out _,
                out _
            )
        );

        Assert.Equal("The 'epk' header is missing the 'y' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingApu_Succeeds()
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

        var header = new JsonObject
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new JsonObject
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = encodedX,
                ["y"] = encodedY,
            },
            ["apv"] = encodedApv,
        };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        using var key = Algorithm.ValidateHeaderForUnwrap(
            curve,
            curveSizeBits,
            headerForUnwrap,
            out var algorithm,
            out var apu,
            out var apv
        );

        Assert.Equal(expectedAlgorithm, algorithm);
        Assert.Null(apu);
        Assert.Equal(encodedApv, apv);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingApv_Succeeds()
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

        var header = new JsonObject
        {
            ["enc"] = expectedAlgorithm,
            ["epk"] = new JsonObject
            {
                ["kty"] = "EC",
                ["crv"] = $"P-{curveSizeBits}",
                ["x"] = encodedX,
                ["y"] = encodedY,
            },
            ["apu"] = encodedApu,
        };
        var headerForUnwrap = header.Deserialize<JsonElement>();

        using var key = Algorithm.ValidateHeaderForUnwrap(
            curve,
            curveSizeBits,
            headerForUnwrap,
            out var algorithm,
            out var apu,
            out var apv
        );

        Assert.Equal(expectedAlgorithm, algorithm);
        Assert.Equal(encodedApu, apu);
        Assert.Null(apv);
    }

    [Fact]
    public void GetLegalCekByteSizes_ReturnsExpected()
    {
        var kekSizeBits = Random.Shared.Next();
        var results = Algorithm.GetLegalCekByteSizes(kekSizeBits);
        var result = Assert.Single(results);
        Assert.Equal(1, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(1, result.SkipSize);
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_ReturnsExpected()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var result = Algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(0, result);
    }

    [Fact]
    public void WrapKey_WhenCalled_Throws()
    {
        Assert.Throws<JoseException>(() =>
            Algorithm.WrapKey(null!, null!, Span<byte>.Empty, new ArrayBufferWriter<byte>())
        );
    }

    public static IEnumerable<object[]> RoundTripTestData
    {
        get
        {
            var curves = new[]
            {
                ECCurve.NamedCurves.nistP256,
                ECCurve.NamedCurves.nistP384,
                ECCurve.NamedCurves.nistP521,
            };

            var keySizes = new[] { 16, 24, 32 };

            foreach (var curve in curves)
            foreach (var keySize in keySizes)
                yield return new object[] { curve, keySize };
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripTestData))]
    public void RoundTrip_EncryptsAndDecrypts(ECCurve curve, int keySizeBytes)
    {
        const string keyId = nameof(keyId);
        const string enc = "dir";

        var apu = new byte[16];
        RandomNumberGenerator.Fill(apu);

        var apv = new byte[32];
        RandomNumberGenerator.Fill(apv);

        var metadata = new KeyMetadata { KeyId = keyId };

        // the sender agrees on a key from the recipient's public key and an ephemeral key of its own

        using var recipientKey = ECDiffieHellman.Create(curve);
        var recipientSecretKey = SecretKeyFactory.CreateEcc(metadata, recipientKey);

        var header = new Dictionary<string, object>
        {
            ["enc"] = enc,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv),
        };

        var cek1 = new byte[keySizeBytes];
        Algorithm.NewKey(recipientSecretKey, header, cek1);

        // RFC 7518 §4.6.1.1: the header carries only the ephemeral public key.
        var epk = Assert.IsType<Dictionary<string, object>>(Assert.Contains("epk", header));
        Assert.Equal(["crv", "kty", "x", "y"], epk.Keys.Order(StringComparer.Ordinal));

        // the recipient agrees on the same key from its private key and the ephemeral public key

        var cek2 = new byte[keySizeBytes];
        var result = Algorithm.TryUnwrapKey(
            recipientSecretKey,
            JsonSerializer.SerializeToElement(header),
            Array.Empty<byte>(),
            cek2,
            out var bytesWritten
        );

        Assert.True(result);
        Assert.Equal(keySizeBytes, bytesWritten);
        Assert.Equal(cek1, cek2);

        // control
        //
        // EcdhKeyManagementUnix is the BCL-backed (ECDiffieHellman) control implementation; despite the name it is the
        // cross-platform one jose-jwt uses on every platform for managed keys. The CNG-backed EcdhKeyManagementWin
        // delegates straight to it whenever the key is an ECDiffieHellman/ECDsa/Jwk, so driving it directly keeps the
        // same third-party oracle without a Windows-only key type.

        var keySizeBits = keySizeBytes << 3;
        var controlAlgorithm = new EcdhKeyManagementUnix(true);
        var controlResult = controlAlgorithm.Unwrap([], recipientKey, keySizeBits, header);
        Assert.Equal(controlResult, cek1);
    }
}
