// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.SdJwt.Tests.Verification;

public sealed class JwkConfirmationKeyResolverTests : IAsyncLifetime
{
    private SdJwtTestHost Host { get; } = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await Host.DisposeAsync();

    private JwkConfirmationKeyResolver CreateResolver() =>
        new(Host.ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>());

    #region ResolveAsync Tests

    [Fact]
    public async Task ResolveAsync_WithPublicEcJwk_ReturnsThePublicKey()
    {
        var confirmation = JsonElement.Parse($$$"""{"jwk":{{{Host.HolderJwk.ToJsonString()}}}}""");

        var key = await CreateResolver().ResolveAsync(confirmation, CancellationToken.None);

        var eccKey = Assert.IsAssignableFrom<EccSecretKey>(key);
        Assert.Equal(SdJwtTestHost.HolderKeyId, eccKey.KeyId);
        Assert.Equal(256, eccKey.KeySizeBits);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"jwk":"x"}""")]
    [InlineData("""{"jwk":{}}""")]
    [InlineData("""{"jwk":{"kty":"XKP","sk":"AA"}}""")]
    [InlineData("""{"jwk":{"kty":"oct","k":"AAAA"}}""")]
    [InlineData("""{"jwk":{"kty":"EC","crv":"P-256","x":"AA","y":"AA"}}""")]
    public async Task ResolveAsync_WithUnusableJwk_ReturnsNull(string confirmationJson)
    {
        var confirmation = JsonElement.Parse(confirmationJson);

        var key = await CreateResolver().ResolveAsync(confirmation, CancellationToken.None);

        Assert.Null(key);
    }

    [Fact]
    public async Task ResolveAsync_WithAJwkThatCarriesItsPrivateHalf_ReturnsNull()
    {
        // The public half alone resolves, so only the private member makes the difference.
        var jwk = JsonObject.Create(Host.HolderJwk.AsElement());
        Assert.NotNull(jwk);
        jwk["d"] = "AA";
        var confirmation = JsonElement.Parse($$$"""{"jwk":{{{jwk.ToJsonString()}}}}""");

        var key = await CreateResolver().ResolveAsync(confirmation, CancellationToken.None);

        Assert.Null(key);
    }

    [Fact]
    public void Member_IsJwk()
    {
        Assert.Equal("jwk", CreateResolver().Member);
    }

    #endregion
}
