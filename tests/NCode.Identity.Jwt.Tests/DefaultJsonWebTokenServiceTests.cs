#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
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
    private readonly ServiceProvider _serviceProvider;
    private readonly IJwtEncoder _encoder;
    private readonly IJwtValidator _validator;
    private readonly IJoseSerializer _joseSerializer;
    private readonly SecretKey _signingKey;
    private readonly JoseSigningCredentials _signingCredentials;
    private readonly DateTimeOffset _utcNow = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public DefaultJsonWebTokenServiceTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<TimeProvider>(new TestTimeProvider(_utcNow));
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        _encoder = _serviceProvider.GetRequiredService<IJwtEncoder>();
        _validator = _serviceProvider.GetRequiredService<IJwtValidator>();
        _joseSerializer = _serviceProvider.GetRequiredService<IJoseSerializer>();

        var secretKeyFactory = _serviceProvider.GetRequiredService<ISecretKeyFactory>();
        byte[] keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        _signingKey = secretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "test" },
            keyBytes
        );

        var algorithmProvider = _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>();
        if (
            !algorithmProvider.Collection.TryGetSignatureAlgorithm(
                "HS256",
                out var signatureAlgorithm
            )
        )
            throw new InvalidOperationException("HS256 signature algorithm not found.");

        _signingCredentials = new JoseSigningCredentials(_signingKey, signatureAlgorithm);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
    }

    private string EncodeToken(
        string issuer = "https://issuer.example",
        string[]? audience = null,
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null,
        IEnumerable<Claim>? subjectClaims = null,
        string? tokenType = null,
        IReadOnlyDictionary<string, object>? extraPayloadClaims = null
    )
    {
        var now = _utcNow;
        var parameters = new JwtEncodingParameters
        {
            SigningCredentials = _signingCredentials,
            Issuer = issuer,
            Audience = new StringValues(audience ?? ["https://audience.example"]),
            IssuedAt = now,
            NotBefore = notBefore ?? now.AddMinutes(-1),
            Expires = expires ?? now.AddHours(1),
            SubjectClaims = subjectClaims,
            ExtraPayloadClaims = extraPayloadClaims,
            TokenType = tokenType ?? JoseTokenTypes.Jwt,
        };
        return _encoder.Encode(parameters);
    }

    private async Task<JwtValidationResult> ValidateTokenAsync(string token)
    {
        var parameters = new JwtValidationParameters().UseValidationKeys([_signingKey]);

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        return result;
    }

    private async Task<JwtValidationResult> EncodeAndValidateAsync(IEnumerable<Claim> subjectClaims)
    {
        var token = EncodeToken(subjectClaims: subjectClaims);
        return await ValidateTokenAsync(token);
    }

    private async Task<JwtValidationResult> EncodeAndValidateLifetimeAsync(
        IReadOnlyDictionary<string, object> lifetimeClaims,
        bool requireExpirationTime = true,
        TimeSpan? maxLifetime = null,
        bool requireNotBefore = false,
        bool requireIssuedAt = false
    )
    {
        // Encode through the JOSE serializer so the JWT encoder cannot add default 'iat'/'nbf'/'exp' claims.
        var signingOptions = new JoseSigningOptions(_signingCredentials);
        var encoder = _joseSerializer.CreateEncoder(signingOptions);
        var token = encoder.Encode(lifetimeClaims);

        var validationParameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateTokenLifeTime(options =>
            {
                options.RequireExpirationTime = requireExpirationTime;
                options.MaxLifetime = maxLifetime;
                options.RequireNotBefore = requireNotBefore;
                options.RequireIssuedAt = requireIssuedAt;
            });

        return await _validator.ValidateAsync(token, validationParameters, CancellationToken.None);
    }

    #region EncodeJwt Tests

    [Fact]
    public void EncodeJwt_WhenSigned_ReturnsThreeSegmentCompactJws()
    {
        var token = EncodeToken();

        Assert.Equal(2, token.Count(c => c == '.'));
    }

    [Fact]
    public void EncodeJwt_WhenNoCredentials_ThrowsArgumentException()
    {
        var parameters = new JwtEncodingParameters();

        Assert.Throws<ArgumentException>(() => _encoder.Encode(parameters));
    }

    [Fact]
    public async Task Encode_WhenLifetimeClaimsAreOmitted_SuppliesDefaults()
    {
        var parameters = new JwtEncodingParameters { SigningCredentials = _signingCredentials };

        var token = _encoder.Encode(parameters);

        var payload = Assert
            .IsType<DecodedJwt>((await ValidateTokenAsync(token)).DecodedJwt)
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
            SigningCredentials = _signingCredentials,
            EnsureTokenLifetime = false,
            IssuedAt = _utcNow,
        };

        var token = _encoder.Encode(parameters);

        var payload = Assert
            .IsType<DecodedJwt>((await ValidateTokenAsync(token)).DecodedJwt)
            .Payload;
        Assert.Equal(
            _utcNow.ToUnixTimeSeconds(),
            payload.GetProperty(JoseClaimNames.Payload.Iat).GetInt64()
        );
        Assert.False(payload.TryGetProperty(JoseClaimNames.Payload.Nbf, out _));
        Assert.False(payload.TryGetProperty(JoseClaimNames.Payload.Exp, out _));
    }

    [Fact]
    public async Task Encode_WhenSingleExtraPayloadClaim_EmitsIt()
    {
        var token = EncodeToken(
            extraPayloadClaims: new Dictionary<string, object> { ["azp"] = "client" }
        );

        var result = await ValidateTokenAsync(token);

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
        var result = await EncodeAndValidateAsync([
            new Claim("event_at", claimValue, ClaimValueTypes.DateTime),
        ]);

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
        var result = await EncodeAndValidateAsync([
            new Claim("event_at", claimValue, ClaimValueTypes.DateTime),
        ]);

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
        var result = await EncodeAndValidateAsync([new Claim("custom", claimValue)]);

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
        var firstToken = EncodeToken(
            extraPayloadClaims: new Dictionary<string, object> { ["custom"] = original }
        );
        var firstResult = await ValidateTokenAsync(firstToken);
        var identity = await firstResult.GetClaimsIdentityAsync(CancellationToken.None);
        var claims = identity.FindAll("custom");

        var secondResult = await EncodeAndValidateAsync(claims);

        var payload = Assert.IsType<DecodedJwt>(secondResult.DecodedJwt).Payload;
        Assert.Equal(json, payload.GetProperty("custom").GetRawText());
    }

    #endregion

    #region ValidateAsync Success Tests

    [Fact]
    public async Task ValidateAsync_WhenValidWithMatchingClaims_Succeeds()
    {
        var token = EncodeToken();

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateIssuer("https://issuer.example")
            .ValidateAudience("https://audience.example")
            .ValidateTokenLifeTime();

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
        Assert.NotNull(result.DecodedJwt);
    }

    [Fact]
    public async Task ValidateAsync_WhenSubjectClaims_ProducesClaimsIdentity()
    {
        var token = EncodeToken(
            subjectClaims: [new Claim("name", "Alice"), new Claim("role", "admin")]
        );

        var parameters = new JwtValidationParameters().UseValidationKeys([_signingKey]);

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

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
        var token = EncodeToken(audience: ["urn:resource:api", "urn:resource:openid"]);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateAudience("urn:resource:api");

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
    }

    #endregion

    #region ValidateAsync Failure Tests

    [Fact]
    public async Task ValidateAsync_WhenIssuerMismatch_Fails()
    {
        var token = EncodeToken(issuer: "https://wrong.example");

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateIssuer("https://issuer.example");

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidIssuer, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenMultiAudienceMissingExpected_Fails()
    {
        // A multi-audience token must be rejected when none of its audiences identify the validating resource.
        var token = EncodeToken(audience: ["urn:resource:other", "urn:resource:openid"]);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateAudience("urn:resource:api");

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenClaimValueMismatch_FailsWithInvalidClaim()
    {
        var token = EncodeToken();

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateClaim(
                JoseClaimNames.Payload.Iss,
                usePayload: true,
                allowCollection: false,
                ["https://other.example"]
            );

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidClaim, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpired_Fails()
    {
        var past = _utcNow.AddHours(-2);
        var token = EncodeToken(notBefore: past.AddMinutes(-1), expires: past);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateTokenLifeTime();

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoValidationKeys_FailsWithKeyNotFound()
    {
        var token = EncodeToken();

        var parameters = new JwtValidationParameters().UseValidationKeys([]);

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtSecretKeyNotFoundException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenTampered_FailsWithDecodeException()
    {
        var token = EncodeToken();
        var tampered = token[..^4] + "AAAA";

        var parameters = new JwtValidationParameters().UseValidationKeys([_signingKey]);

        var result = await _validator.ValidateAsync(tampered, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtDecodeException>(result.Exception);
        Assert.Equal(JwtValidationFailureCodes.InvalidSignature, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenGetClaimsIdentityOnFailedResult_Throws()
    {
        var token = EncodeToken();

        var parameters = new JwtValidationParameters().UseValidationKeys([]);

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await result.GetClaimsIdentityAsync(CancellationToken.None)
        );
    }

    #endregion

    #region ValidateTokenLifeTime NumericDate Tests

    [Fact]
    public async Task ValidateAsync_WhenExpIsFractionalAndInFuture_Succeeds()
    {
        var exp = _utcNow.AddHours(1).ToUnixTimeSeconds() + 0.5;

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = exp }
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpOutOfRangeAndExpirationNotRequired_Fails()
    {
        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = 99_999_999_999_999L },
            requireExpirationTime: false
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpIsDateString_Fails()
    {
        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = "2099-01-01T00:00:00Z" }
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenNbfIsNotANumericDate_Fails()
    {
        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["nbf"] = "soon" }
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenIatIsNotANumericDate_Fails()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = "earlier", ["exp"] = now + 60 }
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("'iat'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenIatIsInFutureWithinClockSkew_Succeeds()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now + 300, ["exp"] = now + 600 }
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenIatIsInFutureBeyondClockSkew_Fails()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now + 301, ["exp"] = now + 600 }
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("issued in the future", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region ValidateTokenLifeTime RequireNotBefore / RequireIssuedAt Tests

    [Fact]
    public async Task ValidateAsync_WhenNotBeforeIsRequiredAndMissing_Fails()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now, ["exp"] = now + 300 },
            requireNotBefore: true
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("not-before", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuedAtIsRequiredAndMissing_Fails()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["nbf"] = now, ["exp"] = now + 300 },
            requireIssuedAt: true
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("issued-at", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenRequiredNotBeforeAndIssuedAtArePresent_Succeeds()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object>
            {
                ["iat"] = now,
                ["nbf"] = now,
                ["exp"] = now + 300,
            },
            requireNotBefore: true,
            requireIssuedAt: true
        );

        Assert.True(result.IsValid);
    }

    #endregion

    #region ValidateTokenLifeTime MaxLifetime Tests

    [Fact]
    public async Task ValidateAsync_WhenLifetimeEqualsMaximum_Succeeds()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now, ["exp"] = now + 300 },
            maxLifetime: TimeSpan.FromMinutes(5)
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenLifetimeExceedsMaximum_Fails()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now, ["exp"] = now + 301 },
            maxLifetime: TimeSpan.FromMinutes(5)
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("exceeds the maximum", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenIssuedLongerAgoThanMaximum_Fails()
    {
        // Only a minute of validity remains, but the token has been valid since it was issued ten minutes ago.
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now - 600, ["exp"] = now + 60 },
            maxLifetime: TimeSpan.FromMinutes(5)
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenNbfPrecedesIatBeyondMaximum_Fails()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object>
            {
                ["iat"] = now,
                ["nbf"] = now - 600,
                ["exp"] = now + 60,
            },
            maxLifetime: TimeSpan.FromMinutes(5)
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoIatOrNbfAndExpWithinMaximumPlusClockSkew_Succeeds()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = now + 600 },
            maxLifetime: TimeSpan.FromMinutes(5)
        );

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoIatOrNbfAndExpBeyondMaximumPlusClockSkew_Fails()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["exp"] = now + 601 },
            maxLifetime: TimeSpan.FromMinutes(5)
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenMaximumSetAndExpAbsent_FailsEvenIfExpirationNotRequired()
    {
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now },
            requireExpirationTime: false,
            maxLifetime: TimeSpan.FromMinutes(5)
        );

        Assert.False(result.IsValid);
        var exception = Assert.IsType<JwtValidationException>(result.Exception);
        Assert.Contains("missing an expiration time", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpAtMaxNumericDateAndMaximumSet_Fails()
    {
        const long maxNumericDate = 253_402_300_799L;
        var now = _utcNow.ToUnixTimeSeconds();

        var result = await EncodeAndValidateLifetimeAsync(
            new Dictionary<string, object> { ["iat"] = now, ["exp"] = maxNumericDate },
            maxLifetime: TimeSpan.FromMinutes(5)
        );

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    #endregion

    #region ValidateTokenType Tests

    [Fact]
    public async Task ValidateAsync_WhenTokenTypeMatches_Succeeds()
    {
        var token = EncodeToken(tokenType: JoseTokenTypes.AccessTokenJwt);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt);

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

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
        var token = EncodeToken(tokenType: encodedTokenType);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt);

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenTypeMismatch_Fails()
    {
        // A plain 'JWT'-typed token must be rejected when 'at+jwt' is required (token-substitution defense).
        var token = EncodeToken(tokenType: JoseTokenTypes.Jwt);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt);

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnyOfSeveralTokenTypesMatch_Succeeds()
    {
        var token = EncodeToken(tokenType: JoseTokenTypes.LogoutJwt);

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .ValidateTokenType(JoseTokenTypes.AccessTokenJwt, JoseTokenTypes.LogoutJwt);

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Exception);
    }

    #endregion

    #region JwtValidationContext Tests

    [Fact]
    public async Task ValidateAsync_WhenAHandlerFailsWithACode_RecordsTheFirstCode()
    {
        var token = EncodeToken();
        JwtValidationContext? captured = null;

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .RequireAssertion(
                (context, _) =>
                {
                    context.Fail("custom_code", "The custom check failed.");
                    captured = context;
                    return ValueTask.FromResult(false);
                }
            );

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.False(result.IsValid);
        var capturedContext = Assert.IsType<JwtValidationContext>(captured);
        Assert.True(capturedContext.HasFailed);
        Assert.Equal("custom_code", capturedContext.FailureCode);
        Assert.Equal("custom_code", result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenAHandlerFailsWithAnEmptyCode_Throws()
    {
        var token = EncodeToken();

        var parameters = new JwtValidationParameters()
            .UseValidationKeys([_signingKey])
            .RequireAssertion(
                (context, _) =>
                {
                    Assert.Throws<ArgumentException>(() => context.Fail(string.Empty, "reason"));
                    return ValueTask.FromResult(true);
                }
            );

        var result = await _validator.ValidateAsync(token, parameters, CancellationToken.None);

        Assert.True(result.IsValid);
    }

    #endregion
}
