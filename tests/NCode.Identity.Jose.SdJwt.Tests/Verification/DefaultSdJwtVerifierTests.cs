// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Issuance;
using NCode.Identity.Jose.SdJwt.Presentation;
using NCode.Identity.Jose.SdJwt.Profiles;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.SdJwt.Tests.Verification;

public sealed class DefaultSdJwtVerifierTests : IAsyncLifetime
{
    private const string Audience = "https://verifier.example";
    private const string Nonce = "n-0S6_WzA2Mj";

    private const string ClaimsJson = """
        {
          "iss": "https://issuer.example",
          "given_name": "John",
          "family_name": "Doe",
          "address": { "street_address": "123 Main St", "country": "US" },
          "nationalities": ["US", "DE"]
        }
        """;

    private SdJwtTestHost Host { get; } = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await Host.DisposeAsync();

    #region Scaffolding

    private SelectiveDisclosureJwt Issue(
        IEnumerable<string> disclosablePaths,
        string claimsJson = ClaimsJson,
        int decoyDigestCount = 0,
        SdJwtProfile? profile = null,
        bool withHolderKey = true
    ) =>
        Host.Issuer.Issue(
            new SdJwtIssuanceOptions
            {
                SigningCredentials = Host.IssuerCredentials,
                Claims = JsonElement.Parse(claimsJson),
                DisclosablePaths = [.. disclosablePaths],
                DecoyDigestCount = decoyDigestCount,
                HolderKey = withHolderKey ? Host.HolderJwk : null,
                Profile = profile,
            }
        );

    private string Present(
        SelectiveDisclosureJwt sdJwt,
        IEnumerable<Disclosure>? disclosures = null,
        bool withKeyBinding = false,
        string nonce = Nonce
    ) =>
        Host.Holder.CreatePresentation(
            new SdJwtPresentationOptions
            {
                SdJwt = sdJwt,
                Disclosures = [.. disclosures ?? sdJwt.Disclosures],
                KeyBinding = withKeyBinding
                    ? new SdJwtKeyBindingOptions
                    {
                        SigningCredentials = Host.HolderCredentials,
                        Audience = Audience,
                        Nonce = nonce,
                    }
                    : null,
            }
        );

    private SdJwtVerificationParameters CreateParameters(
        bool requireKeyBinding = false,
        SdJwtProfile? profile = null,
        SecretKey? issuerKey = null
    ) =>
        new()
        {
            IssuerJwtParameters = Host.CreateIssuerJwtParameters(issuerKey),
            KeyBinding = requireKeyBinding ? CreateKeyBindingRequirement() : null,
            Profile = profile,
        };

    private static SdJwtKeyBindingRequirement CreateKeyBindingRequirement(
        string nonce = Nonce,
        string audience = Audience
    ) =>
        new()
        {
            Audience = audience,
            Nonce = nonce,
            ValidSignatureAlgorithms = [SdJwtTestHost.Es256],
            MaxAge = TimeSpan.FromMinutes(5),
        };

    private ValueTask<SdJwtVerificationResult> VerifyAsync(
        string presentation,
        SdJwtVerificationParameters? parameters = null
    ) =>
        Host.Verifier.VerifyAsync(
            presentation,
            parameters ?? CreateParameters(),
            CancellationToken.None
        );

    /// <summary>
    /// A presentation of a hand-built issuer-signed payload with the given disclosures, for shapes the issuer never
    /// produces.
    /// </summary>
    private string PresentCrafted(string payloadJson, params IEnumerable<string> disclosures) =>
        string.Concat(
            Host.SignIssuerJwt(payloadJson),
            "~",
            string.Concat(disclosures.Select(x => x + "~"))
        );

    private static string Digest(Disclosure disclosure) =>
        disclosure.ComputeDigest(SdJwtDigestAlgorithms.Sha256);

    private static Disclosure CreateDisclosure(string claimName, string valueJson) =>
        Disclosure.Create("c2FsdC1zYWx0LXNhbHQtc2FsdA", claimName, JsonElement.Parse(valueJson));

    private static Disclosure CreateArrayDisclosure(string valueJson) =>
        Disclosure.Create("c2FsdC1zYWx0LXNhbHQtc2FsdA", JsonElement.Parse(valueJson));

    private static void AssertFailure(SdJwtVerificationResult result, string failureCode)
    {
        Assert.False(result.IsValid);
        Assert.Equal(failureCode, result.FailureCode);
        Assert.NotNull(result.FailureReason);
        Assert.Null(result.SdJwt);
        Assert.Null(result.IssuerJwt);
        Assert.Empty(result.Disclosures);
        Assert.Null(result.KeyBinding);
    }

    #endregion

    #region VerifyAsync Tests

    [Fact]
    public async Task VerifyAsync_WhenEveryDisclosureIsPresented_ResolvesEveryClaimInPlace()
    {
        var issued = Issue([
            "/given_name",
            "/address",
            "/address/street_address",
            "/nationalities/1",
        ]);
        var presentation = Present(issued);

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        var claims = result.Claims;
        Assert.Equal("https://issuer.example", claims.GetProperty("iss").GetString());
        Assert.Equal("John", claims.GetProperty("given_name").GetString());
        Assert.Equal("Doe", claims.GetProperty("family_name").GetString());
        Assert.Equal(
            "123 Main St",
            claims.GetProperty("address").GetProperty("street_address").GetString()
        );
        Assert.Equal("US", claims.GetProperty("address").GetProperty("country").GetString());
        Assert.Equal(
            ["US", "DE"],
            claims.GetProperty("nationalities").EnumerateArray().Select(x => x.GetString())
        );
        Assert.False(claims.TryGetProperty("_sd", out _));
        Assert.False(claims.TryGetProperty("_sd_alg", out _));
        Assert.False(claims.GetProperty("address").TryGetProperty("_sd", out _));
        Assert.True(claims.TryGetProperty("cnf", out _));

        Assert.Equal(4, result.Disclosures.Length);
        var byPath = result.Disclosures.ToDictionary(x => x.Path, StringComparer.Ordinal);
        Assert.Same(byPath["/address"].Disclosure, byPath["/address/street_address"].Parent);
        Assert.Null(byPath["/nationalities/1"].Parent);
        Assert.Same(Host.IssuerKey, result.IssuerJwt.SigningKey);
        Assert.Null(result.KeyBinding);
        Assert.Equal(presentation, result.SdJwt.ToString());
    }

    [Fact]
    public async Task VerifyAsync_WhenASubsetIsPresented_OmitsTheWithheldClaims()
    {
        var issued = Issue(["/given_name", "/family_name", "/nationalities/0"]);
        var givenName = Assert.Single(issued.Disclosures, x => x.ClaimName == "given_name");
        var presentation = Present(issued, [givenName]);

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        Assert.Equal("John", result.Claims.GetProperty("given_name").GetString());
        Assert.False(result.Claims.TryGetProperty("family_name", out _));
        Assert.Equal(
            ["DE"],
            result.Claims.GetProperty("nationalities").EnumerateArray().Select(x => x.GetString())
        );
        Assert.Single(result.Disclosures);
    }

    [Fact]
    public async Task VerifyAsync_WithDecoys_DropsTheDecoyDigests()
    {
        var issued = Issue(["/given_name", "/nationalities/1"], decoyDigestCount: 3);
        var presentation = Present(issued);

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        Assert.Equal("John", result.Claims.GetProperty("given_name").GetString());
        Assert.Equal(
            ["US", "DE"],
            result.Claims.GetProperty("nationalities").EnumerateArray().Select(x => x.GetString())
        );
        Assert.Equal(2, result.Disclosures.Length);
    }

    [Fact]
    public async Task VerifyAsync_WithSpecVectors_ResolvesTheSpecDisclosures()
    {
        var payload = $$"""
            {
              "iss": "https://issuer.example",
              "_sd": ["{{SpecVectors.EmailDigest}}", "{{SpecVectors.FamilyNameDigest}}", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"],
              "nationalities": [{"...": "{{SpecVectors.NationalityDigest}}"}],
              "_sd_alg": "sha-256"
            }
            """;
        var presentation = PresentCrafted(
            payload,
            SpecVectors.FamilyNameDisclosure,
            SpecVectors.NationalityDisclosure,
            SpecVectors.EmailDisclosure
        );

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        Assert.Equal("M\u00f6bius", result.Claims.GetProperty("family_name").GetString());
        Assert.Equal("johndoe@example.com", result.Claims.GetProperty("email").GetString());
        Assert.Equal(
            ["FR"],
            result.Claims.GetProperty("nationalities").EnumerateArray().Select(x => x.GetString())
        );
        Assert.Equal(3, result.Disclosures.Length);
    }

    [Fact]
    public async Task VerifyAsync_WithoutSdAlg_DefaultsToSha256()
    {
        var disclosure = CreateDisclosure("given_name", "\"John\"");
        var presentation = PresentCrafted(
            $$"""{"_sd":["{{Digest(disclosure)}}"]}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        Assert.Equal("John", result.Claims.GetProperty("given_name").GetString());
    }

    [Fact]
    public async Task VerifyAsync_WhenSignatureAlgorithmsAreUnset_Throws()
    {
        var presentation = Present(Issue(["/given_name"]));
        var parameters = new SdJwtVerificationParameters
        {
            IssuerJwtParameters = new JwtValidationParameters { ValidationKeys = [Host.IssuerKey] },
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await VerifyAsync(presentation, parameters)
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenTheIssuerKeyDoesNotVerify_FailsWithTheJwtCode()
    {
        var presentation = Present(Issue(["/given_name"]));

        var result = await VerifyAsync(presentation, CreateParameters(issuerKey: Host.StrangerKey));

        AssertFailure(result, JwtValidationFailureCodes.InvalidSignature);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheIssuerSignedWithASymmetricKey_FailsEvenWhenPermitted()
    {
        var secretKeyFactory = Host.ServiceProvider.GetRequiredService<ISecretKeyFactory>();
        var symmetricKey = secretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "hmac" },
            new byte[32]
        );
        var jwt = Host.SignJwt(
            Host.CreateCredentials(symmetricKey, "HS256"),
            """{"iss":"x"}""",
            "JWT"
        );
        var parameters = new SdJwtVerificationParameters
        {
            IssuerJwtParameters = new JwtValidationParameters
            {
                ValidationKeys = [symmetricKey],
                ValidSignatureAlgorithms = ["HS256"],
            },
        };

        var result = await VerifyAsync($"{jwt}~", parameters);

        AssertFailure(result, JwtValidationFailureCodes.AlgorithmNotPermitted);
    }

    [Theory]
    [InlineData("eyJhbGciOiJFUzI1NiJ9.e30.c2ln")]
    [InlineData("")]
    public async Task VerifyAsync_WhenNotInTheCombinedFormat_Fails(string presentation)
    {
        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidSdJwt);
    }

    [Fact]
    public async Task VerifyAsync_WhenADisclosureIsMalformed_Fails()
    {
        var presentation = Present(Issue(["/given_name"])) + "!!!~";

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidDisclosure);
    }

    [Fact]
    public async Task VerifyAsync_WhenOverTheDisclosureLimit_Fails()
    {
        await using var host = new SdJwtTestHost(options => options.MaxDisclosureCount = 1);
        var presentation = Present(Issue(["/given_name", "/family_name"]));

        var result = await host.Verifier.VerifyAsync(
            presentation,
            new SdJwtVerificationParameters
            {
                IssuerJwtParameters = Host.CreateIssuerJwtParameters(),
            },
            CancellationToken.None
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.DisclosureLimitExceeded);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheClaimsNestTooDeep_Fails()
    {
        await using var host = new SdJwtTestHost(options => options.MaxNestingDepth = 1);
        var presentation = Present(Issue(["/address/street_address"]));

        var result = await host.Verifier.VerifyAsync(
            presentation,
            new SdJwtVerificationParameters
            {
                IssuerJwtParameters = Host.CreateIssuerJwtParameters(),
            },
            CancellationToken.None
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.NestingLimitExceeded);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheDigestAlgorithmIsUnregistered_Fails()
    {
        var presentation = PresentCrafted("""{"_sd_alg":"sha-1","_sd":[]}""");

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm);
    }

    [Fact]
    public async Task VerifyAsync_WhenADisclosureIsUnreferenced_Fails()
    {
        var issued = Issue(["/given_name"]);
        var other = Issue(["/family_name"]);
        var presentation = Present(issued) + other.Disclosures[0].Encoded + "~";

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.UnreferencedDisclosure);
    }

    [Fact]
    public async Task VerifyAsync_WhenADisclosureIsPresentedTwice_Fails()
    {
        var issued = Issue(["/given_name"]);
        var presentation = Present(issued) + issued.Disclosures[0].Encoded + "~";

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DuplicateDisclosure);
    }

    [Fact]
    public async Task VerifyAsync_WhenADigestRepeats_Fails()
    {
        var disclosure = CreateDisclosure("given_name", "\"John\"");
        var digest = Digest(disclosure);
        var presentation = PresentCrafted(
            $$$"""{"_sd":["{{{digest}}}"],"address":{"_sd":["{{{digest}}}"]}}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DuplicateDigest);
    }

    [Fact]
    public async Task VerifyAsync_WhenADecoyDigestRepeats_Fails()
    {
        const string decoy = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var presentation = PresentCrafted($$"""{"_sd":["{{decoy}}","{{decoy}}"]}""");

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DuplicateDigest);
    }

    [Fact]
    public async Task VerifyAsync_WhenADisclosedClaimIsAlreadyPresent_Fails()
    {
        var disclosure = CreateDisclosure("given_name", "\"John\"");
        var presentation = PresentCrafted(
            $$"""{"given_name":"Jane","_sd":["{{Digest(disclosure)}}"]}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DuplicateClaim);
    }

    [Fact]
    public async Task VerifyAsync_WhenAnObjectDigestReferencesAnArrayDisclosure_Fails()
    {
        var disclosure = CreateArrayDisclosure("\"DE\"");
        var presentation = PresentCrafted(
            $$"""{"_sd":["{{Digest(disclosure)}}"]}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DisclosureMismatch);
    }

    [Fact]
    public async Task VerifyAsync_WhenAnArrayDigestReferencesAnObjectDisclosure_Fails()
    {
        var disclosure = CreateDisclosure("given_name", "\"John\"");
        var presentation = PresentCrafted(
            $$"""{"nationalities":[{"...":"{{Digest(disclosure)}}"}]}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DisclosureMismatch);
    }

    [Theory]
    [InlineData("""{"_sd":"x"}""")]
    [InlineData("""{"_sd":[1]}""")]
    [InlineData("""{"nationalities":[{"...":"x","extra":1}]}""")]
    [InlineData("""{"nationalities":[{"...":1}]}""")]
    public async Task VerifyAsync_WhenADigestStructureIsMalformed_Fails(string payloadJson)
    {
        var presentation = PresentCrafted(payloadJson);

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidDigestStructure);
    }

    [Theory]
    [InlineData("""{"address":{"...":"x"}}""")]
    [InlineData("""{"address":{"_sd_alg":"sha-256"}}""")]
    public async Task VerifyAsync_WhenAReservedNameIsMisplaced_Fails(string payloadJson)
    {
        var presentation = PresentCrafted(payloadJson);

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.ReservedClaimName);
    }

    [Fact]
    public async Task VerifyAsync_WhenAPlaceholderIsNotAnArrayElement_Fails()
    {
        var disclosure = CreateArrayDisclosure("\"DE\"");
        var presentation = PresentCrafted(
            $$"""{"...":"{{Digest(disclosure)}}"}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.ReservedClaimName);
    }

    [Fact]
    public async Task VerifyAsync_WhenThePayloadRepeatsAMemberName_Fails()
    {
        var presentation = PresentCrafted("""{"a":1,"a":2}""");

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidSdJwt);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheDisclosedValueCarriesDisclosures_ResolvesRecursively()
    {
        var street = CreateDisclosure("street_address", "\"123 Main St\"");
        var address = CreateDisclosure(
            "address",
            $$"""{"_sd":["{{Digest(street)}}"],"country":"US"}"""
        );
        var presentation = PresentCrafted(
            $$"""{"_sd":["{{Digest(address)}}"]}""",
            street.Encoded,
            address.Encoded
        );

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        Assert.Equal(
            "123 Main St",
            result.Claims.GetProperty("address").GetProperty("street_address").GetString()
        );
        var byPath = result.Disclosures.ToDictionary(x => x.Path, StringComparer.Ordinal);
        Assert.Equal("address", byPath["/address/street_address"].Parent?.ClaimName);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheIssuerSignedJwtIsEncrypted_Fails()
    {
        var presentation = "eyJhbGciOiJSU0EtT0FFUCJ9.a.b.c.d~";

        var result = await VerifyAsync(presentation);

        Assert.False(result.IsValid);
        Assert.Null(result.SdJwt);
    }

    #endregion

    #region VerifyAsync Key Binding Tests

    [Fact]
    public async Task VerifyAsync_WithKeyBinding_ReturnsTheVerifiedKeyBinding()
    {
        var issued = Issue(["/given_name"]);
        var presentation = Present(issued, withKeyBinding: true);

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        Assert.True(result.IsValid);
        Assert.NotNull(result.KeyBinding);
        Assert.Equal(Nonce, result.KeyBinding.Nonce);
        Assert.Equal(SdJwtTestHost.StartTime, result.KeyBinding.IssuedAt);
        Assert.Equal(SdJwtTestHost.HolderKeyId, result.KeyBinding.HolderKey.KeyId);
        Assert.Equal("kb+jwt", result.KeyBinding.Jwt.Header.GetProperty("typ").GetString());
    }

    [Fact]
    public async Task VerifyAsync_WhenKeyBindingIsNotRequired_IgnoresTheKeyBindingJwt()
    {
        var presentation = Present(Issue(["/given_name"]), withKeyBinding: true, nonce: "other");

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        Assert.Null(result.KeyBinding);
        Assert.True(result.SdJwt.HasKeyBinding);
    }

    [Fact]
    public async Task VerifyAsync_WhenKeyBindingIsRequiredButAbsent_Fails()
    {
        var presentation = Present(Issue(["/given_name"]));

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.KeyBindingRequired);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheNonceDiffers_Fails()
    {
        var presentation = Present(Issue(["/given_name"]), withKeyBinding: true, nonce: "other");

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidNonce);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheAudienceDiffers_Fails()
    {
        var presentation = Present(Issue(["/given_name"]), withKeyBinding: true);
        var parameters = new SdJwtVerificationParameters
        {
            IssuerJwtParameters = Host.CreateIssuerJwtParameters(),
            KeyBinding = CreateKeyBindingRequirement(audience: "https://other.example"),
        };

        var result = await VerifyAsync(presentation, parameters);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidKeyBinding);
        Assert.Contains(
            JwtValidationFailureCodes.InvalidAudience,
            result.FailureReason,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtCoversAnotherPresentation_Fails()
    {
        var issued = Issue(["/given_name", "/family_name"]);
        var bound = Present(issued, [issued.Disclosures[0]], withKeyBinding: true);
        var keyBindingJwt = bound[(bound.LastIndexOf('~') + 1)..];
        var presentation = Present(issued) + keyBindingJwt;

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidSdHash);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsTooOld_Fails()
    {
        var presentation = Present(Issue(["/given_name"]), withKeyBinding: true);
        Host.TimeProvider.Advance(TimeSpan.FromMinutes(11));

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.KeyBindingNotFresh);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsWithinTheSkew_Succeeds()
    {
        var presentation = Present(Issue(["/given_name"]), withKeyBinding: true);
        Host.TimeProvider.Advance(TimeSpan.FromMinutes(9));

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsFromTheFuture_Fails()
    {
        var issued = Issue(["/given_name"]);
        var prefix = Present(issued);
        var future = SdJwtTestHost.StartTime + TimeSpan.FromMinutes(6);
        var payload = $$"""
            {"iat":{{future.ToUnixTimeSeconds()}},"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(
                prefix
            )}}"}
            """;
        var keyBindingJwt = Host.SignJwt(Host.HolderCredentials, payload, "kb+jwt");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.KeyBindingNotFresh);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsSignedByAnotherKey_Fails()
    {
        var issued = Issue(["/given_name"]);
        var presentation = Host.Holder.CreatePresentation(
            new SdJwtPresentationOptions
            {
                SdJwt = issued,
                KeyBinding = new SdJwtKeyBindingOptions
                {
                    SigningCredentials = Host.StrangerCredentials,
                    Audience = Audience,
                    Nonce = Nonce,
                },
            }
        );

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidKeyBinding);
        Assert.Contains(
            JwtValidationFailureCodes.InvalidSignature,
            result.FailureReason,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsNotTypedKbJwt_Fails()
    {
        var issued = Issue(["/given_name"]);
        var prefix = Present(issued);
        var payload = $$"""
            {"iat":{{SdJwtTestHost.StartTime.ToUnixTimeSeconds()}},"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(
                prefix
            )}}"}
            """;
        var keyBindingJwt = Host.SignJwt(Host.HolderCredentials, payload, "JWT");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidKeyBinding);
        Assert.Contains(
            JwtValidationFailureCodes.InvalidTokenType,
            result.FailureReason,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtLacksIat_Fails()
    {
        var issued = Issue(["/given_name"]);
        var prefix = Present(issued);
        var payload =
            $$"""{"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(prefix)}}"}""";
        var keyBindingJwt = Host.SignJwt(Host.HolderCredentials, payload, "kb+jwt");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.KeyBindingNotFresh);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheIssuerSignedJwtCarriesNoCnf_Fails()
    {
        var presentation = Present(
            Issue(["/given_name"], withHolderKey: false),
            withKeyBinding: true
        );

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.HolderKeyNotFound);
    }

    [Fact]
    public async Task VerifyAsync_WhenCnfIsDisclosedRatherThanPlaintext_Fails()
    {
        // The holder key is read from the issuer-signed payload alone, so a 'cnf' in a disclosure binds nothing.
        var cnf = CreateDisclosure("cnf", $$$"""{"jwk":{{{Host.HolderJwk.ToJsonString()}}}}""");
        var prefix = PresentCrafted($$"""{"_sd":["{{Digest(cnf)}}"]}""", cnf.Encoded);
        var payload = $$"""
            {"iat":{{SdJwtTestHost.StartTime.ToUnixTimeSeconds()}},"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(
                prefix
            )}}"}
            """;
        var keyBindingJwt = Host.SignJwt(Host.HolderCredentials, payload, "kb+jwt");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.HolderKeyNotFound);
    }

    #endregion

    #region VerifyAsync Profile Tests

    private const string VcClaimsJson = """
        {
          "iss": "https://issuer.example",
          "vct": "https://credentials.example/identity_credential",
          "status": { "status_list": { "idx": 0, "uri": "https://issuer.example/status/1" } },
          "sub": "6c5c0a49-b589-431d-bae7-219122a9ec2c",
          "given_name": "John"
        }
        """;

    [Fact]
    public async Task VerifyAsync_WithSdJwtVcProfile_PassesTheStatusClaimThrough()
    {
        var issued = Issue(["/sub", "/given_name"], VcClaimsJson, profile: SdJwtProfiles.SdJwtVc);
        var presentation = Present(issued, withKeyBinding: true);

        var result = await VerifyAsync(
            presentation,
            CreateParameters(requireKeyBinding: true, profile: SdJwtProfiles.SdJwtVc)
        );

        Assert.True(result.IsValid);
        Assert.Equal(
            "https://credentials.example/identity_credential",
            result.Claims.GetProperty("vct").GetString()
        );
        Assert.Equal(
            "https://issuer.example/status/1",
            result
                .Claims.GetProperty("status")
                .GetProperty("status_list")
                .GetProperty("uri")
                .GetString()
        );
        Assert.Equal("dc+sd-jwt", result.IssuerJwt.Header.GetProperty("typ").GetString());
    }

    [Fact]
    public async Task VerifyAsync_WithProfileAndAPlainJwtType_Fails()
    {
        var presentation = Present(Issue(["/given_name"], VcClaimsJson));

        var result = await VerifyAsync(
            presentation,
            CreateParameters(profile: SdJwtProfiles.SdJwtVc)
        );

        AssertFailure(result, JwtValidationFailureCodes.InvalidTokenType);
    }

    [Fact]
    public async Task VerifyAsync_WithProfileAndNoVct_Fails()
    {
        var jwt = Host.SignIssuerJwt("""{"iss":"https://issuer.example"}""", "dc+sd-jwt");

        var result = await VerifyAsync($"{jwt}~", CreateParameters(profile: SdJwtProfiles.SdJwtVc));

        AssertFailure(result, SdJwtVerificationFailureCodes.MissingRequiredClaim);
    }

    [Fact]
    public async Task VerifyAsync_WithProfileAndAProtectedClaimDisclosed_Fails()
    {
        var iss = CreateDisclosure("iss", "\"https://issuer.example\"");
        var jwt = Host.SignIssuerJwt($$"""{"vct":"t","_sd":["{{Digest(iss)}}"]}""", "dc+sd-jwt");

        var result = await VerifyAsync(
            $"{jwt}~{iss.Encoded}~",
            CreateParameters(profile: SdJwtProfiles.SdJwtVc)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.ProtectedClaimDisclosed);
    }

    [Fact]
    public async Task VerifyAsync_WithProfileAndAProtectedNameNested_Succeeds()
    {
        // The profile protects top-level claims; a nested member of the same name is an ordinary claim.
        var exp = CreateDisclosure("exp", "1");
        var jwt = Host.SignIssuerJwt(
            $$$"""{"vct":"t","inner":{"_sd":["{{{Digest(exp)}}}"]}}""",
            "dc+sd-jwt"
        );

        var result = await VerifyAsync(
            $"{jwt}~{exp.Encoded}~",
            CreateParameters(profile: SdJwtProfiles.SdJwtVc)
        );

        Assert.True(result.IsValid);
        Assert.Equal(1, result.Claims.GetProperty("inner").GetProperty("exp").GetInt32());
    }

    #endregion
}
