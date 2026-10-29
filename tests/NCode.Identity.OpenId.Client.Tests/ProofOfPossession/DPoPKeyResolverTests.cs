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

using NCode.Identity.OpenId.Client.ProofOfPossession;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.ProofOfPossession;

public class DPoPKeyResolverTests : IAsyncLifetime
{
    private const string Authority = "https://op.example.test";

    private DPoPTestContext DPoPContext { get; } = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await DPoPContext.DisposeAsync();

    private static ClientRegistration CreateRegistration(
        string? keyRing = DPoPTestContext.KeyRingName,
        bool bound = false
    ) =>
        new(Authority, "client-1", "test") { DPoPKeyRing = keyRing, DPoPBoundAccessTokens = bound };

    private static OpenIdProviderMetadata CreateMetadata(params string[] supported) =>
        new() { DPoPSigningAlgValuesSupported = supported.Length == 0 ? null : [.. supported] };

    private static string AlgorithmOf(DPoPProofKey key)
    {
        var proof = key.CreateProof(HttpMethod.Post, new Uri(Authority + "/token"), null);
        return DPoPTestContext.DecodeHeader(proof).GetProperty("alg").GetString()!;
    }

    #region Resolve Tests

    [Fact]
    public void Resolve_WithoutKeyRing_ReturnsNull()
    {
        var resolver = DPoPContext.CreateResolver();

        var result = resolver.Resolve(CreateRegistration(null), CreateMetadata("ES256"), null);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_WithoutKeyRingWhenBound_Throws()
    {
        var resolver = DPoPContext.CreateResolver();

        Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(CreateRegistration(null, true), CreateMetadata("ES256"), null)
        );
    }

    [Fact]
    public void Resolve_WithoutKeyRingWhenPinned_Throws()
    {
        var resolver = DPoPContext.CreateResolver();

        Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(CreateRegistration(null), CreateMetadata("ES256"), "thumbprint")
        );
    }

    [Fact]
    public void Resolve_WithEmptyKeyRing_Throws()
    {
        var resolver = DPoPContext.CreateResolver();

        Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(CreateRegistration(), CreateMetadata("ES256"), null)
        );
    }

    [Fact]
    public void Resolve_WhenProviderLacksDPoP_ReturnsNull()
    {
        var resolver = DPoPContext.CreateResolver(DPoPContext.EcKey);

        var result = resolver.Resolve(CreateRegistration(), CreateMetadata(), null);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_WhenProviderLacksDPoPAndBound_Throws()
    {
        var resolver = DPoPContext.CreateResolver(DPoPContext.EcKey);

        Assert.Throws<OpenIdProviderException>(() =>
            resolver.Resolve(CreateRegistration(bound: true), CreateMetadata(), null)
        );
    }

    [Fact]
    public void Resolve_WithEcAndRsaKeys_PrefersES256()
    {
        var resolver = DPoPContext.CreateResolver(DPoPContext.RsaKey, DPoPContext.EcKey);

        var result = resolver.Resolve(CreateRegistration(), CreateMetadata("PS256", "ES256"), null);

        Assert.NotNull(result);
        Assert.Equal("ES256", AlgorithmOf(result));
        Assert.Equal(DPoPContext.Thumbprint(DPoPContext.EcKey), result.JwkThumbprint);
    }

    [Fact]
    public void Resolve_WithOnlyRsaKey_UsesPS256()
    {
        var resolver = DPoPContext.CreateResolver(DPoPContext.RsaKey);

        var result = resolver.Resolve(CreateRegistration(), CreateMetadata("PS256", "ES256"), null);

        Assert.NotNull(result);
        Assert.Equal("PS256", AlgorithmOf(result));
        Assert.Equal(DPoPContext.Thumbprint(DPoPContext.RsaKey), result.JwkThumbprint);
    }

    [Fact]
    public void Resolve_WhenNoAdvertisedAlgorithmFits_ReturnsNull()
    {
        var resolver = DPoPContext.CreateResolver(DPoPContext.RsaKey);

        var result = resolver.Resolve(CreateRegistration(), CreateMetadata("ES256"), null);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_WhenNoAdvertisedAlgorithmFitsAndBound_Throws()
    {
        var resolver = DPoPContext.CreateResolver(DPoPContext.RsaKey);

        Assert.Throws<OpenIdProviderException>(() =>
            resolver.Resolve(CreateRegistration(bound: true), CreateMetadata("ES256"), null)
        );
    }

    [Fact]
    public void Resolve_WithPinnedThumbprint_UsesThatKey()
    {
        var resolver = DPoPContext.CreateResolver(DPoPContext.EcKey, DPoPContext.RsaKey);
        var pinned = DPoPContext.Thumbprint(DPoPContext.RsaKey);

        var result = resolver.Resolve(
            CreateRegistration(),
            CreateMetadata("ES256", "PS256"),
            pinned
        );

        Assert.NotNull(result);
        Assert.Equal("PS256", AlgorithmOf(result));
        Assert.Equal(pinned, result.JwkThumbprint);
    }

    [Fact]
    public void Resolve_WithUnknownPinnedThumbprint_Throws()
    {
        var resolver = DPoPContext.CreateResolver(DPoPContext.EcKey);

        Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(CreateRegistration(), CreateMetadata("ES256"), "unknown")
        );
    }

    #endregion
}
