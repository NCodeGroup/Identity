// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.DPoP;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.ProofOfPossession;

public class DPoPProofKeyTests : IAsyncLifetime
{
    private const string HttpUri = "https://op.example.test/token";

    private DPoPTestContext DPoPContext { get; } = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await DPoPContext.DisposeAsync();

    private DPoPProof Validate(string proof, string? accessToken = null)
    {
        var valid = DPoPContext.ProofService.TryValidateProof(
            proof,
            new DPoPProofValidationParameters
            {
                HttpMethod = "POST",
                HttpUri = HttpUri,
                AllowedAlgorithms = ["ES256"],
                Lifetime = TimeSpan.FromMinutes(1),
                AccessToken = accessToken,
            },
            out var proofResult,
            out var error
        );

        Assert.True(valid, error?.ToString());
        return Assert.IsType<DPoPProof>(proofResult);
    }

    #region CreateProofAsync Tests

    [Fact]
    public async Task CreateProofAsync_WithoutNonce_IsValidAndHasNoNonce()
    {
        var proofKey = DPoPContext.CreateProofKey(DPoPContext.EcKey);

        var proof = await proofKey.CreateProofAsync(
            HttpMethod.Post,
            new Uri(HttpUri),
            null,
            CancellationToken.None
        );

        var result = Validate(proof);
        Assert.Null(result.Nonce);
        Assert.Equal(DPoPContext.Thumbprint(DPoPContext.EcKey), result.JwkThumbprint);
    }

    [Fact]
    public async Task CreateProofAsync_AfterRecordNonce_CarriesNonce()
    {
        var proofKey = DPoPContext.CreateProofKey(DPoPContext.EcKey);
        proofKey.RecordNonce(new Uri(HttpUri), "server-nonce");

        var proof = await proofKey.CreateProofAsync(
            HttpMethod.Post,
            new Uri(HttpUri),
            null,
            CancellationToken.None
        );

        var result = Validate(proof);
        Assert.Equal("server-nonce", result.Nonce);
    }

    [Fact]
    public async Task CreateProofAsync_WithAccessToken_IsValidWithAth()
    {
        var proofKey = DPoPContext.CreateProofKey(DPoPContext.EcKey);

        var proof = await proofKey.CreateProofAsync(
            HttpMethod.Post,
            new Uri(HttpUri),
            "at",
            CancellationToken.None
        );

        Validate(proof, "at");
        Assert.True(DPoPTestContext.DecodePayload(proof).TryGetProperty("ath", out _));
    }

    #endregion
}
