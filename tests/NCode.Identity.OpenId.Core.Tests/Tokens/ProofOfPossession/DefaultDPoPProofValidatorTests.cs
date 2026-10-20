#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Moq;
using NCode.Encoders;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.ProofOfPossession;

public class DefaultDPoPProofValidatorTests : BaseTests
{
    private const string HttpMethod = "POST";
    private const string HttpUri = "https://op.test/token";

    private Mock<IEccCurveCatalog> MockCurveCatalog { get; }
    private Mock<IDPoPProofReplayCache> MockReplayCache { get; }
    private Mock<IDPoPNonceService> MockNonceService { get; }
    private Mock<IReadOnlySettingCollection> MockSettings { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdClient> MockClient { get; }

    private DefaultDPoPProofValidator Validator { get; }

    public DefaultDPoPProofValidatorTests()
    {
        MockCurveCatalog = CreateStrictMock<IEccCurveCatalog>();
        MockReplayCache = CreateStrictMock<IDPoPProofReplayCache>();
        MockNonceService = CreateStrictMock<IDPoPNonceService>();
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockClient = CreateStrictMock<OpenIdClient>();

        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        MockClient.SetupGet(x => x.Settings).Returns(MockSettings.Object).Verifiable();

        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DPoPSigningAlgValuesSupported))
            .Returns(["ES256"]);
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.DPoPProofLifetime))
            .Returns(TimeSpan.FromSeconds(60));
        MockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.DPoPNonceRequired)).Returns(false);

        Validator = new DefaultDPoPProofValidator(
            TimeProvider.System,
            MockCurveCatalog.Object,
            MockReplayCache.Object,
            MockNonceService.Object
        );
    }

    #region Scaffolding

    private void SetupCurveCatalog(ECDsa ecdsa)
    {
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var specification = new EccCurveSpecification
        {
            CurveName = "P-256",
            CurveSizeBits = 256,
            Curve = parameters.Curve,
        };
        MockCurveCatalog
            .Setup(x => x.TryGetByName("P-256", out specification!))
            .Returns(true)
            .Verifiable();
    }

    private static string BuildProof(
        ECDsa ecdsa,
        string htm = HttpMethod,
        string htu = HttpUri,
        DateTimeOffset? iat = null,
        string? jti = null,
        string typ = "dpop+jwt",
        string alg = "ES256",
        object? iatClaim = null
    )
    {
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64Url.Encode(parameters.Q.X!),
            ["y"] = Base64Url.Encode(parameters.Q.Y!),
        };
        var header = new Dictionary<string, object>
        {
            ["typ"] = typ,
            ["alg"] = alg,
            ["jwk"] = jwk,
        };
        var payload = new Dictionary<string, object>
        {
            ["htm"] = htm,
            ["htu"] = htu,
            ["iat"] = iatClaim ?? (iat ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
            ["jti"] = jti ?? Guid.NewGuid().ToString("N"),
        };

        var encodedHeader = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";
        var signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation
        );
        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    private static string ComputeExpectedThumbprint(ECDsa ecdsa)
    {
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var x = Base64Url.Encode(parameters.Q.X!);
        var y = Base64Url.Encode(parameters.Q.Y!);
        var canonical = $$"""{"crv":"P-256","kty":"EC","x":"{{x}}","y":"{{y}}"}""";
        return Base64Url.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    #endregion

    #region ValidateAsync Tests

    private ValueTask<string> InvokeValidateAsync(string proof) =>
        Validator.ValidateAsync(
            MockContext.Object,
            new DPoPProofValidationRequest
            {
                ProofJwt = proof,
                HttpMethod = HttpMethod,
                HttpUri = HttpUri,
                Settings = MockClient.Object.Settings,
            },
            CancellationToken.None
        );

    [Fact]
    public async Task ValidateAsync_WhenValidEs256Proof_ReturnsJwkThumbprint()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupCurveCatalog(ecdsa);
        MockReplayCache
            .Setup(x =>
                x.TryRecordAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

        var proof = BuildProof(ecdsa);

        var thumbprint = await InvokeValidateAsync(proof);

        Assert.Equal(ComputeExpectedThumbprint(ecdsa), thumbprint);
    }

    [Fact]
    public async Task ValidateAsync_WhenWrongTyp_Throws()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = BuildProof(ecdsa, typ: "jwt");

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync(proof).AsTask());
    }

    [Fact]
    public async Task ValidateAsync_WhenDisallowedAlg_Throws()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var proof = BuildProof(ecdsa, alg: "HS256");

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync(proof).AsTask());
    }

    [Fact]
    public async Task ValidateAsync_WhenHtuMismatch_Throws()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupCurveCatalog(ecdsa);
        var proof = BuildProof(ecdsa, htu: "https://op.test/other");

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync(proof).AsTask());
    }

    [Fact]
    public async Task ValidateAsync_WhenStaleIat_Throws()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupCurveCatalog(ecdsa);
        var proof = BuildProof(ecdsa, iat: DateTimeOffset.UtcNow.AddMinutes(-10));

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync(proof).AsTask());
    }

    [Theory]
    [InlineData(99_999_999_999_999L)]
    [InlineData(long.MinValue)]
    public async Task ValidateAsync_WhenIatOutOfRange_ThrowsInvalidProof(long iat)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupCurveCatalog(ecdsa);
        var proof = BuildProof(ecdsa, iatClaim: iat);

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync(proof).AsTask());
    }

    [Fact]
    public async Task ValidateAsync_WhenIatIsString_ThrowsInvalidProof()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupCurveCatalog(ecdsa);
        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var proof = BuildProof(ecdsa, iatClaim: iat);

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync(proof).AsTask());
    }

    [Fact]
    public async Task ValidateAsync_WhenIatIsFractional_ReturnsJwkThumbprint()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupCurveCatalog(ecdsa);
        MockReplayCache
            .Setup(x =>
                x.TryRecordAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

        var proof = BuildProof(ecdsa, iatClaim: DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 0.5);

        var thumbprint = await InvokeValidateAsync(proof);

        Assert.Equal(ComputeExpectedThumbprint(ecdsa), thumbprint);
    }

    [Fact]
    public async Task ValidateAsync_WhenReplayed_Throws()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupCurveCatalog(ecdsa);
        MockReplayCache
            .Setup(x =>
                x.TryRecordAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false)
            .Verifiable();

        var proof = BuildProof(ecdsa);

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync(proof).AsTask());
    }

    [Fact]
    public async Task ValidateAsync_WhenSignatureTampered_Throws()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SetupCurveCatalog(ecdsa);
        var proof = BuildProof(ecdsa);
        var lastDot = proof.LastIndexOf('.');
        var signature = proof[(lastDot + 1)..];
        var flipped = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        var tampered = proof[..(lastDot + 1)] + flipped;

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync(tampered).AsTask());
    }

    #endregion
}
