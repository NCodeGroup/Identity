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

    #region Working group examples

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

    #endregion

    #region Issuer, audience and lifetime

    [Fact]
    public async Task ValidateAsync_AnotherIssuer_ThrowsInvalidIssuer()
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
    public async Task ValidateAsync_TokenWithAudienceAndNoneNamed_ThrowsInvalidAudience()
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
    public async Task ValidateAsync_TokenWithoutAudienceAndAudiencesNamed_ThrowsInvalidAudience()
    {
        await using var host = new CwtTestHost();
        var issuance = new CwtIssuanceParameters
        {
            Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            Issuer = Issuer,
            ExpiresAt = CwtTestHost.StartTime + TimeSpan.FromHours(1),
        };
        var token = host.Issuer.Issue(issuance);

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidAudience, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_TokenWithoutAudienceAndNoneNamed_IsValid()
    {
        await using var host = new CwtTestHost();
        var token = host.Issuer.Issue(
            new CwtIssuanceParameters
            {
                Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
                Issuer = Issuer,
                ExpiresAt = CwtTestHost.StartTime + TimeSpan.FromHours(1),
            }
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
    public async Task ValidateAsync_TokenWithoutExpiration_ThrowsInvalidLifetimeUnlessNotRequired()
    {
        await using var host = new CwtTestHost();
        var token = host.Issuer.Issue(
            new CwtIssuanceParameters
            {
                Credentials = new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
                Issuer = Issuer,
                Audiences = [Audience],
            }
        );

        var refused = await Validate(host, token, Parameters(host));
        var accepted = await Validate(
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

        Assert.Equal(CwtValidationFailureCodes.InvalidLifetime, refused.FailureCode);
        Assert.True(accepted.IsValid, accepted.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_LifetimeBeyondTheMaximum_ThrowsInvalidLifetime()
    {
        await using var host = new CwtTestHost();
        var token = host.Issuer.Issue(Issuance(host, CwtTestHost.StartTime));

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
    public async Task ValidateAsync_RequiredClaimMissing_ThrowsMissingClaim()
    {
        await using var host = new CwtTestHost();
        var token = host.Issuer.Issue(Issuance(host, CwtTestHost.StartTime));

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

    #endregion

    #region Token type

    [Fact]
    public async Task ValidateAsync_TokenType_MustMatch()
    {
        await using var host = new CwtTestHost();
        var typed = Issuance(host, CwtTestHost.StartTime);
        var token = host.Issuer.Issue(
            new CwtIssuanceParameters
            {
                Credentials = typed.Credentials,
                Issuer = Issuer,
                Audiences = [Audience],
                ExpiresAt = typed.ExpiresAt,
                TokenType = "application/example+cwt",
            }
        );
        var untyped = host.Issuer.Issue(typed);

        CwtValidationParameters Expecting(string type) =>
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

        Assert.True((await Validate(host, token, Expecting("application/example+cwt"))).IsValid);
        Assert.Equal(
            CwtValidationFailureCodes.InvalidTokenType,
            (await Validate(host, token, Expecting("other"))).FailureCode
        );
        Assert.Equal(
            CwtValidationFailureCodes.InvalidTokenType,
            (await Validate(host, untyped, Expecting("application/example+cwt"))).FailureCode
        );
    }

    #endregion

    #region Encrypted tokens

    [Fact]
    public async Task ValidateAsync_EncryptedSignedToken_IsValidWithDecryptionKeys()
    {
        await using var host = new CwtTestHost();
        var key = host.Aes("enc");
        var issuance = Issuance(host, CwtTestHost.StartTime);
        var token = host.Issuer.Issue(
            new CwtIssuanceParameters
            {
                Credentials = issuance.Credentials,
                Issuer = Issuer,
                Audiences = [Audience],
                ExpiresAt = issuance.ExpiresAt,
                EncryptionCredentials = new CoseCredentials(key, CoseAlgorithms.A128Gcm),
            }
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
        var refused = await Validate(host, token, Parameters(host));

        Assert.True(accepted.IsValid, accepted.FailureReason);
        Assert.NotNull(accepted.Decryption);
        Assert.Equal(CwtValidationFailureCodes.InvalidToken, refused.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_EncryptionOverTheClaimsSetAlone_ThrowsInvalidToken()
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
    }

    #endregion

    #region Claims

    [Fact]
    public async Task ValidateAsync_IssuerClaimOfTheWrongForm_ThrowsInvalidClaim()
    {
        await using var host = new CwtTestHost();
        var claims = host.Serializer.EncodeMap(
            new Dictionary<CoseLabel, CoseValue> { [CwtClaimKeys.Issuer] = CoseValue.FromInt64(1) }
        );
        var token = host.Serializer.EncodeSign1(
            claims,
            new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            new CoseEncodingOptions()
        );

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidClaim, result.FailureCode);
    }

    [Fact]
    public async Task ValidateAsync_ContentThatIsNotAMap_ThrowsInvalidToken()
    {
        await using var host = new CwtTestHost();
        var token = host.Serializer.EncodeSign1(
            Hex("01"),
            new CoseCredentials(host.EcPrivateKey(), CoseAlgorithms.Es256),
            new CoseEncodingOptions()
        );

        var result = await Validate(host, token, Parameters(host));

        Assert.Equal(CwtValidationFailureCodes.InvalidToken, result.FailureCode);
    }

    #endregion

    #region Keys and failures

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
        Assert.Equal(CoseMessageType.Sign1, received!.MessageType);
    }

    [Fact]
    public async Task ValidateAsync_WrongKey_ThrowsInvalidSignature()
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

    #endregion

    #region Parameters

    [Fact]
    public async Task ValidateAsync_InvalidParameters_Throw()
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

        var invalid = new[]
        {
            With(algorithms: []),
            With(issuers: []),
            With(issuers: [""]),
            With(keys: false),
            With(skew: TimeSpan.FromSeconds(-1)),
            With(maxLifetime: TimeSpan.Zero),
            With(audiences: []),
            With(decryptionKeys: [host.Aes()]),
        };

        foreach (var parameters in invalid)
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await host.Validator.ValidateAsync(token, parameters, CancellationToken.None)
            );
        }
    }

    #endregion
}
