// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Issuance;
using NCode.Identity.Jose.SdJwt.Presentation;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.SdJwt.Tests.Presentation;

public sealed class DefaultSdJwtHolderTests : IAsyncLifetime
{
    private const string Audience = "https://verifier.example";
    private const string Nonce = "n-0S6_WzA2Mj";

    private const string ClaimsJson = """
        {
          "iss": "https://issuer.example",
          "given_name": "John",
          "address": { "street_address": "123 Main St", "country": "US" },
          "nationalities": ["US", "DE"]
        }
        """;

    private SdJwtTestHost Host { get; } = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await Host.DisposeAsync();

    private ValueTask<SelectiveDisclosureJwt> IssueAsync(
        params IEnumerable<string> disclosablePaths
    ) =>
        Host.Issuer.IssueAsync(
            new SdJwtIssuanceOptions
            {
                SigningCredentials = Host.IssuerCredentials,
                Claims = JsonElement.Parse(ClaimsJson),
                DisclosablePaths = [.. disclosablePaths],
                HolderKey = Host.HolderJwk,
            },
            CancellationToken.None
        );

    private SdJwtKeyBindingOptions CreateKeyBinding() =>
        new()
        {
            SigningCredentials = Host.HolderCredentials,
            Audience = Audience,
            Nonce = Nonce,
        };

    #region Parse Tests

    [Fact]
    public async Task Parse_WithIssuedSdJwt_RoundTrips()
    {
        var issued = await IssueAsync("/given_name", "/nationalities/0");

        var parsed = Host.Holder.Parse(issued.ToString());

        Assert.Equal(issued.IssuerSignedJwt, parsed.IssuerSignedJwt);
        Assert.Equal(
            issued.Disclosures.Select(x => x.Encoded),
            parsed.Disclosures.Select(x => x.Encoded)
        );
        Assert.Equal(issued.ToString(), parsed.ToString());
    }

    [Fact]
    public async Task Parse_WhenOverTheDisclosureLimit_Throws()
    {
        await using var host = new SdJwtTestHost(options => options.MaxDisclosureCount = 1);
        var issued = await IssueAsync("/given_name", "/nationalities/0");

        var exception = Assert.Throws<SdJwtException>(() => host.Holder.Parse(issued.ToString()));

        Assert.Equal(SdJwtVerificationFailureCodes.DisclosureLimitExceeded, exception.FailureCode);
    }

    #endregion

    #region ResolveDisclosures Tests

    [Fact]
    public async Task ResolveDisclosures_MapsEachDisclosureToItsPathAndParent()
    {
        var issued = await IssueAsync(
            "/given_name",
            "/address",
            "/address/street_address",
            "/nationalities/1"
        );

        var resolved = Host.Holder.ResolveDisclosures(issued);

        Assert.Equal(4, resolved.Count);
        var byPath = resolved.ToDictionary(x => x.Path, StringComparer.Ordinal);
        Assert.Null(byPath["/given_name"].Parent);
        Assert.Null(byPath["/address"].Parent);
        Assert.Same(byPath["/address"].Disclosure, byPath["/address/street_address"].Parent);
        Assert.Null(byPath["/nationalities/1"].Parent);
        Assert.Equal("DE", byPath["/nationalities/1"].Disclosure.Value.GetString());
    }

    [Fact]
    public void ResolveDisclosures_WhenIssuerJwtIsNotACompactJws_Throws()
    {
        var sdJwt = new SelectiveDisclosureJwt("not-a-jwt", [], keyBindingJwt: null);

        var exception = Assert.Throws<SdJwtException>(() => Host.Holder.ResolveDisclosures(sdJwt));

        Assert.Equal(SdJwtVerificationFailureCodes.InvalidSdJwt, exception.FailureCode);
    }

    [Theory]
    [InlineData("eyJhbGciOiJFUzI1NiJ9.@@@@.c2ln", "base64url-encoded JSON object")]
    [InlineData("eyJhbGciOiJFUzI1NiJ9.bm90IGpzb24.c2ln", "base64url-encoded JSON object")]
    [InlineData("eyJhbGciOiJFUzI1NiJ9.e30", "not a JOSE token")]
    public void ResolveDisclosures_WhenTheIssuerJwtPayloadCannotBeRead_Throws(
        string issuerSignedJwt,
        string reason
    )
    {
        // A payload segment that is not base64url, one that is not JSON, and a token with too few segments.
        var sdJwt = new SelectiveDisclosureJwt(issuerSignedJwt, [], keyBindingJwt: null);

        var exception = Assert.Throws<SdJwtException>(() => Host.Holder.ResolveDisclosures(sdJwt));

        Assert.Equal(SdJwtVerificationFailureCodes.InvalidSdJwt, exception.FailureCode);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveDisclosures_WhenPayloadIsNotAnObject_Throws()
    {
        var jwt = await Host.SignIssuerJwt("[]");
        var sdJwt = new SelectiveDisclosureJwt(jwt, [], keyBindingJwt: null);

        var exception = Assert.Throws<SdJwtException>(() => Host.Holder.ResolveDisclosures(sdJwt));

        Assert.Equal(SdJwtVerificationFailureCodes.InvalidSdJwt, exception.FailureCode);
    }

    [Fact]
    public async Task ResolveDisclosures_WhenDigestAlgorithmIsUnregistered_Throws()
    {
        var jwt = await Host.SignIssuerJwt("""{"_sd_alg":"sha-1","_sd":[]}""");
        var sdJwt = new SelectiveDisclosureJwt(jwt, [], keyBindingJwt: null);

        var exception = Assert.Throws<SdJwtException>(() => Host.Holder.ResolveDisclosures(sdJwt));

        Assert.Equal(
            SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm,
            exception.FailureCode
        );
    }

    #endregion

    #region CreatePresentationAsync Tests

    [Fact]
    public async Task CreatePresentationAsync_WithChosenDisclosures_PresentsOnlyThoseInIssuerOrder()
    {
        var issued = await IssueAsync("/given_name", "/nationalities/0", "/nationalities/1");
        var chosen = issued.Disclosures.Where(x => x.IsArrayElement).Reverse().ToArray();

        var presentation = await Host.Holder.CreatePresentationAsync(
            new SdJwtPresentationOptions { SdJwt = issued, Disclosures = chosen },
            CancellationToken.None
        );

        var parsed = Host.Holder.Parse(presentation);
        Assert.Equal(
            issued.Disclosures.Where(x => x.IsArrayElement).Select(x => x.Encoded),
            parsed.Disclosures.Select(x => x.Encoded)
        );
        Assert.False(parsed.HasKeyBinding);
        Assert.EndsWith("~", presentation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePresentationAsync_WithNestedDisclosure_PresentsItsParentToo()
    {
        var issued = await IssueAsync("/address", "/address/street_address");
        var street = Assert.Single(issued.Disclosures, x => x.ClaimName == "street_address");

        var presentation = await Host.Holder.CreatePresentationAsync(
            new SdJwtPresentationOptions { SdJwt = issued, Disclosures = [street] },
            CancellationToken.None
        );

        var parsed = Host.Holder.Parse(presentation);
        Assert.Equal(
            ["address", "street_address"],
            parsed.Disclosures.Select(x => x.ClaimName).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task CreatePresentationAsync_WithNothingChosen_PresentsTheIssuerJwtAlone()
    {
        var issued = await IssueAsync("/given_name");

        var presentation = await Host.Holder.CreatePresentationAsync(
            new SdJwtPresentationOptions { SdJwt = issued },
            CancellationToken.None
        );

        Assert.Equal($"{issued.IssuerSignedJwt}~", presentation);
    }

    [Fact]
    public async Task CreatePresentationAsync_WithKeyBinding_AppendsAKeyBindingJwtOverThePresentation()
    {
        var issued = await IssueAsync("/given_name");

        var presentation = await Host.Holder.CreatePresentationAsync(
            new SdJwtPresentationOptions
            {
                SdJwt = issued,
                Disclosures = issued.Disclosures,
                KeyBinding = CreateKeyBinding(),
            },
            CancellationToken.None
        );

        var parsed = Host.Holder.Parse(presentation);
        Assert.True(parsed.HasKeyBinding);
        var header = Host.JoseSerializer.Parse(parsed.KeyBindingJwt).Header;
        Assert.Equal("kb+jwt", header.GetProperty("typ").GetString());
        Assert.Equal("ES256", header.GetProperty("alg").GetString());

        var payload = Host.DecodePayload(parsed.KeyBindingJwt, Host.HolderKey);
        Assert.Equal(Audience, payload.GetProperty("aud").GetString());
        Assert.Equal(Nonce, payload.GetProperty("nonce").GetString());
        Assert.Equal(
            SdJwtTestHost.StartTime.ToUnixTimeSeconds(),
            payload.GetProperty("iat").GetInt64()
        );
        var expectedInput = presentation[..(presentation.LastIndexOf('~') + 1)];
        Assert.Equal(
            SdJwtDigestAlgorithms.Sha256.ComputeDigest(expectedInput),
            payload.GetProperty("sd_hash").GetString()
        );
    }

    [Fact]
    public async Task CreatePresentationAsync_WithKeyBinding_HashesWithTheSdJwtDigestAlgorithm()
    {
        var issued = await Host.Issuer.IssueAsync(
            new SdJwtIssuanceOptions
            {
                SigningCredentials = Host.IssuerCredentials,
                Claims = JsonElement.Parse(ClaimsJson),
                DisclosablePaths = ["/given_name"],
                DigestAlgorithm = SdJwtDigestAlgorithms.Sha384Name,
            },
            CancellationToken.None
        );

        var presentation = await Host.Holder.CreatePresentationAsync(
            new SdJwtPresentationOptions { SdJwt = issued, KeyBinding = CreateKeyBinding() },
            CancellationToken.None
        );

        var parsed = Host.Holder.Parse(presentation);
        Assert.True(parsed.HasKeyBinding);
        var payload = Host.DecodePayload(parsed.KeyBindingJwt, Host.HolderKey);
        var expectedInput = presentation[..(presentation.LastIndexOf('~') + 1)];
        Assert.Equal(
            SdJwtDigestAlgorithms.Sha384.ComputeDigest(expectedInput),
            payload.GetProperty("sd_hash").GetString()
        );
    }

    [Fact]
    public async Task CreatePresentationAsync_WhenTheSdJwtCarriesAKeyBindingJwt_DropsIt()
    {
        var issued = await IssueAsync("/given_name");
        var bound = Host.Holder.Parse(
            await Host.Holder.CreatePresentationAsync(
                new SdJwtPresentationOptions { SdJwt = issued, KeyBinding = CreateKeyBinding() },
                CancellationToken.None
            )
        );
        Assert.True(bound.HasKeyBinding);

        var presentation = await Host.Holder.CreatePresentationAsync(
            new SdJwtPresentationOptions { SdJwt = bound },
            CancellationToken.None
        );

        Assert.False(Host.Holder.Parse(presentation).HasKeyBinding);
    }

    [Fact]
    public async Task CreatePresentationAsync_WithAForeignDisclosure_Throws()
    {
        var issued = await IssueAsync("/given_name");
        var other = await IssueAsync("/given_name");

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Holder.CreatePresentationAsync(
                new SdJwtPresentationOptions { SdJwt = issued, Disclosures = other.Disclosures },
                CancellationToken.None
            )
        );

        Assert.Contains(
            "not a disclosure of the SD-JWT",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task CreatePresentationAsync_WhenKeyBindingKeyIsSymmetric_Throws()
    {
        var issued = await IssueAsync("/given_name");
        var secretKeyFactory = Host.ServiceProvider.GetRequiredService<ISecretKeyFactory>();
        var symmetricKey = secretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "hmac" },
            new byte[32]
        );
        var keyBinding = new SdJwtKeyBindingOptions
        {
            SigningCredentials = Host.CreateCredentials(symmetricKey, "HS256"),
            Audience = Audience,
            Nonce = Nonce,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Holder.CreatePresentationAsync(
                new SdJwtPresentationOptions { SdJwt = issued, KeyBinding = keyBinding },
                CancellationToken.None
            )
        );

        Assert.Contains("asymmetric", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", Nonce, "audience")]
    [InlineData(Audience, "", "nonce")]
    public async Task CreatePresentationAsync_WhenKeyBindingInputIsEmpty_Throws(
        string audience,
        string nonce,
        string reason
    )
    {
        var issued = await IssueAsync("/given_name");
        var keyBinding = new SdJwtKeyBindingOptions
        {
            SigningCredentials = Host.HolderCredentials,
            Audience = audience,
            Nonce = nonce,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Holder.CreatePresentationAsync(
                new SdJwtPresentationOptions { SdJwt = issued, KeyBinding = keyBinding },
                CancellationToken.None
            )
        );

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
