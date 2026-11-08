// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cwt.Issuance;
using NCode.Identity.Cwt.Validation;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cwt.Tests.Validation;

public class DefaultCwtValidatorTests
{
    private const string Issuer = "coap://as.example.com";
    private const string Audience = "coap://light.example.com";

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static CwtValidationParameters Parameters(
        CwtTestHost host,
        Func<CwtValidationParameters, CwtValidationParameters>? customize = null
    )
    {
        var parameters = new CwtValidationParameters
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
            ValidIssuers = [Issuer],
            ValidAudiences = [Audience],
            ValidationKeys = [host.EcPublicKey()],
        };
        return customize?.Invoke(parameters) ?? parameters;
    }

    private static async Task<CwtValidationResult> Validate(
        CwtTestHost host,
        byte[] token,
        CwtValidationParameters parameters
    ) => await host.Validator.ValidateAsync(token, parameters, CancellationToken.None);

    private static CwtIssuanceParameters Issuance(CwtTestHost host, DateTimeOffset now) =>
        new()
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            Issuer = Issuer,
            Audiences = [Audience],
            IssuedAt = now,
            ExpiresAt = now + TimeSpan.FromHours(1),
        };

    private static ValueTask<byte[]> IssueWithoutExpirationAsync(CwtTestHost host) =>
        host.Issuer.IssueAsync(
            new CwtIssuanceParameters
            {
                Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
                Issuer = Issuer,
                Audiences = [Audience],
            },
            CancellationToken.None
        );

    private static CwtValidationParameters Lifetime(
        CwtTestHost host,
        TimeSpan? maxLifetime = null,
        bool requireNotBefore = false,
        bool requireIssuedAt = false,
        TimeSpan? clockSkew = null
    ) =>
        new()
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
            ValidIssuers = [Issuer],
            ValidAudiences = [Audience],
            ValidationKeys = [host.EcPublicKey()],
            MaxLifetime = maxLifetime,
            RequireNotBefore = requireNotBefore,
            RequireIssuedAt = requireIssuedAt,
            ClockSkew = clockSkew ?? TimeSpan.FromMinutes(5),
        };

    private static ValueTask<byte[]> IssueLifetimeAsync(
        CwtTestHost host,
        DateTimeOffset? notBefore,
        DateTimeOffset? issuedAt,
        DateTimeOffset? expiresAt
    ) =>
        host.Issuer.IssueAsync(
            new CwtIssuanceParameters
            {
                Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
                Issuer = Issuer,
                Audiences = [Audience],
                NotBefore = notBefore,
                IssuedAt = issuedAt,
                ExpiresAt = expiresAt,
            },
            CancellationToken.None
        );

    private static ValueTask<byte[]> SignClaimsAsync(
        CwtTestHost host,
        Dictionary<CoseLabel, CoseValue> claims
    ) =>
        host.Serializer.EncodeSign1Async(
            host.Serializer.EncodeMap(claims),
            new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

    private static CoseValue NumericDate(DateTimeOffset instant) =>
        CoseValue.FromInt64(instant.ToUnixTimeSeconds());

    private static async ValueTask<byte[]> IssueTypedAsync(CwtTestHost host, bool typed)
    {
        var issuance = Issuance(host, CwtTestHost.StartTime);
        return await host.Issuer.IssueAsync(
            new CwtIssuanceParameters
            {
                Credentials = issuance.Credentials,
                Issuer = Issuer,
                Audiences = [Audience],
                ExpiresAt = issuance.ExpiresAt,
                TokenType = typed ? "application/example+cwt" : null,
            },
            CancellationToken.None
        );
    }

    private static CwtValidationParameters ExpectingType(CwtTestHost host, string type) =>
        Parameters(
            host,
            p => new CwtValidationParameters
            {
                ValidAlgorithms = p.ValidAlgorithms,
                ValidIssuers = p.ValidIssuers,
                ValidAudiences = p.ValidAudiences,
                ValidationKeys = p.ValidationKeys,
                TokenType = type,
            }
        );

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_SignedExample_IsValid()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);

        var result = await Validate(host, Hex(CwtVectors.SignedCwt), Parameters(host));

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal(Issuer, result.Claims.Issuer);
        Assert.Equal(CoseMessageType.Sign1, result.Message.MessageType);
        Assert.Null(result.Decryption);
    }

    [Fact]
    public async Task ValidateAsync_SignedExampleUnderTheCwtTag_IsValid()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);
        var token = Hex("D83D" + CwtVectors.SignedCwt);

        var result = await Validate(host, token, Parameters(host));

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Theory]
    [InlineData(CwtVectors.MacedCwt)]
    [InlineData("D83D" + CwtVectors.MacedCwt)]
    public async Task ValidateAsync_MacedExample_IsValid(string hex)
    {
        await using var host = new CwtTestHost(
            CwtVectors.RfcTime,
            [CoseAlgorithms.HmacSha256Truncated64]
        );
        var parameters = Parameters(
            host,
            p => new CwtValidationParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.HmacSha256Truncated64],
                ValidIssuers = p.ValidIssuers,
                ValidAudiences = p.ValidAudiences,
                ValidationKeys = [host.Hmac()],
            }
        );

        var result = await Validate(host, Hex(hex), parameters);

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal(CoseMessageType.Mac0, result.Message.MessageType);
    }

    [Theory]
    [InlineData("2015-10-05T17:20:00Z", false, CwtValidationFailureCodes.Expired)]
    [InlineData("2015-10-05T17:12:00Z", true, null)]
    [InlineData("2015-10-04T07:40:00Z", false, CwtValidationFailureCodes.NotYetValid)]
    public async Task ValidateAsync_SignedExampleAtATime_AppliesTheLifetime(
        string instant,
        bool expectedValid,
        string? expectedCode
    )
    {
        await using var host = new CwtTestHost(
            DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture)
        );

        var result = await Validate(host, Hex(CwtVectors.SignedCwt), Parameters(host));

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(expectedCode, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_AlgorithmNotPermitted_ReportsTheCoseCode()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);
        var parameters = Parameters(
            host,
            p => new CwtValidationParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.Es384],
                ValidIssuers = p.ValidIssuers,
                ValidAudiences = p.ValidAudiences,
                ValidationKeys = p.ValidationKeys,
            }
        );

        var result = await Validate(host, Hex(CwtVectors.SignedCwt), parameters);

        Assert.Equal(CoseFailureCodes.AlgorithmNotPermitted, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_UntaggedMessage_RequiresTheMessageType()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);
        var untagged = Hex(CwtVectors.SignedCwt)[1..];

        var refused = await Validate(host, untagged, Parameters(host));
        var accepted = await Validate(
            host,
            untagged,
            Parameters(
                host,
                p => new CwtValidationParameters
                {
                    ValidAlgorithms = p.ValidAlgorithms,
                    ValidIssuers = p.ValidIssuers,
                    ValidAudiences = p.ValidAudiences,
                    ValidationKeys = p.ValidationKeys,
                    UntaggedMessageType = CoseMessageType.Sign1,
                }
            )
        );

        Assert.Equal(CoseFailureCodes.UntaggedMessage, refused.FailureCode);
        Assert.True(accepted.IsValid, accepted.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_AnotherIssuer_ReportsInvalidIssuer()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);
        var parameters = Parameters(
            host,
            p => new CwtValidationParameters
            {
                ValidAlgorithms = p.ValidAlgorithms,
                ValidIssuers = ["coap://other.example.com"],
                ValidAudiences = p.ValidAudiences,
                ValidationKeys = p.ValidationKeys,
            }
        );

        var result = await Validate(host, Hex(CwtVectors.SignedCwt), parameters);

        Assert.Equal(CwtValidationFailureCodes.InvalidIssuer, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_TokenWithAudienceAndNoneNamed_ReportsInvalidAudience()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);
        var parameters = Parameters(
            host,
            p => new CwtValidationParameters
            {
                ValidAlgorithms = p.ValidAlgorithms,
                ValidIssuers = p.ValidIssuers,
                ValidationKeys = p.ValidationKeys,
            }
        );

        var result = await Validate(host, Hex(CwtVectors.SignedCwt), parameters);

        Assert.Equal(CwtValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_TokenWithoutAudienceAndAudiencesNamed_ReportsInvalidAudience()
    {
        await using var host = new CwtTestHost();
        var issuance = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            Issuer = Issuer,
            ExpiresAt = CwtTestHost.StartTime + TimeSpan.FromHours(1),
        };
        var token = await host.Issuer.IssueAsync(issuance, CancellationToken.None);

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_TokenWithoutAudienceAndNoneNamed_IsValid()
    {
        await using var host = new CwtTestHost();
        var token = await host.Issuer.IssueAsync(
            new CwtIssuanceParameters
            {
                Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
                Issuer = Issuer,
                ExpiresAt = CwtTestHost.StartTime + TimeSpan.FromHours(1),
            },
            CancellationToken.None
        );
        var parameters = new CwtValidationParameters
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
            ValidIssuers = [Issuer],
            ValidationKeys = [host.EcPublicKey()],
        };

        var result = await Validate(host, token, parameters);

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_TokenWithoutExpiration_ReportsInvalidLifetime()
    {
        await using var host = new CwtTestHost();
        var token = await IssueWithoutExpirationAsync(host);

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_TokenWithoutExpirationWhenNotRequired_IsValid()
    {
        await using var host = new CwtTestHost();
        var token = await IssueWithoutExpirationAsync(host);

        var result = await Validate(
            host,
            token,
            new CwtValidationParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
                ValidIssuers = [Issuer],
                ValidAudiences = [Audience],
                ValidationKeys = [host.EcPublicKey()],
                RequireExpirationTime = false,
            }
        );

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_LifetimeBeyondTheMaximum_ReportsInvalidLifetime()
    {
        await using var host = new CwtTestHost();
        var token = await host.Issuer.IssueAsync(
            Issuance(host, CwtTestHost.StartTime),
            CancellationToken.None
        );

        var result = await Validate(
            host,
            token,
            Parameters(
                host,
                p => new CwtValidationParameters
                {
                    ValidAlgorithms = p.ValidAlgorithms,
                    ValidIssuers = p.ValidIssuers,
                    ValidAudiences = p.ValidAudiences,
                    ValidationKeys = p.ValidationKeys,
                    MaxLifetime = TimeSpan.FromMinutes(30),
                }
            )
        );

        Assert.Equal(CwtValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_RequiredNotBeforeMissing_ReportsInvalidLifetime()
    {
        await using var host = new CwtTestHost();
        var token = await host.Issuer.IssueAsync(
            Issuance(host, CwtTestHost.StartTime),
            CancellationToken.None
        );

        var result = await Validate(host, token, Lifetime(host, requireNotBefore: true));

        Assert.Equal(CwtValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_RequiredIssuedAtMissing_ReportsInvalidLifetime()
    {
        await using var host = new CwtTestHost();
        var now = CwtTestHost.StartTime;
        var token = await SignClaimsAsync(
            host,
            new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.Issuer] = CoseValue.FromTextString(Issuer),
                [CwtClaimKeys.Audience] = CoseValue.FromTextString(Audience),
                [CwtClaimKeys.ExpirationTime] = NumericDate(now + TimeSpan.FromHours(1)),
            }
        );

        var result = await Validate(host, token, Lifetime(host, requireIssuedAt: true));

        Assert.Equal(CwtValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_NotBeforeAfterExpiration_ReportsInvalidLifetime()
    {
        await using var host = new CwtTestHost();
        var now = CwtTestHost.StartTime;
        var token = await SignClaimsAsync(
            host,
            new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.Issuer] = CoseValue.FromTextString(Issuer),
                [CwtClaimKeys.Audience] = CoseValue.FromTextString(Audience),
                [CwtClaimKeys.IssuedAt] = NumericDate(now),
                [CwtClaimKeys.NotBefore] = NumericDate(now + TimeSpan.FromHours(2)),
                [CwtClaimKeys.ExpirationTime] = NumericDate(now + TimeSpan.FromHours(1)),
            }
        );

        var result = await Validate(host, token, Lifetime(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidLifetime, result.FailureCode);
    }

    [Theory]
    [InlineData(4, true, null)]
    [InlineData(6, false, CwtValidationFailureCodes.NotYetValid)]
    public async Task ValidateAsync_NotBeforeAgainstTheClockSkew_AppliesTheSkew(
        int minutes,
        bool expectedValid,
        string? expectedCode
    )
    {
        await using var host = new CwtTestHost();
        var now = CwtTestHost.StartTime;
        var token = await IssueLifetimeAsync(
            host,
            now + TimeSpan.FromMinutes(minutes),
            now,
            now + TimeSpan.FromHours(1)
        );

        var result = await Validate(host, token, Lifetime(host));

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(expectedCode, result.FailureCode);
    }

    [Theory]
    [InlineData(4, true, null)]
    [InlineData(6, false, CwtValidationFailureCodes.NotYetValid)]
    public async Task ValidateAsync_IssuedAtAgainstTheClockSkew_AppliesTheSkew(
        int minutes,
        bool expectedValid,
        string? expectedCode
    )
    {
        await using var host = new CwtTestHost();
        var now = CwtTestHost.StartTime;
        var token = await IssueLifetimeAsync(
            host,
            null,
            now + TimeSpan.FromMinutes(minutes),
            now + TimeSpan.FromHours(1)
        );

        var result = await Validate(host, token, Lifetime(host));

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(expectedCode, result.FailureCode);
    }

    [Theory]
    [InlineData(20, false, CwtValidationFailureCodes.InvalidLifetime)]
    [InlineData(10, true, null)]
    public async Task ValidateAsync_MaxLifetimeWithAnEarlierNotBefore_IsMeasuredFromTheNotBefore(
        int notBeforeMinutesAgo,
        bool expectedValid,
        string? expectedCode
    )
    {
        await using var host = new CwtTestHost();
        var now = CwtTestHost.StartTime;
        var token = await IssueLifetimeAsync(
            host,
            now - TimeSpan.FromMinutes(notBeforeMinutesAgo),
            now,
            now + TimeSpan.FromMinutes(15)
        );

        var result = await Validate(
            host,
            token,
            Lifetime(host, maxLifetime: TimeSpan.FromMinutes(30))
        );

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(expectedCode, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_ClockSkewAtTheMaximum_AcceptsAnIssuedToken()
    {
        await using var host = new CwtTestHost();
        var token = await host.Issuer.IssueAsync(
            Issuance(host, CwtTestHost.StartTime),
            CancellationToken.None
        );

        var result = await Validate(host, token, Lifetime(host, clockSkew: TimeSpan.MaxValue));

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_RequiredClaimMissing_ReportsMissingClaim()
    {
        await using var host = new CwtTestHost();
        var token = await host.Issuer.IssueAsync(
            Issuance(host, CwtTestHost.StartTime),
            CancellationToken.None
        );

        var result = await Validate(
            host,
            token,
            Parameters(
                host,
                p => new CwtValidationParameters
                {
                    ValidAlgorithms = p.ValidAlgorithms,
                    ValidIssuers = p.ValidIssuers,
                    ValidAudiences = p.ValidAudiences,
                    ValidationKeys = p.ValidationKeys,
                    RequiredClaims = [CwtClaimKeys.Scope],
                }
            )
        );

        Assert.Equal(CwtValidationFailureCodes.MissingClaim, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_DeclaredTokenType_IsValid()
    {
        await using var host = new CwtTestHost();
        var token = await IssueTypedAsync(host, typed: true);

        var result = await Validate(host, token, ExpectingType(host, "application/example+cwt"));

        Assert.True(result.IsValid, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_OtherTokenType_ReportsInvalidTokenType()
    {
        await using var host = new CwtTestHost();
        var token = await IssueTypedAsync(host, typed: true);

        var result = await Validate(host, token, ExpectingType(host, "other"));

        Assert.Equal(CwtValidationFailureCodes.InvalidTokenType, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_UndeclaredTokenType_ReportsInvalidTokenType()
    {
        await using var host = new CwtTestHost();
        var token = await IssueTypedAsync(host, typed: false);

        var result = await Validate(host, token, ExpectingType(host, "application/example+cwt"));

        Assert.Equal(CwtValidationFailureCodes.InvalidTokenType, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_EncryptedSignedToken_IsValidWithDecryptionKeys()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var issuance = Issuance(host, CwtTestHost.StartTime);
        var token = await host.Issuer.IssueAsync(
            new CwtIssuanceParameters
            {
                Credentials = issuance.Credentials,
                Issuer = Issuer,
                Audiences = [Audience],
                ExpiresAt = issuance.ExpiresAt,
                EncryptionCredentials = new CoseCredentials(key, CoseAlgorithms.A128Gcm),
            },
            CancellationToken.None
        );

        var accepted = await Validate(
            host,
            token,
            Parameters(
                host,
                p => new CwtValidationParameters
                {
                    ValidAlgorithms = p.ValidAlgorithms,
                    ValidIssuers = p.ValidIssuers,
                    ValidAudiences = p.ValidAudiences,
                    ValidationKeys = p.ValidationKeys,
                    DecryptionKeys = [key],
                    ValidContentEncryptionAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                }
            )
        );

        Assert.True(accepted.IsValid, accepted.FailureReason);
        Assert.NotNull(accepted.Decryption);
    }

    [Fact]
    public async Task ValidateAsync_EncryptedTokenWithoutDecryptionKeys_ReportsInvalidToken()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var issuance = Issuance(host, CwtTestHost.StartTime);
        var token = await host.Issuer.IssueAsync(
            new CwtIssuanceParameters
            {
                Credentials = issuance.Credentials,
                Issuer = Issuer,
                Audiences = [Audience],
                ExpiresAt = issuance.ExpiresAt,
                EncryptionCredentials = new CoseCredentials(key, CoseAlgorithms.A128Gcm),
            },
            CancellationToken.None
        );

        var refused = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidToken, refused.FailureCode);
        Assert.Equal(
            "An encrypted CWT is not accepted because no decryption keys were provided.",
            refused.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_EncryptionOverTheClaimsSetAlone_ReportsInvalidToken()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var claims = host.Serializer.EncodeMap(
            new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.Issuer] = CoseValue.FromTextString(Issuer),
            }
        );
        var token = host.Serializer.EncodeEncrypt0(
            claims,
            new CoseCredentials(key, CoseAlgorithms.A128Gcm),
            new CoseEncodingOptions()
        );

        var result = await Validate(
            host,
            token,
            Parameters(
                host,
                p => new CwtValidationParameters
                {
                    ValidAlgorithms = p.ValidAlgorithms,
                    ValidIssuers = p.ValidIssuers,
                    ValidAudiences = p.ValidAudiences,
                    ValidationKeys = p.ValidationKeys,
                    DecryptionKeys = [key],
                    ValidContentEncryptionAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                }
            )
        );

        Assert.Equal(CwtValidationFailureCodes.InvalidToken, result.FailureCode);
        Assert.Equal(
            "The encrypted CWT does not enclose a signed or authenticated message.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_EncryptionOverAnEncryptedMessage_ReportsInvalidToken()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var claims = host.Serializer.EncodeMap(
            new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.Issuer] = CoseValue.FromTextString(Issuer),
            }
        );
        var credentials = new CoseCredentials(key, CoseAlgorithms.A128Gcm);
        var inner = host.Serializer.EncodeEncrypt0(claims, credentials, new CoseEncodingOptions());
        var token = host.Serializer.EncodeEncrypt0(inner, credentials, new CoseEncodingOptions());

        var result = await Validate(
            host,
            token,
            Parameters(
                host,
                p => new CwtValidationParameters
                {
                    ValidAlgorithms = p.ValidAlgorithms,
                    ValidIssuers = p.ValidIssuers,
                    ValidAudiences = p.ValidAudiences,
                    ValidationKeys = p.ValidationKeys,
                    DecryptionKeys = [key],
                    ValidContentEncryptionAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                }
            )
        );

        Assert.Equal(CwtValidationFailureCodes.InvalidToken, result.FailureCode);
        Assert.Equal(
            "The encrypted CWT does not enclose a signed or authenticated message.",
            result.FailureReason
        );
    }

    [Fact]
    public async Task ValidateAsync_IssuerClaimOfTheWrongForm_ReportsInvalidClaim()
    {
        await using var host = new CwtTestHost();
        var claims = host.Serializer.EncodeMap(
            new Dictionary<CoseLabel, CoseValue> { [CwtClaimKeys.Issuer] = CoseValue.FromInt64(1) }
        );
        var token = await host.Serializer.EncodeSign1Async(
            claims,
            new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidClaim, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_ContentThatIsNotAMap_ReportsInvalidToken()
    {
        await using var host = new CwtTestHost();
        var token = await host.Serializer.EncodeSign1Async(
            Hex("01"),
            new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidToken, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_ResolverDelegate_ReceivesTheInspectedMessage()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);
        CoseMessageInfo? received = null;
        var parameters = new CwtValidationParameters
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
            ValidIssuers = [Issuer],
            ValidAudiences = [Audience],
            ResolveValidationKeysAsync = (message, _) =>
            {
                received = message;
                return ValueTask.FromResult<IReadOnlyCollection<SecretKey>>([host.EcPublicKey()]);
            },
        };

        var result = await Validate(host, Hex(CwtVectors.SignedCwt), parameters);

        Assert.True(result.IsValid, result.FailureReason);
        Assert.NotNull(received);
        Assert.Equal(CoseMessageType.Sign1, received.MessageType);
    }

    [Fact]
    public async Task ValidateAsync_WrongKey_ReportsInvalidSignature()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);
        var other = host.KeyFactory.CreateEcc(
            new KeyMetadata(),
            System.Security.Cryptography.ECDsa.Create(
                System.Security.Cryptography.ECCurve.NamedCurves.nistP256
            )
        );

        var result = await Validate(
            host,
            Hex(CwtVectors.SignedCwt),
            Parameters(
                host,
                p => new CwtValidationParameters
                {
                    ValidAlgorithms = p.ValidAlgorithms,
                    ValidIssuers = p.ValidIssuers,
                    ValidAudiences = p.ValidAudiences,
                    ValidationKeys = [other],
                }
            )
        );

        Assert.Equal(CoseFailureCodes.InvalidSignature, result.FailureCode);
        Assert.DoesNotContain(CwtVectors.SignedCwt[..40], result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_MalformedToken_FailsWithoutCarryingTheToken()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);

        var result = await Validate(host, Hex("D2FF"), Parameters(host));

        Assert.False(result.IsValid);
        Assert.Equal(CoseFailureCodes.InvalidMessage, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_InvalidParameters_ThrowNamingTheRule()
    {
        await using var host = new CwtTestHost();
        var token = Hex(CwtVectors.SignedCwt);
        var valid = Parameters(host);

        CwtValidationParameters With(
            IReadOnlyCollection<int>? algorithms = null,
            IReadOnlyCollection<string>? issuers = null,
            bool keys = true,
            TimeSpan? skew = null,
            TimeSpan? maxLifetime = null,
            IReadOnlyCollection<string>? audiences = null,
            IReadOnlyCollection<SecretKey>? decryptionKeys = null
        ) =>
            new()
            {
                ValidAlgorithms = algorithms ?? valid.ValidAlgorithms,
                ValidIssuers = issuers ?? valid.ValidIssuers,
                ValidationKeys = keys ? valid.ValidationKeys : null,
                ClockSkew = skew ?? valid.ClockSkew,
                MaxLifetime = maxLifetime,
                ValidAudiences = audiences ?? valid.ValidAudiences,
                DecryptionKeys = decryptionKeys,
            };

        var invalid = new (CwtValidationParameters Parameters, string Expected)[]
        {
            (With(algorithms: []), "At least one valid algorithm"),
            (With(issuers: []), "At least one valid issuer"),
            (With(issuers: [""]), "At least one valid issuer"),
            (With(keys: false), "The validation keys"),
            (With(skew: TimeSpan.FromSeconds(-1)), "The clock skew must not be negative"),
            (With(maxLifetime: TimeSpan.Zero), "The maximum lifetime must be positive"),
            (With(audiences: []), "When valid audiences are given"),
            (With(decryptionKeys: [host.Aes()]), "When decryption keys are given"),
        };

        foreach (var (parameters, expected) in invalid)
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await host.Validator.ValidateAsync(token, parameters, CancellationToken.None)
            );
            Assert.StartsWith(expected, exception.Message);
        }
    }

    [Fact]
    public async Task ValidateAsync_CanceledToken_Throws()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await host.Validator.ValidateAsync(
                Hex(CwtVectors.SignedCwt),
                Parameters(host),
                new CancellationToken(canceled: true)
            )
        );
    }

    [Fact]
    public async Task ValidateAsync_TruncatedTag_ReportsInvalidMessage()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);

        var result = await Validate(host, Hex("D8"), Parameters(host));

        Assert.Equal(CoseFailureCodes.InvalidMessage, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_ResolverReturningNull_ReportsKeyNotFound()
    {
        await using var host = new CwtTestHost(CwtVectors.RfcTime);
        var parameters = new CwtValidationParameters
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
            ValidIssuers = [Issuer],
            ValidAudiences = [Audience],
            ResolveValidationKeysAsync = (_, _) =>
                ValueTask.FromResult<IReadOnlyCollection<SecretKey>>(null!),
        };

        var result = await Validate(host, Hex(CwtVectors.SignedCwt), parameters);

        Assert.Equal(CoseFailureCodes.KeyNotFound, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_IntegerTokenType_ReportsInvalidTokenType()
    {
        await using var host = new CwtTestHost();
        var claims = host.Serializer.EncodeMap(
            new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.Issuer] = CoseValue.FromTextString(Issuer),
                [CwtClaimKeys.Audience] = CoseValue.FromTextString(Audience),
                [CwtClaimKeys.ExpirationTime] = NumericDate(
                    CwtTestHost.StartTime + TimeSpan.FromHours(1)
                ),
            }
        );
        var token = await host.Serializer.EncodeSign1Async(
            claims,
            new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            new CoseEncodingOptions
            {
                ProtectedHeaders = new Dictionary<CoseLabel, CoseValue>
                {
                    [CoseHeaderLabels.Type] = CoseValue.FromInt64(61),
                },
            },
            CancellationToken.None
        );

        var result = await Validate(host, token, ExpectingType(host, "application/example+cwt"));

        Assert.Equal(CwtValidationFailureCodes.InvalidTokenType, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_TokenWithoutIssuer_ReportsInvalidIssuer()
    {
        await using var host = new CwtTestHost();
        var token = await SignClaimsAsync(
            host,
            new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.Audience] = CoseValue.FromTextString(Audience),
                [CwtClaimKeys.ExpirationTime] = NumericDate(
                    CwtTestHost.StartTime + TimeSpan.FromHours(1)
                ),
            }
        );

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidIssuer, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_OtherAudience_ReportsInvalidAudience()
    {
        await using var host = new CwtTestHost();
        var issuance = Issuance(host, CwtTestHost.StartTime);
        var token = await host.Issuer.IssueAsync(
            new CwtIssuanceParameters
            {
                Credentials = issuance.Credentials,
                Issuer = Issuer,
                Audiences = ["coap://other.example.com"],
                ExpiresAt = issuance.ExpiresAt,
            },
            CancellationToken.None
        );

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Theory]
    [InlineData(10, true, null)]
    [InlineData(60, false, CwtValidationFailureCodes.InvalidLifetime)]
    public async Task ValidateAsync_MaxLifetimeWithoutAStart_IsMeasuredFromTheLatestStart(
        int expiresInMinutes,
        bool expectedValid,
        string? expectedCode
    )
    {
        await using var host = new CwtTestHost();
        var token = await SignClaimsAsync(
            host,
            new Dictionary<CoseLabel, CoseValue>
            {
                [CwtClaimKeys.Issuer] = CoseValue.FromTextString(Issuer),
                [CwtClaimKeys.Audience] = CoseValue.FromTextString(Audience),
                [CwtClaimKeys.ExpirationTime] = NumericDate(
                    CwtTestHost.StartTime + TimeSpan.FromMinutes(expiresInMinutes)
                ),
            }
        );

        var result = await Validate(
            host,
            token,
            Lifetime(host, maxLifetime: TimeSpan.FromMinutes(30))
        );

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(expectedCode, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_EncryptedWithRecipients_IsValidWithDecryptionKeys()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var issuance = Issuance(host, CwtTestHost.StartTime);
        var token = await host.Issuer.IssueAsync(
            new CwtIssuanceParameters
            {
                Credentials = issuance.Credentials,
                Issuer = Issuer,
                Audiences = [Audience],
                ExpiresAt = issuance.ExpiresAt,
                EncryptionRecipients = new CoseRecipients(
                    CoseAlgorithms.A128Gcm,
                    [new CoseRecipient(new CoseCredentials(key, CoseAlgorithms.A128Kw))]
                ),
            },
            CancellationToken.None
        );

        var result = await Validate(
            host,
            token,
            Parameters(
                host,
                p => new CwtValidationParameters
                {
                    ValidAlgorithms = p.ValidAlgorithms,
                    ValidIssuers = p.ValidIssuers,
                    ValidAudiences = p.ValidAudiences,
                    ValidationKeys = p.ValidationKeys,
                    DecryptionKeys = [key],
                    ValidContentEncryptionAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                    ValidKeyDistributionAlgorithms = [CoseAlgorithmIdentifiers.A128Kw],
                }
            )
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal(CoseMessageType.Encrypt, result.Decryption?.MessageType);
    }

    #endregion
}
