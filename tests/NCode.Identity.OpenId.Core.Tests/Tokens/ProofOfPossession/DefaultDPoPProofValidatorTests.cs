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

using System.Text.Json;
using Moq;
using NCode.Identity.Jose.DPoP;
using NCode.Identity.Jwk;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.ProofOfPossession;

public class DefaultDPoPProofValidatorTests : BaseTests
{
    private const string HttpMethod = "POST";
    private const string HttpUri = "https://op.test/token";
    private const string ProofJwt = "header.payload.signature";
    private const string Thumbprint = "thumbprint";
    private const string Jti = "jti-1";

    private static readonly DateTimeOffset IssuedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private Mock<IDPoPProofService> MockProofService { get; }
    private Mock<IDPoPProofReplayCache> MockReplayCache { get; }
    private Mock<IDPoPNonceService> MockNonceService { get; }
    private Mock<IReadOnlySettingCollection> MockSettings { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdClient> MockClient { get; }

    private DefaultDPoPProofValidator Validator { get; }

    public DefaultDPoPProofValidatorTests()
    {
        MockProofService = CreateStrictMock<IDPoPProofService>();
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
        MockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.DPoPProofLifetime)).Returns(Lifetime);
        MockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.DPoPNonceRequired)).Returns(false);

        Validator = new DefaultDPoPProofValidator(
            MockProofService.Object,
            MockReplayCache.Object,
            MockNonceService.Object
        );
    }

    #region Scaffolding

    private static DPoPProof CreateProof(string? nonce = null) =>
        new()
        {
            Algorithm = "ES256",
            PublicKey = new JsonWebKey(
                JsonDocument.Parse("""{"kty":"EC","crv":"P-256","x":"x","y":"y"}""").RootElement
            ),
            JwkThumbprint = Thumbprint,
            Jti = Jti,
            IssuedAt = IssuedAt,
            Nonce = nonce,
        };

    private void SetupProofService(DPoPProof? proof, string? error = null)
    {
        MockProofService
            .Setup(x =>
                x.TryValidateProof(
                    ProofJwt,
                    It.Is<DPoPProofValidationParameters>(p =>
                        p.HttpMethod == HttpMethod
                        && p.HttpUri == HttpUri
                        && p.AccessToken == "access-token"
                        && p.Lifetime == Lifetime
                        && p.AllowedAlgorithms.SequenceEqual(new[] { "ES256" })
                    ),
                    out proof,
                    out error
                )
            )
            .Returns(proof is not null)
            .Verifiable();
    }

    private void SetupReplayCache(bool recorded) =>
        MockReplayCache
            .Setup(x =>
                x.TryRecordAsync(
                    MockContext.Object,
                    Thumbprint,
                    Jti,
                    IssuedAt + Lifetime,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(recorded)
            .Verifiable();

    private ValueTask<string> InvokeValidateAsync() =>
        Validator.ValidateAsync(
            MockContext.Object,
            new DPoPProofValidationRequest
            {
                ProofJwt = ProofJwt,
                HttpMethod = HttpMethod,
                HttpUri = HttpUri,
                AccessToken = "access-token",
                Settings = MockClient.Object.Settings,
            },
            CancellationToken.None
        );

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WhenProofValid_RecordsJtiAndReturnsThumbprint()
    {
        SetupProofService(CreateProof());
        SetupReplayCache(recorded: true);

        var thumbprint = await InvokeValidateAsync();

        Assert.Equal(Thumbprint, thumbprint);
    }

    [Fact]
    public async Task ValidateAsync_WhenProofRejected_ThrowsWithoutRecording()
    {
        SetupProofService(proof: null, error: "The DPoP proof signature is invalid.");

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync().AsTask());
    }

    [Fact]
    public async Task ValidateAsync_WhenReplayed_Throws()
    {
        SetupProofService(CreateProof());
        SetupReplayCache(recorded: false);

        await Assert.ThrowsAsync<OpenIdException>(() => InvokeValidateAsync().AsTask());
    }

    [Fact]
    public async Task ValidateAsync_WhenNonceRequiredAndValid_ReturnsThumbprint()
    {
        MockSettings.Setup(x => x.GetValue(OpenIdSettingKeys.DPoPNonceRequired)).Returns(true);
        SetupProofService(CreateProof(nonce: "server-nonce"));
        MockNonceService
            .Setup(x =>
                x.ValidateNonceAsync(
                    MockContext.Object,
                    "server-nonce",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true)
            .Verifiable();
        SetupReplayCache(recorded: true);

        var thumbprint = await InvokeValidateAsync();

        Assert.Equal(Thumbprint, thumbprint);
    }

    #endregion
}
