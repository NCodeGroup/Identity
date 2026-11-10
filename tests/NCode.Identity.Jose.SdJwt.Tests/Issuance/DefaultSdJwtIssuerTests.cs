// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Issuance;
using NCode.Identity.Jose.SdJwt.Profiles;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.SdJwt.Tests.Issuance;

public sealed class DefaultSdJwtIssuerTests : IAsyncLifetime
{
    private const string ClaimsJson = """
        {
          "iss": "https://issuer.example",
          "given_name": "John",
          "family_name": "Doe",
          "address": { "street_address": "123 Main St", "country": "US" },
          "nationalities": ["US", "DE"],
          "verified": null
        }
        """;

    private SdJwtTestHost Host { get; } = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await Host.DisposeAsync();

    private static JsonElement Claims => JsonElement.Parse(ClaimsJson);

    private SdJwtIssuanceOptions CreateOptions(params IEnumerable<string> disclosablePaths) =>
        new()
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            DisclosablePaths = [.. disclosablePaths],
        };

    private ValueTask<JsonElement> DecodeIssuerPayloadAsync(
        SelectiveDisclosureJwt sdJwt,
        CancellationToken cancellationToken
    ) => Host.DecodePayloadAsync(sdJwt.IssuerSignedJwt, Host.IssuerKey, cancellationToken);

    private static string[] ReadDigests(JsonElement obj) =>
        [.. obj.GetProperty("_sd").EnumerateArray().Select(x => x.GetString() ?? string.Empty)];

    #region IssueAsync Tests

    [Fact]
    public async Task IssueAsync_WithDisclosableMembers_MovesThemIntoSaltedDisclosures()
    {
        var sdJwt = await Host.Issuer.IssueAsync(
            CreateOptions("/given_name", "/family_name"),
            CancellationToken.None
        );

        var payload = await DecodeIssuerPayloadAsync(sdJwt, CancellationToken.None);
        Assert.False(payload.TryGetProperty("given_name", out _));
        Assert.False(payload.TryGetProperty("family_name", out _));
        Assert.Equal("https://issuer.example", payload.GetProperty("iss").GetString());
        Assert.Equal("sha-256", payload.GetProperty("_sd_alg").GetString());

        Assert.Equal(2, sdJwt.Disclosures.Length);
        var byName = sdJwt.Disclosures.ToDictionary(
            x => x.ClaimName ?? string.Empty,
            StringComparer.Ordinal
        );
        Assert.Equal("John", byName["given_name"].Value.GetString());
        Assert.Equal("Doe", byName["family_name"].Value.GetString());
        Assert.NotEqual(byName["given_name"].Salt, byName["family_name"].Salt);
        Assert.All(sdJwt.Disclosures, x => Assert.Equal(22, x.Salt.Length));

        var digests = ReadDigests(payload);
        Assert.Equal(digests.Order(StringComparer.Ordinal), digests);
        Assert.Equal(
            sdJwt
                .Disclosures.Select(x => x.ComputeDigest(SdJwtDigestAlgorithms.Sha256))
                .Order(StringComparer.Ordinal),
            digests
        );
        Assert.Null(sdJwt.KeyBindingJwt);
        Assert.EndsWith("~", sdJwt.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WithNestedPath_PutsTheInnerDigestInTheOuterDisclosure()
    {
        var sdJwt = await Host.Issuer.IssueAsync(
            CreateOptions("/address", "/address/street_address"),
            CancellationToken.None
        );

        var payload = await DecodeIssuerPayloadAsync(sdJwt, CancellationToken.None);
        Assert.False(payload.TryGetProperty("address", out _));
        Assert.Single(ReadDigests(payload));

        var address = Assert.Single(sdJwt.Disclosures, x => x.ClaimName == "address");
        var street = Assert.Single(sdJwt.Disclosures, x => x.ClaimName == "street_address");
        Assert.Equal("US", address.Value.GetProperty("country").GetString());
        Assert.False(address.Value.TryGetProperty("street_address", out _));
        Assert.Equal(
            [street.ComputeDigest(SdJwtDigestAlgorithms.Sha256)],
            ReadDigests(address.Value)
        );
        Assert.False(address.Value.TryGetProperty("_sd_alg", out _));
    }

    [Fact]
    public async Task IssueAsync_WithArrayElement_WritesAPlaceholderInPlace()
    {
        var sdJwt = await Host.Issuer.IssueAsync(
            CreateOptions("/nationalities/1"),
            CancellationToken.None
        );

        var payload = await DecodeIssuerPayloadAsync(sdJwt, CancellationToken.None);
        var nationalities = payload.GetProperty("nationalities");
        Assert.Equal(2, nationalities.GetArrayLength());
        Assert.Equal("US", nationalities[0].GetString());
        var placeholder = nationalities[1];
        Assert.Single(placeholder.EnumerateObject());

        var disclosure = Assert.Single(sdJwt.Disclosures);
        Assert.True(disclosure.IsArrayElement);
        Assert.Equal("DE", disclosure.Value.GetString());
        Assert.Equal(
            disclosure.ComputeDigest(SdJwtDigestAlgorithms.Sha256),
            placeholder.GetProperty("...").GetString()
        );
        Assert.False(payload.TryGetProperty("_sd", out _));
    }

    [Fact]
    public async Task IssueAsync_WithDecoys_AddsDigestsThatMatchNoDisclosure()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            DisclosablePaths = ["/given_name", "/nationalities/0"],
            DecoyDigestCount = 3,
        };

        var sdJwt = await Host.Issuer.IssueAsync(options, CancellationToken.None);

        var payload = await DecodeIssuerPayloadAsync(sdJwt, CancellationToken.None);
        Assert.Equal(4, ReadDigests(payload).Length);
        Assert.Equal(5, payload.GetProperty("nationalities").GetArrayLength());
        Assert.Equal(2, sdJwt.Disclosures.Length);
        Assert.Equal(
            4,
            payload
                .GetProperty("nationalities")
                .EnumerateArray()
                .Count(x => x.ValueKind == JsonValueKind.Object)
        );
    }

    [Fact]
    public async Task IssueAsync_WithoutDecoysOnAnUndisclosedArray_LeavesTheArrayAlone()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            DisclosablePaths = ["/given_name"],
            DecoyDigestCount = 2,
        };

        var sdJwt = await Host.Issuer.IssueAsync(options, CancellationToken.None);

        var payload = await DecodeIssuerPayloadAsync(sdJwt, CancellationToken.None);
        Assert.Equal(2, payload.GetProperty("nationalities").GetArrayLength());
        Assert.Equal(3, ReadDigests(payload).Length);
    }

    [Fact]
    public async Task IssueAsync_WithNullValue_DisclosesNull()
    {
        var sdJwt = await Host.Issuer.IssueAsync(
            CreateOptions("/verified"),
            CancellationToken.None
        );

        var disclosure = Assert.Single(sdJwt.Disclosures);
        Assert.Equal("verified", disclosure.ClaimName);
        Assert.Equal(JsonValueKind.Null, disclosure.Value.ValueKind);
        Assert.Equal(JsonValueKind.Null, Disclosure.Parse(disclosure.Encoded).Value.ValueKind);
    }

    [Fact]
    public async Task IssueAsync_WithEscapedPointer_FindsTheMember()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse("""{"a/b":1,"c~d":2}"""),
            DisclosablePaths = ["/a~1b", "/c~0d"],
        };

        var sdJwt = await Host.Issuer.IssueAsync(options, CancellationToken.None);

        Assert.Equal(
            ["a/b", "c~d"],
            sdJwt.Disclosures.Select(x => x.ClaimName).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task IssueAsync_WithoutDisclosablePaths_WritesNoSdAlg()
    {
        var sdJwt = await Host.Issuer.IssueAsync(CreateOptions(), CancellationToken.None);

        var payload = await DecodeIssuerPayloadAsync(sdJwt, CancellationToken.None);
        Assert.False(payload.TryGetProperty("_sd_alg", out _));
        Assert.False(payload.TryGetProperty("_sd", out _));
        Assert.Empty(sdJwt.Disclosures);
        Assert.Equal(
            "JWT",
            Host.JoseSerializer.Parse(sdJwt.IssuerSignedJwt).Header.GetProperty("typ").GetString()
        );
    }

    [Fact]
    public async Task IssueAsync_WithHolderKey_WritesCnfJwkWithPublicMembersOnly()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            HolderKey = Host.HolderJwk,
        };

        var sdJwt = await Host.Issuer.IssueAsync(options, CancellationToken.None);

        var cnf = (await DecodeIssuerPayloadAsync(sdJwt, CancellationToken.None)).GetProperty(
            "cnf"
        );
        var jwk = new JsonWebKey(cnf.GetProperty("jwk"));
        Assert.Equal("EC", jwk.KeyType);
        Assert.False(jwk.ContainsPrivateKeyMaterial);
        Assert.Equal(SdJwtTestHost.HolderKeyId, jwk.KeyId);
    }

    [Fact]
    public async Task IssueAsync_WithAnotherDigestAlgorithm_WritesItsName()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            DisclosablePaths = ["/given_name"],
            DigestAlgorithm = SdJwtDigestAlgorithms.Sha512Name,
        };

        var sdJwt = await Host.Issuer.IssueAsync(options, CancellationToken.None);

        var payload = await DecodeIssuerPayloadAsync(sdJwt, CancellationToken.None);
        Assert.Equal("sha-512", payload.GetProperty("_sd_alg").GetString());
        var disclosure = Assert.Single(sdJwt.Disclosures);
        Assert.Equal(
            [disclosure.ComputeDigest(SdJwtDigestAlgorithms.Sha512)],
            ReadDigests(payload)
        );
    }

    [Fact]
    public async Task IssueAsync_WithProfile_TypesTheJwtAndKeepsProtectedClaims()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse(
                """{"iss":"https://issuer.example","vct":"https://credentials.example/id","sub":"x"}"""
            ),
            DisclosablePaths = ["/sub"],
            Profile = SdJwtProfiles.SdJwtVc,
        };

        var sdJwt = await Host.Issuer.IssueAsync(options, CancellationToken.None);

        var header = Host.JoseSerializer.Parse(sdJwt.IssuerSignedJwt).Header;
        Assert.Equal("dc+sd-jwt", header.GetProperty("typ").GetString());
        Assert.Equal("sub", Assert.Single(sdJwt.Disclosures).ClaimName);
    }

    [Fact]
    public async Task IssueAsync_WithProfileAndProtectedPath_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse("""{"iss":"https://issuer.example","vct":"t"}"""),
            DisclosablePaths = ["/iss"],
            Profile = SdJwtProfiles.SdJwtVc,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains(
            "never selectively disclosable",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task IssueAsync_WithProfileAndAPathBeneathAProtectedClaim_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse(
                """{"iss":"https://issuer.example","vct":"t","status":{"status_list":{"idx":0,"uri":"u"}}}"""
            ),
            DisclosablePaths = ["/status/status_list"],
            Profile = SdJwtProfiles.SdJwtVc,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("'status'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SD-JWT VC", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/iss")]
    [InlineData("/exp")]
    [InlineData("/nbf")]
    [InlineData("/cnf")]
    [InlineData("/cnf/jwk")]
    [InlineData("/exp/0")]
    public async Task IssueAsync_WithoutProfileAndAFormatProtectedPath_Throws(string path)
    {
        // The verifier judges these claims on the issuer-signed payload before it resolves a disclosure, so an SD-JWT
        // that disclosed one would never verify.
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse(
                """{"iss":"https://issuer.example","exp":[1],"nbf":1,"cnf":{"jwk":{}}}"""
            ),
            DisclosablePaths = [path],
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains(
            "never selectively disclosable",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("profile", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WithALargerSaltSize_SizesEverySaltByTheOption()
    {
        await using var host = new SdJwtTestHost(options => options.SaltSizeBytes = 32);
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = host.IssuerCredentials,
            Claims = Claims,
            DisclosablePaths = ["/given_name", "/family_name", "/nationalities/0"],
            DecoyDigestCount = 1,
        };

        var sdJwt = await host.Issuer.IssueAsync(options, CancellationToken.None);

        Assert.Equal(3, sdJwt.Disclosures.Length);
        Assert.All(
            sdJwt.Disclosures,
            x =>
            {
                Assert.Equal(43, x.Salt.Length);
                Assert.Equal(32, System.Buffers.Text.Base64Url.DecodeFromChars(x.Salt).Length);
            }
        );
        Assert.Equal(
            3,
            sdJwt.Disclosures.Select(x => x.Salt).Distinct(StringComparer.Ordinal).Count()
        );
    }

    [Fact]
    public async Task IssueAsync_WithProfileAndMissingRequiredClaim_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse("""{"iss":"https://issuer.example","vct":""}"""),
            Profile = SdJwtProfiles.SdJwtVc,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("'vct'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("given_name", "JSON Pointer")]
    [InlineData("", "JSON Pointer")]
    [InlineData("/missing", "names no claim")]
    [InlineData("/nationalities/01", "names no claim")]
    [InlineData("/nationalities/2", "names no claim")]
    [InlineData("/address/zip", "names no claim")]
    public async Task IssueAsync_WithUnusablePath_Throws(string path, string reason)
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(CreateOptions(path), CancellationToken.None)
        );

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WithRepeatedPath_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(
                CreateOptions("/given_name", "/given_name"),
                CancellationToken.None
            )
        );

        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"_sd":[]}""")]
    [InlineData("""{"a":{"_sd_alg":"sha-256"}}""")]
    [InlineData("""{"a":[{"...":"x"}]}""")]
    public async Task IssueAsync_WithReservedName_Throws(string claimsJson)
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse(claimsJson),
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("reserved", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WhenClaimsAreNotAnObject_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse("[]"),
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );
    }

    [Fact]
    public async Task IssueAsync_WhenDigestAlgorithmIsUnregistered_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            DigestAlgorithm = "sha-1",
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("sha-1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WhenSigningKeyIsSymmetric_Throws()
    {
        var secretKeyFactory = Host.ServiceProvider.GetRequiredService<ISecretKeyFactory>();
        var symmetricKey = secretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "hmac" },
            new byte[32]
        );
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.CreateCredentials(symmetricKey, "HS256"),
            Claims = Claims,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("asymmetric", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WhenHolderKeyCarriesPrivateMaterial_Throws()
    {
        var privateJwk = JsonWebKey.Parse(
            """{"kty":"EC","crv":"P-256","x":"AA","y":"AA","d":"AA"}"""
        );
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            HolderKey = privateJwk,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("public members", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WhenHolderKeyIsSymmetric_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            HolderKey = JsonWebKey.Parse("""{"kty":"oct","k":"AAAA"}"""),
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("asymmetric", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WhenHolderKeyTypeIsUnregistered_Throws()
    {
        // A key the registered types cannot read is screened by no type's JWK rules, so it is refused outright.
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            HolderKey = JsonWebKey.Parse("""{"kty":"XKP","pub":"AA"}"""),
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("'XKP'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("not registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WhenClaimsCarryCnfAndAHolderKeyIsGiven_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse("""{"cnf":{}}"""),
            HolderKey = Host.HolderJwk,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("'cnf'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WhenDecoyCountIsNegative_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = Claims,
            DecoyDigestCount = -1,
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );
    }

    [Fact]
    public async Task IssueAsync_WhenOverTheDisclosureLimit_Throws()
    {
        await using var host = new SdJwtTestHost(options => options.MaxDisclosureCount = 1);
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = host.IssuerCredentials,
            Claims = Claims,
            DisclosablePaths = ["/given_name", "/family_name"],
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IssueAsync_WithProfileAndAPathToARequiredClaimItDoesNotListAsProtected_Throws()
    {
        // The verifier judges a required claim on the issuer-signed payload before it resolves a disclosure, so an
        // SD-JWT that disclosed one would never verify under the same profile.
        var profile = new SdJwtProfile("test", "test+sd-jwt", ["sub"], []);
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse("""{"iss":"https://issuer.example","sub":"x"}"""),
            DisclosablePaths = ["/sub"],
            Profile = profile,
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("'sub'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'test' profile", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"a":{"b":{"c":1}}}""")]
    [InlineData("""{"a":[[1]]}""")]
    public async Task IssueAsync_WhenClaimsNestDeeperThanTheLimit_Throws(string claimsJson)
    {
        await using var host = new SdJwtTestHost(options => options.MaxNestingDepth = 1);
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = host.IssuerCredentials,
            Claims = JsonElement.Parse(claimsJson),
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains(
            "nest deeper than the limit of 1",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task IssueAsync_WhenClaimsNestToTheLimit_Issues()
    {
        // The limit counts the nesting of the resolved claims, so an SD-JWT the issuer makes within it verifies.
        await using var host = new SdJwtTestHost(options => options.MaxNestingDepth = 1);
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = host.IssuerCredentials,
            Claims = JsonElement.Parse("""{"a":{"b":1},"c":[1]}"""),
            DisclosablePaths = ["/a/b", "/c/0"],
        };

        var sdJwt = await host.Issuer.IssueAsync(options, CancellationToken.None);

        Assert.Equal(2, sdJwt.Disclosures.Length);
        Assert.Equal(2, host.Holder.ResolveDisclosures(sdJwt).Count);
    }

    [Fact]
    public async Task IssueAsync_WhenClaimsRepeatAMemberName_Throws()
    {
        var options = new SdJwtIssuanceOptions
        {
            SigningCredentials = Host.IssuerCredentials,
            Claims = JsonElement.Parse("""{"a":1,"a":2}"""),
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Host.Issuer.IssueAsync(options, CancellationToken.None)
        );

        Assert.Contains("repeat", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
