// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jwt;

public class DefaultJsonWebTokenServiceTests : IAsyncLifetime
{
    private ServiceProvider ServiceProvider { get; }
    private IJwtEncoder Encoder { get; }
    private IJwtValidator Validator { get; }
    private IJoseSerializer JoseSerializer { get; }
    private SecretKey SigningKey { get; }
    private JoseSigningCredentials SigningCredentials { get; }
    private DateTimeOffset UtcNow { get; } = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public DefaultJsonWebTokenServiceTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<TimeProvider>(new FakeTimeProvider(UtcNow));
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Encoder = ServiceProvider.GetRequiredService<IJwtEncoder>();
        Validator = ServiceProvider.GetRequiredService<IJwtValidator>();
        JoseSerializer = ServiceProvider.GetRequiredService<IJoseSerializer>();

        var secretKeyFactory = ServiceProvider.GetRequiredService<ISecretKeyFactory>();
        byte[] keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        SigningKey = secretKeyFactory.CreateSymmetric(new KeyMetadata { KeyId = "test" }, keyBytes);

        var algorithmProvider = ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>();
        if (
            !algorithmProvider.Collection.TryGetSignatureAlgorithm(
                "HS256",
                out var signatureAlgorithm
            )
        )
            throw new InvalidOperationException("HS256 signature algorithm not found.");

        SigningCredentials = new JoseSigningCredentials(SigningKey, signatureAlgorithm);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
    }

    private ValueTask<string> EncodeTokenAsync(
        JwtTestTokenOptions options,
        CancellationToken cancellationToken
    )
    {
        var now = UtcNow;
        var parameters = new JwtEncodingParameters
        {
            SigningCredentials = SigningCredentials,
            Issuer = options.Issuer ?? "https://issuer.example",
            Audience = new StringValues(options.Audience ?? ["https://audience.example"]),
            IssuedAt = now,
            NotBefore = options.NotBefore ?? now.AddMinutes(-1),
            Expires = options.Expires ?? now.AddHours(1),
            SubjectClaims = options.SubjectClaims,
            ExtraPayloadClaims = options.ExtraPayloadClaims,
            TokenType = options.TokenType ?? JoseTokenTypes.Jwt,
        };
        return Encoder.EncodeAsync(parameters, cancellationToken);
    }

    private async ValueTask<JwtValidationResult> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken
    )
    {
        var parameters = new JwtValidationParameters().UseValidationKeys([SigningKey]);

        var result = await Validator.ValidateAsync(token, parameters, cancellationToken);

        Assert.True(result.IsValid);
        return result;
    }

    private async ValueTask<JwtValidationResult> EncodeAndValidateAsync(
        IEnumerable<Claim> subjectClaims,
        CancellationToken cancellationToken
    )
    {
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { SubjectClaims = subjectClaims },
            cancellationToken
        );
        return await ValidateTokenAsync(token, cancellationToken);
    }

    private async ValueTask<JwtValidationResult> EncodeAndValidateLifetimeAsync(
        IReadOnlyDictionary<string, object> lifetimeClaims,
        JwtTestLifetimeOptions options,
        CancellationToken cancellationToken
    )
    {
        // Encode through the JOSE serializer so the JWT encoder cannot add default 'iat'/'nbf'/'exp' claims.
        var signingOptions = new JoseSigningOptions(SigningCredentials);
        var encoder = JoseSerializer.CreateEncoder(signingOptions);
        var token = await encoder.EncodeAsync(lifetimeClaims, cancellationToken);

        var validationParameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateTokenLifeTime(lifetimeOptions =>
            {
                lifetimeOptions.RequireExpirationTime = options.RequireExpirationTime;
                lifetimeOptions.MaxLifetime = options.MaxLifetime;
                lifetimeOptions.RequireNotBefore = options.RequireNotBefore;
                lifetimeOptions.RequireIssuedAt = options.RequireIssuedAt;
            });

        return await Validator.ValidateAsync(token, validationParameters, cancellationToken);
    }

    #region EncodeJwt Tests

    [Fact]
    public async Task EncodeJwt_WhenSigned_ReturnsThreeSegmentCompactJws()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);

        Assert.Equal(2, token.Count(c => c == '.'));
    }

    [Fact]
    public async Task EncodeJwt_WhenNoCredentials_ThrowsArgumentException()
    {
        var parameters = new JwtEncodingParameters();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Encoder.EncodeAsync(parameters, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Encode_WhenLifetimeClaimsAreOmitted_SuppliesDefaults()
    {
        var parameters = new JwtEncodingParameters { SigningCredentials = SigningCredentials };

        var token = await Encoder.EncodeAsync(parameters, CancellationToken.None);

        var payload = Assert
            .IsType<DecodedJwt>(
                (await ValidateTokenAsync(token, CancellationToken.None)).DecodedJwt
            )
            .Payload;
        Assert.True(payload.TryGetProperty(JoseClaimNames.Payload.Iat, out _));
        Assert.True(payload.TryGetProperty(JoseClaimNames.Payload.Nbf, out _));
        Assert.True(payload.TryGetProperty(JoseClaimNames.Payload.Exp, out _));
    }

    [Fact]
    public async Task Encode_WhenTheJwtIsNotAToken_CarriesOnlyTheTimeClaimsItIsGiven()
    {
        var parameters = new JwtEncodingParameters
        {
            SigningCredentials = SigningCredentials,
            EnsureTokenLifetime = false,
            IssuedAt = UtcNow,
        };

        var token = await Encoder.EncodeAsync(parameters, CancellationToken.None);

        var payload = Assert
            .IsType<DecodedJwt>(
                (await ValidateTokenAsync(token, CancellationToken.None)).DecodedJwt
            )
            .Payload;
        Assert.Equal(
            UtcNow.ToUnixTimeSeconds(),
            payload.GetProperty(JoseClaimNames.Payload.Iat).GetInt64()
        );
        Assert.False(payload.TryGetProperty(JoseClaimNames.Payload.Nbf, out _));
        Assert.False(payload.TryGetProperty(JoseClaimNames.Payload.Exp, out _));
    }

    [Fact]
    public async Task Encode_WhenSingleExtraPayloadClaim_EmitsIt()
    {
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions
            {
                ExtraPayloadClaims = new Dictionary<string, object> { ["azp"] = "client" },
            },
            CancellationToken.None
        );

        var result = await ValidateTokenAsync(token, CancellationToken.None);

        var payload = Assert.IsType<DecodedJwt>(result.DecodedJwt).Payload;
        Assert.Equal("client", payload.GetProperty("azp").GetString());
    }

    [Theory]
    [InlineData("2024-01-02T03:04:05", "2024-01-02T03:04:05+00:00")]
    [InlineData("2024-01-02T03:04:05Z", "2024-01-02T03:04:05+00:00")]
    [InlineData("2024-01-02T03:04:05+05:00", "2024-01-02T03:04:05+05:00")]
    public async Task Encode_WhenDateTimeClaim_EmitsIso8601ViaSharedDatePolicy(
        string claimValue,
        string expected
    )
    {
        var result = await EncodeAndValidateAsync(
            [new Claim("event_at", claimValue, ClaimValueTypes.DateTime)],
            CancellationToken.None
        );

        var payload = Assert.IsType<DecodedJwt>(result.DecodedJwt).Payload;
        Assert.Equal(expected, payload.GetProperty("event_at").GetString());
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("0001-01-01T00:00:00+05:00")]
    public async Task Encode_WhenDateTimeClaimUnparseableOrUnrepresentable_EmitsVerbatimString(
        string claimValue
    )
    {
        var result = await EncodeAndValidateAsync(
            [new Claim("event_at", claimValue, ClaimValueTypes.DateTime)],
            CancellationToken.None
        );

        var payload = Assert.IsType<DecodedJwt>(result.DecodedJwt).Payload;
        Assert.Equal(claimValue, payload.GetProperty("event_at").GetString());
    }

    #endregion

    #region GetClaimsIdentityAsync Tests

    [Theory]
    [InlineData("1.2")]
    [InlineData("10:30")]
    [InlineData("1990-03-15")]
    [InlineData("2024-01-02T03:04:05Z")]
    public async Task GetClaimsIdentityAsync_WhenStringClaimLooksLikeDate_KeepsVerbatimStringClaim(
        string claimValue
    )
    {
        var result = await EncodeAndValidateAsync(
            [new Claim("custom", claimValue)],
            CancellationToken.None
        );

        var identity = await result.GetClaimsIdentityAsync(CancellationToken.None);

        var claim = Assert.Single(identity.FindAll("custom"));
        Assert.Equal(claimValue, claim.Value);
        Assert.Equal(ClaimValueTypes.String, claim.ValueType);
    }

    [Theory]
    [InlineData("\"text\"")]
    [InlineData("\"1.2\"")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("-2147483649")]
    [InlineData("9007199254740993")]
    [InlineData("18446744073709551615")]
    [InlineData("18446744073709551616")]
    [InlineData("1.5")]
    [InlineData("{\"a\":1}")]
    public async Task GetClaimsIdentityAsync_WhenClaimsReEncoded_RoundTripsJsonValue(string json)
    {
        var original = JsonElement.Parse(json);
        var firstToken = await EncodeTokenAsync(
            new JwtTestTokenOptions
            {
                ExtraPayloadClaims = new Dictionary<string, object> { ["custom"] = original },
            },
            CancellationToken.None
        );
        var firstResult = await ValidateTokenAsync(firstToken, CancellationToken.None);
        var identity = await firstResult.GetClaimsIdentityAsync(CancellationToken.None);
        var claims = identity.FindAll("custom");

        var secondResult = await EncodeAndValidateAsync(claims, CancellationToken.None);

        var payload = Assert.IsType<DecodedJwt>(secondResult.DecodedJwt).Payload;
        Assert.Equal(json, payload.GetProperty("custom").GetRawText());
    }

    #endregion

    #region ValidateAsync Success Tests

    [Fact]
    public async Task ValidateAsync_WhenValidWithMatchingClaims_Succeeds()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateIssuer("https://issuer.example")
            .ValidateAudience("https://audience.example")
            .ValidateTokenLifeTime();

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
        Assert.NotNull(result.DecodedJwt);
    }

    [Fact]
    public async Task ValidateAsync_WhenSubjectClaims_ProducesClaimsIdentity()
    {
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions
            {
                SubjectClaims = [new Claim("name", "Alice"), new Claim("role", "admin")],
            },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters().UseValidationKeys([SigningKey]);

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);

        var identity = await result.GetClaimsIdentityAsync(CancellationToken.None);
        Assert.True(identity.IsAuthenticated);
        Assert.Equal("Alice", identity.FindFirst("name")?.Value);
        Assert.Equal("admin", identity.FindFirst("role")?.Value);
    }

    [Fact]
    public async Task ValidateAsync_WhenMultiAudienceContainsExpected_Succeeds()
    {
        // A multi-audience token must validate against a resource that identifies itself with one of the audiences,
        // even though the token also carries other audiences (RFC 7519 §4.1.3).
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { Audience = ["urn:resource:api", "urn:resource:openid"] },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateAudience("urn:resource:api");

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
    }

    #endregion

    #region ValidateAsync Failure Tests

    [Fact]
    public async Task ValidateAsync_WhenIssuerMismatch_Fails()
    {
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { Issuer = "https://wrong.example" },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateIssuer("https://issuer.example");

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidIssuer, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenMultiAudienceMissingExpected_Fails()
    {
        // A multi-audience token must be rejected when none of its audiences identify the validating resource.
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { Audience = ["urn:resource:other", "urn:resource:openid"] },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateAudience("urn:resource:api");

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenClaimValueMismatch_FailsWithInvalidClaim()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateClaim(
                JoseClaimNames.Payload.Iss,
                usePayload: true,
                allowCollection: false,
                ["https://other.example"]
            );

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidClaim, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpired_Fails()
    {
        var past = UtcNow.AddHours(-2);
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { NotBefore = past.AddMinutes(-1), Expires = past },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateTokenLifeTime();

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.Expired, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoValidationKeys_FailsWithKeyNotFound()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);

        var parameters = new JwtValidationParameters().UseValidationKeys([]);

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtSecretKeyNotFoundException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenTampered_FailsWithDecodeException()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);
        var tampered = token[..^4] + "AAAA";

        var parameters = new JwtValidationParameters().UseValidationKeys([SigningKey]);

        var result = await Validator.ValidateAsync(tampered, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtDecodeException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidSignature, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenGetClaimsIdentityOnFailedResult_Throws()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);

        var parameters = new JwtValidationParameters().UseValidationKeys([]);

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await result.GetClaimsIdentityAsync(CancellationToken.None)
        );
    }

    [Fact]
    public async Task ValidateAsync_WhenAssertionIsNotSatisfied_FailsWithAssertionFailed()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .RequireAssertion((_, _) => ValueTask.FromResult(false));

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.AssertionFailed, result.FailureCode);
    }

    #endregion

    #region ValidateTokenLifeTime NumericDate Tests

    [Fact]
    public async Task ValidateAsync_WhenExpIsFractionalAndInFuture_Succeeds()
    {
        var exp = UtcNow.AddHours(1).ToUnixTimeSeconds() + 0.5;

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = exp },
            new JwtTestLifetimeOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpOutOfRangeAndExpirationNotRequired_Fails()
    {
        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = 99_999_999_999_999L },
            new JwtTestLifetimeOptions { RequireExpirationTime = false },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpIsDateString_Fails()
    {
        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = "2099-01-01T00:00:00Z" },
            new JwtTestLifetimeOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenNbfIsNotANumericDate_Fails()
    {
        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["nbf"] = "soon" },
            new JwtTestLifetimeOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenIatIsNotANumericDate_Fails()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = "earlier", ["exp"] = now + 60 },
            new JwtTestLifetimeOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("'iat'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(JwtValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenIatIsInFutureWithinClockSkew_Succeeds()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now + 300, ["exp"] = now + 600 },
            new JwtTestLifetimeOptions(),
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenIatIsInFutureBeyondClockSkew_Fails()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now + 301, ["exp"] = now + 600 },
            new JwtTestLifetimeOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("issued in the future", exception.Message, StringComparison.Ordinal);
        Assert.Equal(JwtValidationFailureCodes.NotYetValid, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenNotBeforeIsInFutureBeyondClockSkew_FailsAsNotYetValid()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["nbf"] = now + 301, ["exp"] = now + 600 },
            new JwtTestLifetimeOptions(),
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("not valid before", exception.Message, StringComparison.Ordinal);
        Assert.Equal(JwtValidationFailureCodes.NotYetValid, result.FailureCode);
    }

    #endregion

    #region ValidateTokenLifeTime RequireNotBefore / RequireIssuedAt Tests

    [Fact]
    public async Task ValidateAsync_WhenNotBeforeIsRequiredAndMissing_Fails()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now, ["exp"] = now + 300 },
            new JwtTestLifetimeOptions { RequireNotBefore = true },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("not-before", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuedAtIsRequiredAndMissing_Fails()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["nbf"] = now, ["exp"] = now + 300 },
            new JwtTestLifetimeOptions { RequireIssuedAt = true },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("issued-at", exception.Message, StringComparison.Ordinal);
        Assert.Equal(JwtValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenRequiredNotBeforeAndIssuedAtArePresent_Succeeds()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object>
            {
                ["iat"] = now,
                ["nbf"] = now,
                ["exp"] = now + 300,
            },
            new JwtTestLifetimeOptions { RequireNotBefore = true, RequireIssuedAt = true },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    #endregion

    #region ValidateTokenLifeTime MaxLifetime Tests

    [Fact]
    public async Task ValidateAsync_WhenLifetimeEqualsMaximum_Succeeds()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now, ["exp"] = now + 300 },
            new JwtTestLifetimeOptions { MaxLifetime = TimeSpan.FromMinutes(5) },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenLifetimeExceedsMaximum_Fails()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now, ["exp"] = now + 301 },
            new JwtTestLifetimeOptions { MaxLifetime = TimeSpan.FromMinutes(5) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("exceeds the maximum", exception.Message, StringComparison.Ordinal);
        Assert.Equal(JwtValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuedLongerAgoThanMaximum_Fails()
    {
        // Only a minute of validity remains, but the token has been valid since it was issued ten minutes ago.
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now - 600, ["exp"] = now + 60 },
            new JwtTestLifetimeOptions { MaxLifetime = TimeSpan.FromMinutes(5) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenNbfPrecedesIatBeyondMaximum_Fails()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object>
            {
                ["iat"] = now,
                ["nbf"] = now - 600,
                ["exp"] = now + 60,
            },
            new JwtTestLifetimeOptions { MaxLifetime = TimeSpan.FromMinutes(5) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoIatOrNbfAndExpWithinMaximumPlusClockSkew_Succeeds()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = now + 600 },
            new JwtTestLifetimeOptions { MaxLifetime = TimeSpan.FromMinutes(5) },
            CancellationToken.None
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoIatOrNbfAndExpBeyondMaximumPlusClockSkew_Fails()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = now + 601 },
            new JwtTestLifetimeOptions { MaxLifetime = TimeSpan.FromMinutes(5) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenMaximumSetAndExpAbsent_FailsEvenIfExpirationNotRequired()
    {
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now },
            new JwtTestLifetimeOptions
            {
                RequireExpirationTime = false,
                MaxLifetime = TimeSpan.FromMinutes(5),
            },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("missing an expiration time", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpAtMaxNumericDateAndMaximumSet_Fails()
    {
        const long maxNumericDate = 253_402_300_799L;
        var now = UtcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now, ["exp"] = maxNumericDate },
            new JwtTestLifetimeOptions { MaxLifetime = TimeSpan.FromMinutes(5) },
            CancellationToken.None
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    #endregion

    #region ValidateTokenType Tests

    [Fact]
    public async Task ValidateAsync_WhenTokenTypeMatches_Succeeds()
    {
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { TokenType = JoseTokenTypes.AccessTokenJwt },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt);

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
        Assert.Null(result.FailureCode);
    }

    [Theory]
    [InlineData("AT+JWT")]
    [InlineData("application/at+jwt")]
    [InlineData("Application/AT+JWT")]
    public async Task ValidateAsync_WhenTokenTypeMatchesByMediaTypeRules_Succeeds(
        string encodedTokenType
    )
    {
        // RFC 7515 §4.1.9: the 'application/' prefix is optional and the comparison is case-insensitive, so these all
        // match the expected 'at+jwt'.
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { TokenType = encodedTokenType },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt);

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenTypeMismatch_Fails()
    {
        // A plain 'JWT'-typed token must be rejected when 'at+jwt' is required (token-substitution defense).
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { TokenType = JoseTokenTypes.Jwt },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt);

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidTokenType, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnyOfSeveralTokenTypesMatch_Succeeds()
    {
        var token = await EncodeTokenAsync(
            new JwtTestTokenOptions { TokenType = JoseTokenTypes.LogoutJwt },
            CancellationToken.None
        );

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt, JoseTokenTypes.LogoutJwt);

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
    }

    #endregion

    #region JwtValidationContext Tests

    [Fact]
    public async Task ValidateAsync_WhenAHandlerFailsWithACode_RecordsTheFirstCode()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);
        JwtValidationContext? captured = null;

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .RequireAssertion(
                (context, _) =>
                {
                    context.Fail("custom_code", "The custom check failed.");
                    captured = context;
                    return ValueTask.FromResult(false);
                }
            );

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        var capturedContext = Assert.IsType<JwtValidationContext>(captured);
        Assert.True(capturedContext.HasFailed);
        Assert.Equal("custom_code", capturedContext.FailureCode);
        Assert.Equal("custom_code", result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenAHandlerFailsWithAnEmptyCode_Throws()
    {
        var token = await EncodeTokenAsync(new JwtTestTokenOptions(), CancellationToken.None);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([SigningKey])
            .RequireAssertion(
                (context, _) =>
                {
                    Assert.Throws<ArgumentException>(() => context.Fail(string.Empty, "reason"));
                    return ValueTask.FromResult(true);
                }
            );

        var result = await Validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
    }

    #endregion
}
