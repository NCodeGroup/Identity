// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Issuance;

namespace NCode.Identity.Jose.SdJwt.Tests.Issuance;

/// <summary>
/// Drives the issuance walk with a scripted random source, so the salt of every disclosure and decoy and the position
/// of every decoy are observed rather than counted.
/// </summary>
public class IssuanceWalkTests : BaseTests
{
    private static string[] Salts { get; } = ["c2FsdC0w", "c2FsdC0x", "c2FsdC0y", "c2FsdC0z"];

    private static SdJwtDigestAlgorithm Algorithm => SdJwtDigestAlgorithms.Sha256;

    private Mock<ISdJwtRandomSource> CreateRandomSourceMock(int saltCount)
    {
        var mock = CreateStrictMock<ISdJwtRandomSource>();
        var salts = 0;
        mock.Setup(x => x.NextSalt())
            .Returns(() => Salts[salts++])
            .Verifiable(Times.Exactly(saltCount));
        return mock;
    }

    private static JsonObject ParseClaims(string json) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(json));

    private static string ReadPlaceholderDigest(JsonNode? element) =>
        Assert.IsType<JsonObject>(element)["..."]?.GetValue<string>() ?? string.Empty;

    #region ProcessObject Tests

    [Fact]
    public void ProcessObject_WithDecoys_WritesEachDecoyAsTheDigestOfAFreshSalt()
    {
        var randomSource = CreateRandomSourceMock(saltCount: 3);
        var claims = ParseClaims("""{"given_name":"John","iss":"x"}""");
        var walk = new IssuanceWalk(
            Algorithm,
            randomSource.Object,
            ["/given_name"],
            decoyDigestCount: 2
        );

        walk.ProcessObject(claims, "");

        var disclosure = Assert.Single(walk.Disclosures);
        Assert.Equal(Salts[0], disclosure.Salt);
        var sd = Assert.IsType<JsonArray>(claims["_sd"]);
        string[] digests = [.. sd.Select(x => x?.GetValue<string>() ?? string.Empty)];
        Assert.Equal(digests.Order(StringComparer.Ordinal), digests);
        Assert.Equal(
            new[]
            {
                disclosure.ComputeDigest(Algorithm),
                Algorithm.ComputeDigest(Salts[1]),
                Algorithm.ComputeDigest(Salts[2]),
            }.Order(StringComparer.Ordinal),
            digests
        );
        Assert.True(walk.WroteDigests);
    }

    [Fact]
    public void ProcessObject_WithDisclosableArrayElementAndDecoys_InsertsEachDecoyAtTheScriptedIndex()
    {
        var randomSource = CreateRandomSourceMock(saltCount: 3);
        // The first decoy goes to the front of the three positions, the second to the end of the four.
        randomSource.Setup(x => x.NextIndex(3)).Returns(0).Verifiable();
        randomSource.Setup(x => x.NextIndex(4)).Returns(3).Verifiable();
        var claims = ParseClaims("""{"nationalities":["US","DE"]}""");
        var walk = new IssuanceWalk(
            Algorithm,
            randomSource.Object,
            ["/nationalities/1"],
            decoyDigestCount: 2
        );

        walk.ProcessObject(claims, "");

        var disclosure = Assert.Single(walk.Disclosures);
        Assert.True(disclosure.IsArrayElement);
        Assert.Equal(Salts[0], disclosure.Salt);
        Assert.Equal("DE", disclosure.Value.GetString());
        var nationalities = Assert.IsType<JsonArray>(claims["nationalities"]);
        Assert.Equal(4, nationalities.Count);
        Assert.Equal(Algorithm.ComputeDigest(Salts[1]), ReadPlaceholderDigest(nationalities[0]));
        Assert.Equal("US", nationalities[1]?.GetValue<string>());
        Assert.Equal(disclosure.ComputeDigest(Algorithm), ReadPlaceholderDigest(nationalities[2]));
        Assert.Equal(Algorithm.ComputeDigest(Salts[2]), ReadPlaceholderDigest(nationalities[3]));
        Assert.False(claims.ContainsKey("_sd"));
        Assert.True(walk.WroteDigests);
    }

    [Fact]
    public void ProcessObject_WithNestedDisclosablePaths_DigestsTheInnerClaimInsideTheOuterDisclosure()
    {
        var randomSource = CreateRandomSourceMock(saltCount: 2);
        var claims = ParseClaims("""{"address":{"street":"Main","country":"US"}}""");
        var walk = new IssuanceWalk(
            Algorithm,
            randomSource.Object,
            ["/address", "/address/street"],
            decoyDigestCount: 0
        );

        walk.ProcessObject(claims, "");

        Assert.Equal(2, walk.Disclosures.Count);
        var street = walk.Disclosures[0];
        var address = walk.Disclosures[1];
        Assert.Equal("street", street.ClaimName);
        Assert.Equal(Salts[0], street.Salt);
        Assert.Equal("address", address.ClaimName);
        Assert.Equal(Salts[1], address.Salt);
        Assert.Equal(
            street.ComputeDigest(Algorithm),
            address.Value.GetProperty("_sd")[0].GetString()
        );
        Assert.Equal("US", address.Value.GetProperty("country").GetString());
        Assert.False(address.Value.TryGetProperty("street", out _));
        Assert.False(claims.ContainsKey("address"));
    }

    [Fact]
    public void ProcessObject_WithoutDisclosablePaths_WritesNothing()
    {
        var randomSource = CreateRandomSourceMock(saltCount: 0);
        var claims = ParseClaims("""{"given_name":"John","nationalities":["US"]}""");
        var walk = new IssuanceWalk(Algorithm, randomSource.Object, [], decoyDigestCount: 3);

        walk.ProcessObject(claims, "");

        Assert.Empty(walk.Disclosures);
        Assert.False(walk.WroteDigests);
        Assert.False(claims.ContainsKey("_sd"));
        Assert.Single(Assert.IsType<JsonArray>(claims["nationalities"]));
    }

    #endregion
}
