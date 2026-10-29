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

    #region CreateProof Tests

    [Fact]
    public void CreateProof_WithoutNonce_IsValidAndHasNoNonce()
    {
        var proofKey = DPoPContext.CreateProofKey(DPoPContext.EcKey);

        var proof = proofKey.CreateProof(HttpMethod.Post, new Uri(HttpUri), null);

        var result = Validate(proof);
        Assert.Null(result.Nonce);
        Assert.Equal(DPoPContext.Thumbprint(DPoPContext.EcKey), result.JwkThumbprint);
    }

    [Fact]
    public void CreateProof_AfterRecordNonce_CarriesNonce()
    {
        var proofKey = DPoPContext.CreateProofKey(DPoPContext.EcKey);
        proofKey.RecordNonce(new Uri(HttpUri), "server-nonce");

        var proof = proofKey.CreateProof(HttpMethod.Post, new Uri(HttpUri), null);

        var result = Validate(proof);
        Assert.Equal("server-nonce", result.Nonce);
    }

    [Fact]
    public void CreateProof_WithAccessToken_IsValidWithAth()
    {
        var proofKey = DPoPContext.CreateProofKey(DPoPContext.EcKey);

        var proof = proofKey.CreateProof(HttpMethod.Post, new Uri(HttpUri), "at");

        Validate(proof, "at");
        Assert.True(DPoPTestContext.DecodePayload(proof).TryGetProperty("ath", out _));
    }

    #endregion
}
