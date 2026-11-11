// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Options;
using NCode.Identity.Jose.SdJwt.Processing;
using NCode.Identity.Json;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// Provides the default implementation of <see cref="ISdJwtVerifier"/>. The issuer-signed JWT and the Key Binding JWT
/// are validated by the JWT validator, so their signature, algorithm and key policy is the family's one policy; the
/// issuer-signed JWT must be a compact JWS, so a Nested JWT is refused whatever decryption keys the caller's
/// parameters name; the disclosures are resolved under the rules of RFC 9901 §7.1; and the Key Binding JWT's
/// <c>nonce</c>, <c>sd_hash</c> and <c>iat</c> are checked here, against the presentation as received. A failure
/// reason never carries the token: a JWT the validator could not decode is reported by its failure code and fixed
/// text. The confirmation-key resolvers are validated as a whole when the verifier is composed: each <c>cnf</c>
/// member has exactly one resolver.
/// </summary>
// See ADR-1405.
internal class DefaultSdJwtVerifier : ISdJwtVerifier
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSdJwtVerifier"/> class.
    /// </summary>
    /// <param name="jwtValidator">The JWT validator that verifies the issuer-signed JWT and the Key Binding JWT.</param>
    /// <param name="digestAlgorithmCatalog">The registered hash algorithms.</param>
    /// <param name="confirmationKeyResolvers">The registered resolvers of the holder key from the <c>cnf</c> claim,
    /// consulted in registration order.</param>
    /// <param name="timeProvider">The clock the Key Binding JWT's freshness is judged by.</param>
    /// <param name="optionsAccessor">The accessor of the limits.</param>
    /// <exception cref="InvalidOperationException">A resolver is <c>null</c>, names an empty or whitespace member, or
    /// names the member of another resolver.</exception>
    public DefaultSdJwtVerifier(
        IJwtValidator jwtValidator,
        ISdJwtDigestAlgorithmCatalog digestAlgorithmCatalog,
        IEnumerable<IConfirmationKeyResolver> confirmationKeyResolvers,
        TimeProvider timeProvider,
        IOptions<SdJwtOptions> optionsAccessor
    )
    {
        ArgumentNullException.ThrowIfNull(confirmationKeyResolvers);

        JwtValidator = jwtValidator;
        DigestAlgorithmCatalog = digestAlgorithmCatalog;
        ConfirmationKeyResolvers = ValidateResolvers(confirmationKeyResolvers);
        TimeProvider = timeProvider;
        Options = optionsAccessor.Value;
    }

    private IJwtValidator JwtValidator { get; }
    private ISdJwtDigestAlgorithmCatalog DigestAlgorithmCatalog { get; }
    private IConfirmationKeyResolver[] ConfirmationKeyResolvers { get; }
    private TimeProvider TimeProvider { get; }
    private SdJwtOptions Options { get; }

    private static IConfirmationKeyResolver[] ValidateResolvers(
        IEnumerable<IConfirmationKeyResolver> confirmationKeyResolvers
    )
    {
        var resolvers = new List<IConfirmationKeyResolver>();
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resolver in confirmationKeyResolvers)
        {
            if (resolver is null)
                throw new InvalidOperationException(
                    "A registered SD-JWT confirmation-key resolver is null."
                );

            if (string.IsNullOrWhiteSpace(resolver.Member))
                throw new InvalidOperationException(
                    "A registered SD-JWT confirmation-key resolver names an empty 'cnf' member."
                );

            if (!members.Add(resolver.Member))
                throw new InvalidOperationException(
                    $"The 'cnf' member '{resolver.Member}' has more than one registered SD-JWT confirmation-key resolver."
                );

            resolvers.Add(resolver);
        }

        return [.. resolvers];
    }

    /// <inheritdoc />
    public async ValueTask<SdJwtVerificationResult> VerifyAsync(
        string presentation,
        SdJwtVerificationParameters parameters,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(parameters);

        // The signature algorithms of the issuer-signed JWT are a trust boundary: an unset list permits nothing.
        if (parameters.IssuerJwtParameters.ValidSignatureAlgorithms is null)
            throw new ArgumentException(
                "The issuer JWT parameters must name the signature algorithms the issuer-signed JWT may use.",
                nameof(parameters)
            );

        if (parameters.KeyBinding is { } requirement)
            ValidateRequirement(requirement, nameof(parameters));

        try
        {
            return await VerifyCoreAsync(presentation, parameters, cancellationToken);
        }
        catch (SdJwtException exception)
        {
            return SdJwtVerificationResult.Fail(exception.FailureCode, exception.Message);
        }
    }

    // A requirement built from a missing value would let any captured Key Binding JWT replay, so each value fails
    // closed before the presentation is read.
    private static void ValidateRequirement(
        SdJwtKeyBindingRequirement requirement,
        string paramName
    )
    {
        if (string.IsNullOrEmpty(requirement.Audience))
            throw new ArgumentException(
                "The key binding requirement's audience must not be empty.",
                paramName
            );

        if (string.IsNullOrEmpty(requirement.Nonce))
            throw new ArgumentException(
                "The key binding requirement's nonce must not be empty.",
                paramName
            );

        if (requirement.ValidSignatureAlgorithms is null)
            throw new ArgumentException(
                "The key binding requirement must name the signature algorithms the Key Binding JWT may use.",
                paramName
            );

        if (requirement.MaxAge <= TimeSpan.Zero)
            throw new ArgumentException(
                "The key binding requirement's maximum age must be positive.",
                paramName
            );

        if (requirement.ClockSkew < TimeSpan.Zero)
            throw new ArgumentException(
                "The key binding requirement's clock skew must not be negative.",
                paramName
            );
    }

    private async ValueTask<SdJwtVerificationResult> VerifyCoreAsync(
        string presentation,
        SdJwtVerificationParameters parameters,
        CancellationToken cancellationToken
    )
    {
        var sdJwt = SdJwtSerialization.Parse(presentation, Options.MaxDisclosureCount);

        var issuerResult = await JwtValidator.ValidateAsync(
            sdJwt.IssuerSignedJwt,
            parameters.IssuerJwtParameters,
            cancellationToken
        );
        if (!issuerResult.IsValid)
            return SdJwtVerificationResult.Fail(
                issuerResult.FailureCode,
                DescribeFailure(
                    "The issuer-signed JWT",
                    issuerResult.FailureCode,
                    issuerResult.Exception
                )
            );

        var issuerJwt = issuerResult.DecodedJwt;
        if (issuerJwt.EncryptionHeader is not null)
            return SdJwtVerificationResult.Fail(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "The issuer-signed JWT must be a compact JWS."
            );

        if (issuerJwt.SigningKey is not AsymmetricSecretKey)
            return SdJwtVerificationResult.Fail(
                JwtValidationFailureCodes.AlgorithmNotPermitted,
                "The issuer-signed JWT must be signed with an asymmetric key."
            );

        var payload = issuerJwt.Payload;
        if (payload.ValueKind != JsonValueKind.Object)
            return SdJwtVerificationResult.Fail(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "The issuer-signed JWT payload must be a JSON object."
            );

        var profile = parameters.Profile;
        if (profile is not null)
        {
            if (
                !issuerJwt.Header.TryGetPropertyValue<string>(
                    JoseClaimNames.Header.Typ,
                    out var typ
                ) || !JoseTokenTypes.AreSame(typ, profile.TokenType)
            )
                return SdJwtVerificationResult.Fail(
                    JwtValidationFailureCodes.InvalidTokenType,
                    $"The issuer-signed JWT 'typ' header must be '{profile.TokenType}' under the '{profile.Name}' profile."
                );

            foreach (var requiredClaim in profile.RequiredClaims)
            {
                if (
                    !payload.TryGetPropertyValue<string>(requiredClaim, out var value)
                    || string.IsNullOrEmpty(value)
                )
                    return SdJwtVerificationResult.Fail(
                        SdJwtVerificationFailureCodes.MissingRequiredClaim,
                        $"The '{profile.Name}' profile requires the '{requiredClaim}' claim as a non-empty string."
                    );
            }
        }

        var algorithm = DigestAlgorithmCatalog.Resolve(payload);
        var processor = new SdJwtPayloadProcessor(algorithm, Options.MaxNestingDepth, profile);
        var processed = processor.Process(payload, sdJwt.Disclosures);

        SdJwtKeyBinding? keyBinding = null;
        if (parameters.KeyBinding is { } requirement)
        {
            var keyBindingResult = await VerifyKeyBindingAsync(
                presentation,
                sdJwt,
                payload,
                algorithm,
                requirement,
                cancellationToken
            );
            if (!keyBindingResult.IsValid)
                return keyBindingResult.Failure;

            keyBinding = keyBindingResult.KeyBinding;
        }

        var claims = JsonSerializer.SerializeToElement(processed.Claims);
        return SdJwtVerificationResult.Success(
            sdJwt,
            issuerJwt,
            claims,
            processed.Disclosed,
            keyBinding
        );
    }

    // The JWT validator catches every exception, cancellation included, and a decode failure's message carries the
    // token, so cancellation is rethrown and a decode failure is reported by its code and fixed text.
    private static string DescribeFailure(string tokenName, string failureCode, Exception exception)
    {
        if (exception is OperationCanceledException)
            ExceptionDispatchInfo.Capture(exception).Throw();

        var detail = exception is JwtDecodeException or JwtSecretKeyNotFoundException
            ? "The token could not be decoded with the keys the parameters name."
            : exception.Message;
        return $"{tokenName} is invalid ({failureCode}): {detail}";
    }

    private async ValueTask<KeyBindingOutcome> VerifyKeyBindingAsync(
        string presentation,
        SelectiveDisclosureJwt sdJwt,
        JsonElement payload,
        SdJwtDigestAlgorithm algorithm,
        SdJwtKeyBindingRequirement requirement,
        CancellationToken cancellationToken
    )
    {
        if (!sdJwt.HasKeyBinding)
            return KeyBindingOutcome.Fail(
                SdJwtVerificationFailureCodes.KeyBindingRequired,
                "The presentation carries no Key Binding JWT."
            );

        // The holder key is read from the issuer-signed payload before any disclosure is resolved into it, so a
        // disclosure can never supply it (RFC 9901 §4.3).
        var holderKey = await ResolveHolderKeyAsync(payload, cancellationToken);
        if (holderKey is null)
            return KeyBindingOutcome.Fail(
                SdJwtVerificationFailureCodes.HolderKeyNotFound,
                "The issuer-signed JWT carries no 'cnf' claim that names a usable holder key."
            );

        var keyBindingParameters = new JwtValidationParameters
        {
            ValidationKeys = [holderKey],
            ValidSignatureAlgorithms = requirement.ValidSignatureAlgorithms,
            ClockSkew = requirement.ClockSkew,
        }
            .ValidateTokenType(JoseTokenTypes.KeyBindingJwt)
            .ValidateAudience(requirement.Audience);

        var result = await JwtValidator.ValidateAsync(
            sdJwt.KeyBindingJwt,
            keyBindingParameters,
            cancellationToken
        );
        if (!result.IsValid)
            return KeyBindingOutcome.Fail(
                SdJwtVerificationFailureCodes.InvalidKeyBinding,
                DescribeFailure("The Key Binding JWT", result.FailureCode, result.Exception)
            );

        var keyBindingJwt = result.DecodedJwt;
        var claims = keyBindingJwt.Payload;
        if (claims.ValueKind != JsonValueKind.Object)
            return KeyBindingOutcome.Fail(
                SdJwtVerificationFailureCodes.InvalidKeyBinding,
                "The Key Binding JWT payload must be a JSON object."
            );

        if (
            !claims.TryGetPropertyValue<string>(JoseClaimNames.Payload.Nonce, out var nonce)
            || !AreEqual(nonce, requirement.Nonce)
        )
            return KeyBindingOutcome.Fail(
                SdJwtVerificationFailureCodes.InvalidNonce,
                "The Key Binding JWT 'nonce' claim is not the expected nonce."
            );

        // sd_hash covers the presentation exactly as received, up to and including the last '~' (RFC 9901 §4.3.1).
        var expectedSdHash = algorithm.ComputeDigest(
            SdJwtSerialization.GetKeyBindingInput(presentation)
        );
        if (
            !claims.TryGetPropertyValue<string>(JoseClaimNames.Payload.SdHash, out var sdHash)
            || !AreEqual(sdHash, expectedSdHash)
        )
            return KeyBindingOutcome.Fail(
                SdJwtVerificationFailureCodes.InvalidSdHash,
                "The Key Binding JWT 'sd_hash' claim is not the digest of the presentation."
            );

        if (!claims.TryGetPropertyNumericDate(JoseClaimNames.Payload.Iat, out var issuedAt))
            return KeyBindingOutcome.Fail(
                SdJwtVerificationFailureCodes.KeyBindingNotFresh,
                "The Key Binding JWT is missing a valid 'iat' claim."
            );

        // Elapsed time is compared and the skew subtracted from it, never added to the age, so a maximum age at the
        // end of the TimeSpan range cannot overflow.
        var now = TimeProvider.GetUtcNow();
        if (
            issuedAt - now > requirement.ClockSkew
            || now - issuedAt - requirement.ClockSkew > requirement.MaxAge
        )
            return KeyBindingOutcome.Fail(
                SdJwtVerificationFailureCodes.KeyBindingNotFresh,
                "The Key Binding JWT 'iat' claim is in the future or older than the maximum age."
            );

        return KeyBindingOutcome.Succeed(
            new SdJwtKeyBinding
            {
                Jwt = keyBindingJwt,
                HolderKey = holderKey,
                Nonce = nonce,
                IssuedAt = issuedAt,
            }
        );
    }

    // A nonce and a digest are guard values, compared in fixed time.
    private static bool AreEqual(string presented, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(presented.AsSpan()),
            MemoryMarshal.AsBytes(expected.AsSpan())
        );

    private async ValueTask<SecretKey?> ResolveHolderKeyAsync(
        JsonElement payload,
        CancellationToken cancellationToken
    )
    {
        if (
            !payload.TryGetPropertyValue<JsonElement>(
                JoseClaimNames.Payload.Cnf,
                out var confirmation
            )
            || confirmation.ValueKind != JsonValueKind.Object
        )
            return null;

        var count = ConfirmationKeyResolvers.Length;
        for (var i = 0; i < count; i++)
        {
            var resolver = ConfirmationKeyResolvers[i];
            if (!confirmation.TryGetProperty(resolver.Member, out _))
                continue;

            var holderKey = await resolver.ResolveAsync(confirmation, cancellationToken);
            if (holderKey is not null)
                return holderKey;
        }

        return null;
    }
}
