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
using NCode.Identity.Jose.DPoP;
using NCode.Identity.OpenId.Client.ProofOfPossession;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.ProofOfPossession;

public class DefaultDPoPProofFactoryTests : BaseTests, IAsyncLifetime
{
    private const string Authority = "https://op.example.test";

    private DPoPTestContext DPoPContext { get; } = new();

    private ClientRegistration Registration { get; } =
        new(Authority, "client-1", "none") { DPoPKeyRing = DPoPTestContext.KeyRingName };

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await DPoPContext.DisposeAsync();

    private Mock<IProviderMetadataResolver> CreateMetadataMock()
    {
        var mock = CreateStrictMock<IProviderMetadataResolver>();
        mock.Setup(x => x.GetMetadataAsync(Authority, It.IsAny<CancellationToken>()))
            .Returns(
                ValueTask.FromResult(
                    new OpenIdProviderMetadata { DPoPSigningAlgValuesSupported = ["ES256"] }
                )
            );
        return mock;
    }

    private DefaultDPoPProofFactory CreateFactory(Mock<IProviderMetadataResolver> mock) =>
        new(mock.Object, DPoPContext.CreateResolver(DPoPContext.EcKey), DPoPContext.NonceCache);

    private DPoPProof Validate(string proof, string uri)
    {
        var valid = DPoPContext.ProofService.TryValidateProof(
            proof,
            new DPoPProofValidationParameters
            {
                HttpMethod = "GET",
                HttpUri = uri,
                AllowedAlgorithms = ["ES256"],
                Lifetime = TimeSpan.FromMinutes(1),
                AccessToken = "at",
            },
            out var proofResult,
            out var error
        );

        Assert.True(valid, error?.ToString());
        return Assert.IsType<DPoPProof>(proofResult);
    }

    #region CreateResourceProofAsync Tests

    [Fact]
    public async Task CreateResourceProofAsync_WithPinnedKey_CreatesProofWithAth()
    {
        var factory = CreateFactory(CreateMetadataMock());
        const string uri = "https://api.example.test/orders";

        var proof = await factory.CreateResourceProofAsync(
            Registration,
            DPoPContext.Thumbprint(DPoPContext.EcKey),
            HttpMethod.Get,
            new Uri(uri),
            "at",
            CancellationToken.None
        );

        var result = Validate(proof, uri);
        Assert.Equal(DPoPContext.Thumbprint(DPoPContext.EcKey), result.JwkThumbprint);
        Assert.True(DPoPTestContext.DecodePayload(proof).TryGetProperty("ath", out _));
    }

    [Fact]
    public async Task CreateResourceProofAsync_AfterRecordNonce_CarriesNonce()
    {
        var factory = CreateFactory(CreateMetadataMock());
        factory.RecordNonce(new Uri("https://api.example.test/x"), "server-nonce");
        const string uri = "https://api.example.test/orders";

        var proof = await factory.CreateResourceProofAsync(
            Registration,
            DPoPContext.Thumbprint(DPoPContext.EcKey),
            HttpMethod.Get,
            new Uri(uri),
            "at",
            CancellationToken.None
        );

        Assert.Equal("server-nonce", Validate(proof, uri).Nonce);
    }

    [Fact]
    public async Task CreateResourceProofAsync_WithUnknownThumbprint_Throws()
    {
        var factory = CreateFactory(CreateMetadataMock());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateResourceProofAsync(
                Registration,
                "unknown-thumbprint",
                HttpMethod.Get,
                new Uri("https://api.example.test/orders"),
                "at",
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task CreateResourceProofAsync_WithRelativeUri_Throws()
    {
        var factory = CreateFactory(CreateStrictMock<IProviderMetadataResolver>());

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await factory.CreateResourceProofAsync(
                Registration,
                DPoPContext.Thumbprint(DPoPContext.EcKey),
                HttpMethod.Get,
                new Uri("/orders", UriKind.Relative),
                "at",
                CancellationToken.None
            )
        );
    }

    #endregion

    #region RecordNonce Tests

    [Fact]
    public void RecordNonce_WithEmptyNonce_Throws()
    {
        var factory = CreateFactory(CreateStrictMock<IProviderMetadataResolver>());

        Assert.Throws<ArgumentException>(() =>
            factory.RecordNonce(new Uri("https://api.example.test/x"), "")
        );
    }

    #endregion
}
