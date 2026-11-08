// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Issuance;
using NCode.Identity.Jose.SdJwt.Options;
using NCode.Identity.Jose.SdJwt.Presentation;
using NCode.Identity.Jose.SdJwt.Profiles;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.SdJwt.Tests.Verification;

public sealed class DefaultSdJwtVerifierTests : BaseTests
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

    private const string VcClaimsJson = """
        {
          "iss": "https://issuer.example",
          "vct": "https://credentials.example/identity_credential",
          "status": { "status_list": { "idx": 0, "uri": "https://issuer.example/status/1" } },
          "sub": "6c5c0a49-b589-431d-bae7-219122a9ec2c",
          "given_name": "John"
        }
        """;

    private SdJwtTestHost Host { get; } = new();

    protected override async ValueTask DisposeAsyncCore()
    {
        await Host.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    private ValueTask<SelectiveDisclosureJwt> IssueAsync(
        IEnumerable<string> disclosablePaths,
        string claimsJson = ClaimsJson,
        int decoyDigestCount = 0,
        SdJwtProfile? profile = null,
        bool withHolderKey = true
    ) =>
        Host.Issuer.IssueAsync(
            new SdJwtIssuanceOptions
            {
                SigningCredentials = Host.IssuerCredentials,
                Claims = JsonElement.Parse(claimsJson),
                DisclosablePaths = [.. disclosablePaths],
                DecoyDigestCount = decoyDigestCount,
                HolderKey = withHolderKey ? Host.HolderJwk : null,
                Profile = profile,
            },
            CancellationToken.None
        );

    private ValueTask<string> PresentAsync(
        SelectiveDisclosureJwt sdJwt,
        IEnumerable<Disclosure>? disclosures = null,
        bool withKeyBinding = false,
        string nonce = Nonce
    ) =>
        Host.Holder.CreatePresentationAsync(
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
            },
            CancellationToken.None
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
    private async ValueTask<string> PresentCraftedAsync(
        string payloadJson,
        params IEnumerable<string> disclosures
    ) =>
        string.Concat(
            await Host.SignIssuerJwtAsync(payloadJson),
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

    private Mock<IConfirmationKeyResolver> CreateResolverMock(string member)
    {
        var mock = CreateStrictMock<IConfirmationKeyResolver>();
        mock.Setup(x => x.Member).Returns(member).Verifiable();
        return mock;
    }

    private DefaultSdJwtVerifier CreateVerifier(
        params IEnumerable<IConfirmationKeyResolver> resolvers
    ) => CreateVerifier(Host.ServiceProvider.GetRequiredService<IJwtValidator>(), resolvers);

    private DefaultSdJwtVerifier CreateVerifier(
        IJwtValidator jwtValidator,
        IEnumerable<IConfirmationKeyResolver> resolvers
    ) =>
        new(
            jwtValidator,
            Host.ServiceProvider.GetRequiredService<ISdJwtDigestAlgorithmCatalog>(),
            resolvers,
            Host.TimeProvider,
            Host.ServiceProvider.GetRequiredService<IOptions<SdJwtOptions>>()
        );

    /// <summary>
    /// A verifier over a JWT validator that answers as scripted, for results the real validator never produces.
    /// </summary>
    private DefaultSdJwtVerifier CreateVerifierOver(IJwtValidator jwtValidator) =>
        CreateVerifier(jwtValidator, Host.ServiceProvider.GetServices<IConfirmationKeyResolver>());

    private static JwtValidationResult Succeed(
        JwtValidationParameters parameters,
        DecodedJwt decodedJwt
    ) =>
        JwtValidationResult.Success(
            parameters,
            parameters.PropertyBag,
            decodedJwt,
            static (_, _, _, _, _, _) => throw new NotSupportedException()
        );

    #region Constructor Tests

    [Fact]
    public void Constructor_WithOneResolverPerMember_Succeeds()
    {
        var jwk = CreateResolverMock("jwk");
        var kid = CreateResolverMock("kid");

        var verifier = CreateVerifier(jwk.Object, kid.Object);

        Assert.NotNull(verifier);
    }

    [Fact]
    public void Constructor_WhenTwoResolversShareAMember_Throws()
    {
        var first = CreateResolverMock("jwk");
        var second = CreateResolverMock("jwk");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateVerifier(first.Object, second.Object)
        );

        Assert.Contains("'jwk'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenAResolverNamesAnEmptyMember_Throws()
    {
        var resolver = CreateResolverMock(" ");

        Assert.Throws<InvalidOperationException>(() => CreateVerifier(resolver.Object));
    }

    [Fact]
    public void Constructor_WhenAResolverIsNull_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateVerifier([null!]));

        Assert.Contains("is null", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region VerifyAsync Tests

    [Fact]
    public async Task VerifyAsync_WhenEveryDisclosureIsPresented_ResolvesEveryClaimInPlace()
    {
        var issued = await IssueAsync([
            "/given_name",
            "/address",
            "/address/street_address",
            "/nationalities/1",
        ]);
        var presentation = await PresentAsync(issued);

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
        var issued = await IssueAsync(["/given_name", "/family_name", "/nationalities/0"]);
        var givenName = Assert.Single(issued.Disclosures, x => x.ClaimName == "given_name");
        var presentation = await PresentAsync(issued, [givenName]);

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
        var issued = await IssueAsync(["/given_name", "/nationalities/1"], decoyDigestCount: 3);
        var presentation = await PresentAsync(issued);

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
        var presentation = await PresentCraftedAsync(
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
        var presentation = await PresentCraftedAsync(
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
        var presentation = await PresentAsync(await IssueAsync(["/given_name"]));
        var parameters = new SdJwtVerificationParameters
        {
            IssuerJwtParameters = new JwtValidationParameters { ValidationKeys = [Host.IssuerKey] },
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await VerifyAsync(presentation, parameters)
        );
    }

    [Theory]
    [InlineData("", Nonce, true, 5, 0, "audience")]
    [InlineData(Audience, "", true, 5, 0, "nonce")]
    [InlineData(Audience, Nonce, false, 5, 0, "signature algorithms")]
    [InlineData(Audience, Nonce, true, 0, 0, "maximum age")]
    [InlineData(Audience, Nonce, true, -5, 0, "maximum age")]
    [InlineData(Audience, Nonce, true, 5, -1, "clock skew")]
    public async Task VerifyAsync_WhenTheKeyBindingRequirementIsUnusable_Throws(
        string audience,
        string nonce,
        bool withAlgorithms,
        int maxAgeMinutes,
        int clockSkewMinutes,
        string reason
    )
    {
        // A requirement built from a missing value would let a captured Key Binding JWT replay, so it is refused
        // before the presentation is read, which a valid presentation proves.
        var presentation = await PresentAsync(
            await IssueAsync(["/given_name"]),
            withKeyBinding: true
        );
        var parameters = new SdJwtVerificationParameters
        {
            IssuerJwtParameters = Host.CreateIssuerJwtParameters(),
            KeyBinding = new SdJwtKeyBindingRequirement
            {
                Audience = audience,
                Nonce = nonce,
                ValidSignatureAlgorithms = withAlgorithms ? [SdJwtTestHost.Es256] : null!,
                MaxAge = TimeSpan.FromMinutes(maxAgeMinutes),
                ClockSkew = TimeSpan.FromMinutes(clockSkewMinutes),
            },
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await VerifyAsync(presentation, parameters)
        );

        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
        Assert.Equal("parameters", exception.ParamName);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheMaximumAgeIsUnbounded_VerifiesAnOldKeyBindingJwt()
    {
        var presentation = await PresentAsync(
            await IssueAsync(["/given_name"]),
            withKeyBinding: true
        );
        Host.TimeProvider.Advance(TimeSpan.FromDays(365));
        var parameters = new SdJwtVerificationParameters
        {
            IssuerJwtParameters = Host.CreateIssuerJwtParameters(),
            KeyBinding = new SdJwtKeyBindingRequirement
            {
                Audience = Audience,
                Nonce = Nonce,
                ValidSignatureAlgorithms = [SdJwtTestHost.Es256],
                MaxAge = TimeSpan.MaxValue,
            },
        };

        var result = await VerifyAsync(presentation, parameters);

        Assert.True(result.IsValid);
        Assert.NotNull(result.KeyBinding);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheIssuerSignedJwtDoesNotDecode_ReportsFixedTextWithoutTheToken()
    {
        var sdJwt = await IssueAsync(["/given_name"]);
        var presentation = await PresentAsync(sdJwt);

        var result = await VerifyAsync(presentation, CreateParameters(issuerKey: Host.StrangerKey));

        AssertFailure(result, JwtValidationFailureCodes.InvalidSignature);
        Assert.NotNull(result.FailureReason);
        Assert.Contains(
            JwtValidationFailureCodes.InvalidSignature,
            result.FailureReason,
            StringComparison.Ordinal
        );
        Assert.Contains("could not be decoded", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain(
            sdJwt.IssuerSignedJwt,
            result.FailureReason,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenTheValidatorReportsCancellation_Throws()
    {
        // The JWT validator reports every exception as a failed result, cancellation included, which the verifier
        // propagates rather than reports as an invalid token.
        var presentation = await PresentAsync(await IssueAsync(["/given_name"]));
        var parameters = CreateParameters();
        var validator = CreateStrictMock<IJwtValidator>();
        validator
            .Setup(x =>
                x.ValidateAsync(
                    It.IsAny<string>(),
                    parameters.IssuerJwtParameters,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, JwtValidationParameters, CancellationToken>(
                (token, jwtParameters, _) =>
                    ValueTask.FromResult(
                        JwtValidationResult.Fail(
                            jwtParameters,
                            jwtParameters.PropertyBag,
                            token,
                            new OperationCanceledException()
                        )
                    )
            )
            .Verifiable();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CreateVerifierOver(validator.Object)
                .VerifyAsync(presentation, parameters, CancellationToken.None)
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenTheIssuerSignedJwtPayloadIsNotAnObject_Fails()
    {
        var sdJwt = await IssueAsync(["/given_name"]);
        var presentation = await PresentAsync(sdJwt);
        var parameters = CreateParameters();
        var validator = CreateStrictMock<IJwtValidator>();
        validator
            .Setup(x =>
                x.ValidateAsync(
                    sdJwt.IssuerSignedJwt,
                    parameters.IssuerJwtParameters,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                Succeed(
                    parameters.IssuerJwtParameters,
                    new DecodedJwt(
                        Host.JoseSerializer.Parse(sdJwt.IssuerSignedJwt),
                        JsonElement.Parse("[1]"),
                        Host.IssuerKey
                    )
                )
            )
            .Verifiable();

        var result = await CreateVerifierOver(validator.Object)
            .VerifyAsync(presentation, parameters, CancellationToken.None);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidSdJwt);
        Assert.Contains("JSON object", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheIssuerKeyDoesNotVerify_FailsWithTheJwtCode()
    {
        var presentation = await PresentAsync(await IssueAsync(["/given_name"]));

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
        var jwt = await Host.SignJwtAsync(
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
        var presentation = (await PresentAsync(await IssueAsync(["/given_name"]))) + "!!!~";

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidDisclosure);
    }

    [Fact]
    public async Task VerifyAsync_WhenOverTheDisclosureLimit_Fails()
    {
        await using var host = new SdJwtTestHost(options => options.MaxDisclosureCount = 1);
        var presentation = await PresentAsync(await IssueAsync(["/given_name", "/family_name"]));

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

    /// <summary>
    /// A chain of disclosures whose values nest through further digests, from a flat issuer-signed payload, so the
    /// depth is reached only through resolved disclosure values: <c>a</c> holds the digest of <c>b</c>, which holds
    /// the digest of <c>c</c>, whose value is the innermost object.
    /// </summary>
    private async ValueTask<string> PresentNestedDisclosureChainAsync()
    {
        var c = CreateDisclosure("c", "{}");
        var b = CreateDisclosure("b", $$"""{"_sd":["{{Digest(c)}}"]}""");
        var a = CreateDisclosure("a", $$"""{"_sd":["{{Digest(b)}}"]}""");
        return await PresentCraftedAsync(
            $$"""{"_sd":["{{Digest(a)}}"]}""",
            a.Encoded,
            b.Encoded,
            c.Encoded
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenTheDisclosedValuesNestTooDeep_Fails()
    {
        await using var host = new SdJwtTestHost(options => options.MaxNestingDepth = 2);
        var presentation = await PresentNestedDisclosureChainAsync();

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
    public async Task VerifyAsync_WhenTheDisclosedValuesNestToTheLimit_Succeeds()
    {
        await using var host = new SdJwtTestHost(options => options.MaxNestingDepth = 3);
        var presentation = await PresentNestedDisclosureChainAsync();

        var result = await host.Verifier.VerifyAsync(
            presentation,
            new SdJwtVerificationParameters
            {
                IssuerJwtParameters = Host.CreateIssuerJwtParameters(),
            },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
        Assert.Equal(
            JsonValueKind.Object,
            result.Claims.GetProperty("a").GetProperty("b").GetProperty("c").ValueKind
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenTheDigestAlgorithmIsUnregistered_Fails()
    {
        var presentation = await PresentCraftedAsync("""{"_sd_alg":"sha-1","_sd":[]}""");

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm);
    }

    [Fact]
    public async Task VerifyAsync_WhenADisclosureIsUnreferenced_Fails()
    {
        var issued = await IssueAsync(["/given_name"]);
        var other = await IssueAsync(["/family_name"]);
        var presentation = (await PresentAsync(issued)) + other.Disclosures[0].Encoded + "~";

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.UnreferencedDisclosure);
    }

    [Fact]
    public async Task VerifyAsync_WhenADisclosureIsPresentedTwice_Fails()
    {
        var issued = await IssueAsync(["/given_name"]);
        var presentation = (await PresentAsync(issued)) + issued.Disclosures[0].Encoded + "~";

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DuplicateDisclosure);
    }

    [Fact]
    public async Task VerifyAsync_WhenADigestRepeats_Fails()
    {
        var disclosure = CreateDisclosure("given_name", "\"John\"");
        var digest = Digest(disclosure);
        var presentation = await PresentCraftedAsync(
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
        var presentation = await PresentCraftedAsync($$"""{"_sd":["{{decoy}}","{{decoy}}"]}""");

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DuplicateDigest);
    }

    [Fact]
    public async Task VerifyAsync_WhenADisclosedClaimIsAlreadyPresent_Fails()
    {
        // The claim name is the holder's input, so the reason names the rule and never the name.
        const string claimName = "given_name\nforged log line";
        var disclosure = CreateDisclosure(claimName, "\"John\"");
        var presentation = await PresentCraftedAsync(
            $$"""{"given_name\nforged log line":"Jane","_sd":["{{Digest(disclosure)}}"]}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DuplicateClaim);
        Assert.DoesNotContain("forged", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WhenAnObjectDigestReferencesAnArrayDisclosure_Fails()
    {
        var disclosure = CreateArrayDisclosure("\"DE\"");
        var presentation = await PresentCraftedAsync(
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
        var presentation = await PresentCraftedAsync(
            $$"""{"nationalities":[{"...":"{{Digest(disclosure)}}"}]}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.DisclosureMismatch);
    }

    [Theory]
    [InlineData("""{"_sd_alg":1,"_sd":[]}""")]
    [InlineData("""{"_sd_alg":"","_sd":[]}""")]
    [InlineData("""{"_sd_alg":null,"_sd":[]}""")]
    public async Task VerifyAsync_WhenTheSdAlgClaimIsNotANonEmptyString_Fails(string payloadJson)
    {
        var presentation = await PresentCraftedAsync(payloadJson);

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.UnsupportedDigestAlgorithm);
        Assert.Contains("non-empty string", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WhenAnObjectInsideAnArrayCarriesDigests_ResolvesThemAtTheElementsPath()
    {
        var name = CreateDisclosure("name", "\"x\"");
        var presentation = await PresentCraftedAsync(
            $$"""{"items":[1,{"_sd":["{{Digest(name)}}"]}]}""",
            name.Encoded
        );

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        Assert.Equal("x", result.Claims.GetProperty("items")[1].GetProperty("name").GetString());
        Assert.Equal("/items/1/name", Assert.Single(result.Disclosures).Path);
    }

    [Theory]
    [InlineData("""{"_sd":"x"}""")]
    [InlineData("""{"_sd":[1]}""")]
    [InlineData("""{"nationalities":[{"...":"x","extra":1}]}""")]
    [InlineData("""{"nationalities":[{"...":1}]}""")]
    public async Task VerifyAsync_WhenADigestStructureIsMalformed_Fails(string payloadJson)
    {
        var presentation = await PresentCraftedAsync(payloadJson);

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidDigestStructure);
    }

    [Theory]
    [InlineData("""{"address":{"...":"x"}}""")]
    [InlineData("""{"address":{"_sd_alg":"sha-256"}}""")]
    public async Task VerifyAsync_WhenAReservedNameIsMisplaced_Fails(string payloadJson)
    {
        var presentation = await PresentCraftedAsync(payloadJson);

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.ReservedClaimName);
    }

    [Fact]
    public async Task VerifyAsync_WhenAPlaceholderIsNotAnArrayElement_Fails()
    {
        var disclosure = CreateArrayDisclosure("\"DE\"");
        var presentation = await PresentCraftedAsync(
            $$"""{"...":"{{Digest(disclosure)}}"}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.ReservedClaimName);
    }

    [Fact]
    public async Task VerifyAsync_WhenThePayloadRepeatsAMemberName_Fails()
    {
        var presentation = await PresentCraftedAsync("""{"a":1,"a":2}""");

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidSdJwt);
        Assert.Contains(
            "issuer-signed JWT payload",
            result.FailureReason,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("""{"a":1,"a":2}""")]
    [InlineData("""{"inner":{"a":1,"a":2}}""")]
    [InlineData("""[{"a":1,"a":2}]""")]
    public async Task VerifyAsync_WhenADisclosedValueRepeatsAMemberName_FailsAsAnInvalidDisclosure(
        string valueJson
    )
    {
        // The repeat is in the holder's disclosure, so it is reported as such and never as a repeat in the
        // issuer-signed payload.
        var disclosure = CreateDisclosure("claim", valueJson);
        var presentation = await PresentCraftedAsync(
            $$"""{"_sd":["{{Digest(disclosure)}}"]}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidDisclosure);
        Assert.Contains("disclosed value", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheDisclosedValueCarriesDisclosures_ResolvesRecursively()
    {
        var street = CreateDisclosure("street_address", "\"123 Main St\"");
        var address = CreateDisclosure(
            "address",
            $$"""{"_sd":["{{Digest(street)}}"],"country":"US"}"""
        );
        var presentation = await PresentCraftedAsync(
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
    public async Task VerifyAsync_WhenTheIssuerSignedJwtIsANestedJwt_FailsEvenWithDecryptionKeys()
    {
        var issued = await IssueAsync(["/given_name"]);
        var nested = await Host.EncloseInJweAsync(issued.IssuerSignedJwt, Host.StrangerKey);
        var presentation = $"{nested}~{issued.Disclosures[0].Encoded}~";
        var parameters = new SdJwtVerificationParameters
        {
            IssuerJwtParameters = Host.CreateIssuerJwtParameters()
                .UseDecryptionKeys([Host.StrangerKey]),
        };
        // The JWT validator accepts the Nested JWT under these parameters, so the refusal is the verifier's own rule.
        var jwtResult = await Host
            .ServiceProvider.GetRequiredService<IJwtValidator>()
            .ValidateAsync(nested, parameters.IssuerJwtParameters, CancellationToken.None);
        Assert.True(jwtResult.IsValid);

        var result = await VerifyAsync(presentation, parameters);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidSdJwt);
    }

    [Fact]
    public async Task VerifyAsync_WithDecryptionKeysAndACompactJws_Succeeds()
    {
        var presentation = await PresentAsync(await IssueAsync(["/given_name"]));
        var parameters = new SdJwtVerificationParameters
        {
            IssuerJwtParameters = Host.CreateIssuerJwtParameters()
                .UseDecryptionKeys([Host.StrangerKey]),
        };

        var result = await VerifyAsync(presentation, parameters);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("exp", "1")]
    [InlineData("nbf", "1")]
    [InlineData("iss", "\"https://issuer.example\"")]
    [InlineData("cnf", "{}")]
    public async Task VerifyAsync_WithoutProfileAndAFormatProtectedClaimDisclosed_Fails(
        string claimName,
        string valueJson
    )
    {
        var disclosure = CreateDisclosure(claimName, valueJson);
        var presentation = await PresentCraftedAsync(
            $$"""{"_sd":["{{Digest(disclosure)}}"]}""",
            disclosure.Encoded
        );

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.ProtectedClaimDisclosed);
    }

    [Theory]
    [InlineData("""{"cnf":{"_sd":["AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"]}}""")]
    [InlineData("""{"exp":[{"...":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"}]}""")]
    [InlineData("""{"iss":{"inner":{"_sd":["AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"]}}}""")]
    public async Task VerifyAsync_WithoutProfileAndADigestBeneathAProtectedClaim_Fails(
        string payloadJson
    )
    {
        // A digest beneath a protected claim is refused whether or not a disclosure matches it: a withheld
        // disclosure and a decoy look alike, so the claim is plaintext in its entirety.
        var presentation = await PresentCraftedAsync(payloadJson);

        var result = await VerifyAsync(presentation);

        AssertFailure(result, SdJwtVerificationFailureCodes.ProtectedClaimDisclosed);
    }

    [Fact]
    public async Task VerifyAsync_WithKeyBinding_ReturnsTheVerifiedKeyBinding()
    {
        var issued = await IssueAsync(["/given_name"]);
        var presentation = await PresentAsync(issued, withKeyBinding: true);

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
        var presentation = await PresentAsync(
            await IssueAsync(["/given_name"]),
            withKeyBinding: true,
            nonce: "other"
        );

        var result = await VerifyAsync(presentation);

        Assert.True(result.IsValid);
        Assert.Null(result.KeyBinding);
        Assert.True(result.SdJwt.HasKeyBinding);
    }

    [Fact]
    public async Task VerifyAsync_WhenKeyBindingIsRequiredButAbsent_Fails()
    {
        var presentation = await PresentAsync(await IssueAsync(["/given_name"]));

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.KeyBindingRequired);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheNonceDiffers_Fails()
    {
        var presentation = await PresentAsync(
            await IssueAsync(["/given_name"]),
            withKeyBinding: true,
            nonce: "other"
        );

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidNonce);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheAudienceDiffers_Fails()
    {
        var presentation = await PresentAsync(
            await IssueAsync(["/given_name"]),
            withKeyBinding: true
        );
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
        var issued = await IssueAsync(["/given_name", "/family_name"]);
        var bound = await PresentAsync(issued, [issued.Disclosures[0]], withKeyBinding: true);
        var keyBindingJwt = bound[(bound.LastIndexOf('~') + 1)..];
        var presentation = (await PresentAsync(issued)) + keyBindingJwt;

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidSdHash);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsTooOld_Fails()
    {
        var presentation = await PresentAsync(
            await IssueAsync(["/given_name"]),
            withKeyBinding: true
        );
        Host.TimeProvider.Advance(TimeSpan.FromMinutes(11));

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.KeyBindingNotFresh);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsWithinTheSkew_Succeeds()
    {
        var presentation = await PresentAsync(
            await IssueAsync(["/given_name"]),
            withKeyBinding: true
        );
        Host.TimeProvider.Advance(TimeSpan.FromMinutes(9));

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsFromTheFuture_Fails()
    {
        var issued = await IssueAsync(["/given_name"]);
        var prefix = await PresentAsync(issued);
        var future = SdJwtTestHost.StartTime + TimeSpan.FromMinutes(6);
        var payload = $$"""
            {"iat":{{future.ToUnixTimeSeconds()}},"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(
                prefix
            )}}"}
            """;
        var keyBindingJwt = await Host.SignJwtAsync(Host.HolderCredentials, payload, "kb+jwt");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.KeyBindingNotFresh);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtIsSignedByAnotherKey_Fails()
    {
        var issued = await IssueAsync(["/given_name"]);
        var presentation = await Host.Holder.CreatePresentationAsync(
            new SdJwtPresentationOptions
            {
                SdJwt = issued,
                KeyBinding = new SdJwtKeyBindingOptions
                {
                    SigningCredentials = Host.StrangerCredentials,
                    Audience = Audience,
                    Nonce = Nonce,
                },
            },
            CancellationToken.None
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
        var issued = await IssueAsync(["/given_name"]);
        var prefix = await PresentAsync(issued);
        var payload = $$"""
            {"iat":{{SdJwtTestHost.StartTime.ToUnixTimeSeconds()}},"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(
                prefix
            )}}"}
            """;
        var keyBindingJwt = await Host.SignJwtAsync(Host.HolderCredentials, payload, "JWT");

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
        var issued = await IssueAsync(["/given_name"]);
        var prefix = await PresentAsync(issued);
        var payload =
            $$"""{"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(prefix)}}"}""";
        var keyBindingJwt = await Host.SignJwtAsync(Host.HolderCredentials, payload, "kb+jwt");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.KeyBindingNotFresh);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheIssuerSignedJwtCarriesNoCnf_Fails()
    {
        var presentation = await PresentAsync(
            await IssueAsync(["/given_name"], withHolderKey: false),
            withKeyBinding: true
        );

        var result = await VerifyAsync(presentation, CreateParameters(requireKeyBinding: true));

        AssertFailure(result, SdJwtVerificationFailureCodes.HolderKeyNotFound);
    }

    [Theory]
    [InlineData("""{"jwk":{"kty":"oct","k":"AAAA"}}""")]
    [InlineData("""{"jwk":"x"}""")]
    [InlineData("""{"x5c":[]}""")]
    [InlineData("""[]""")]
    public async Task VerifyAsync_WhenTheCnfNamesNoUsableKey_Fails(string cnfJson)
    {
        var prefix = await PresentCraftedAsync($$"""{"iss":"x","cnf":{{cnfJson}}}""");
        var payload = $$"""
            {"iat":{{SdJwtTestHost.StartTime.ToUnixTimeSeconds()}},"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(
                prefix
            )}}"}
            """;
        var keyBindingJwt = await Host.SignJwtAsync(Host.HolderCredentials, payload, "kb+jwt");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.HolderKeyNotFound);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtPayloadIsNotAnObject_Fails()
    {
        var sdJwt = await IssueAsync(["/given_name"]);
        var presentation = await PresentAsync(sdJwt, withKeyBinding: true);
        var keyBindingJwt = Host.Holder.Parse(presentation).KeyBindingJwt;
        Assert.NotNull(keyBindingJwt);
        var parameters = CreateParameters(requireKeyBinding: true);
        var realValidator = Host.ServiceProvider.GetRequiredService<IJwtValidator>();
        var issuerResult = await realValidator.ValidateAsync(
            sdJwt.IssuerSignedJwt,
            parameters.IssuerJwtParameters,
            CancellationToken.None
        );
        Assert.True(issuerResult.IsValid);
        var validator = CreateStrictMock<IJwtValidator>();
        validator
            .Setup(x =>
                x.ValidateAsync(
                    sdJwt.IssuerSignedJwt,
                    parameters.IssuerJwtParameters,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(issuerResult)
            .Verifiable();
        validator
            .Setup(x =>
                x.ValidateAsync(
                    keyBindingJwt,
                    It.Is<JwtValidationParameters>(p => p != parameters.IssuerJwtParameters),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, JwtValidationParameters, CancellationToken>(
                (token, jwtParameters, _) =>
                    ValueTask.FromResult(
                        Succeed(
                            jwtParameters,
                            new DecodedJwt(
                                Host.JoseSerializer.Parse(token),
                                JsonElement.Parse("[1]"),
                                Host.HolderKey
                            )
                        )
                    )
            )
            .Verifiable();

        var result = await CreateVerifierOver(validator.Object)
            .VerifyAsync(presentation, parameters, CancellationToken.None);

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidKeyBinding);
        Assert.Contains("JSON object", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheKeyBindingJwtDoesNotDecode_ReportsFixedTextWithoutTheToken()
    {
        var prefix = await PresentAsync(await IssueAsync(["/given_name"]));
        var payload = $$"""
            {"iat":{{SdJwtTestHost.StartTime.ToUnixTimeSeconds()}},"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(
                prefix
            )}}"}
            """;
        var keyBindingJwt = await Host.SignJwtAsync(Host.StrangerCredentials, payload, "kb+jwt");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.InvalidKeyBinding);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("could not be decoded", result.FailureReason, StringComparison.Ordinal);
        Assert.DoesNotContain(keyBindingJwt, result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WhenCnfIsDisclosedRatherThanPlaintext_Fails()
    {
        // 'cnf' is never selectively disclosable, so a 'cnf' in a disclosure is refused before any key binding is
        // judged, and the holder key is read from the issuer-signed payload alone.
        var cnf = CreateDisclosure("cnf", $$$"""{"jwk":{{{Host.HolderJwk.ToJsonString()}}}}""");
        var prefix = await PresentCraftedAsync($$"""{"_sd":["{{Digest(cnf)}}"]}""", cnf.Encoded);
        var payload = $$"""
            {"iat":{{SdJwtTestHost.StartTime.ToUnixTimeSeconds()}},"aud":"{{Audience}}","nonce":"{{Nonce}}","sd_hash":"{{SdJwtDigestAlgorithms.Sha256.ComputeDigest(
                prefix
            )}}"}
            """;
        var keyBindingJwt = await Host.SignJwtAsync(Host.HolderCredentials, payload, "kb+jwt");

        var result = await VerifyAsync(
            prefix + keyBindingJwt,
            CreateParameters(requireKeyBinding: true)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.ProtectedClaimDisclosed);
    }

    [Fact]
    public async Task VerifyAsync_WithSdJwtVcProfile_PassesTheStatusClaimThrough()
    {
        var issued = await IssueAsync(
            ["/sub", "/given_name"],
            VcClaimsJson,
            profile: SdJwtProfiles.SdJwtVc
        );
        var presentation = await PresentAsync(issued, withKeyBinding: true);

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
        var presentation = await PresentAsync(await IssueAsync(["/given_name"], VcClaimsJson));

        var result = await VerifyAsync(
            presentation,
            CreateParameters(profile: SdJwtProfiles.SdJwtVc)
        );

        AssertFailure(result, JwtValidationFailureCodes.InvalidTokenType);
    }

    [Fact]
    public async Task VerifyAsync_WithProfileAndNoVct_Fails()
    {
        var jwt = await Host.SignIssuerJwtAsync(
            """{"iss":"https://issuer.example"}""",
            "dc+sd-jwt"
        );

        var result = await VerifyAsync($"{jwt}~", CreateParameters(profile: SdJwtProfiles.SdJwtVc));

        AssertFailure(result, SdJwtVerificationFailureCodes.MissingRequiredClaim);
    }

    [Fact]
    public async Task VerifyAsync_WithProfileAndAProtectedClaimDisclosed_Fails()
    {
        var iss = CreateDisclosure("iss", "\"https://issuer.example\"");
        var jwt = await Host.SignIssuerJwtAsync(
            $$"""{"vct":"t","_sd":["{{Digest(iss)}}"]}""",
            "dc+sd-jwt"
        );

        var result = await VerifyAsync(
            $"{jwt}~{iss.Encoded}~",
            CreateParameters(profile: SdJwtProfiles.SdJwtVc)
        );

        AssertFailure(result, SdJwtVerificationFailureCodes.ProtectedClaimDisclosed);
    }

    [Fact]
    public async Task VerifyAsync_WithProfileAndADigestBeneathAProtectedClaim_Fails()
    {
        // The holder withholds the status_list disclosure, so the digest under 'status' matches nothing and would
        // otherwise be dropped as a decoy, leaving an empty status the application reads as unrevoked.
        var statusList = CreateDisclosure(
            "status_list",
            """{"idx":0,"uri":"https://issuer.example/status/1"}"""
        );
        var jwt = await Host.SignIssuerJwtAsync(
            $$$"""{"vct":"t","status":{"_sd":["{{{Digest(statusList)}}}"]}}""",
            "dc+sd-jwt"
        );

        var result = await VerifyAsync($"{jwt}~", CreateParameters(profile: SdJwtProfiles.SdJwtVc));

        AssertFailure(result, SdJwtVerificationFailureCodes.ProtectedClaimDisclosed);
        Assert.Contains("'status'", result.FailureReason, StringComparison.Ordinal);
        Assert.Contains("SD-JWT VC", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_WithProfileAndAProtectedNameNested_Succeeds()
    {
        // The profile protects top-level claims; a nested member of the same name is an ordinary claim.
        var exp = CreateDisclosure("exp", "1");
        var jwt = await Host.SignIssuerJwtAsync(
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
