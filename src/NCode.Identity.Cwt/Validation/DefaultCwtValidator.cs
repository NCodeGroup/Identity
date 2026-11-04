// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Cbor;
using NCode.Identity.Cose;
using NCode.Identity.Cwt.Claims;
using NCode.Identity.Secrets.Keys;
using NCode.Time;

namespace NCode.Identity.Cwt.Validation;

/// <summary>
/// Provides the default implementation of <see cref="ICwtValidator"/>. The validation order is fixed: the parameters,
/// the CWT tag, the optional encryption layer, the keys, the COSE verification, the token type and only then the
/// claims, which are read from verified content alone.
/// </summary>
// See ADR-1410.
internal class DefaultCwtValidator(ICoseSerializer serializer, TimeProvider timeProvider)
    : ICwtValidator
{
    private ICoseSerializer Serializer { get; } = serializer;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public async ValueTask<CwtValidationResult> ValidateAsync(
        ReadOnlyMemory<byte> token,
        CwtValidationParameters parameters,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckParameters(parameters);

        try
        {
            return await ValidateCoreAsync(token, parameters, cancellationToken);
        }
        catch (CoseException exception)
        {
            return CwtValidationResult.Fail(exception.FailureCode, exception.Message);
        }
    }

    private async ValueTask<CwtValidationResult> ValidateCoreAsync(
        ReadOnlyMemory<byte> token,
        CwtValidationParameters parameters,
        CancellationToken cancellationToken
    )
    {
        var message = RemoveCwtTag(token);
        var info = Serializer.Inspect(message, parameters.UntaggedMessageType);

        CoseVerifiedMessage? decryption = null;
        var isEncrypted = info.MessageType is CoseMessageType.Encrypt0 or CoseMessageType.Encrypt;
        if (isEncrypted)
        {
            if (parameters.DecryptionKeys is not { } decryptionKeys)
                return CwtValidationResult.Fail(
                    CwtValidationFailureCodes.InvalidToken,
                    "An encrypted CWT is not accepted because no decryption keys were provided."
                );

            decryption = Serializer.Decrypt(
                message,
                new CoseDecryptionParameters
                {
                    ValidAlgorithms = parameters.ValidContentEncryptionAlgorithms,
                    Keys = decryptionKeys,
                    ValidKeyDistributionAlgorithms = parameters.ValidKeyDistributionAlgorithms,
                    UnderstoodCriticalHeaders = parameters.UnderstoodCriticalHeaders,
                    UntaggedMessageType = parameters.UntaggedMessageType,
                    ClockSkew = parameters.ClockSkew,
                }
            );
            message = decryption.Content;

            // The enclosed message must authenticate the issuer: an encryption layer does not.
            try
            {
                info = Serializer.Inspect(message, null);
            }
            catch (CoseException)
            {
                return CwtValidationResult.Fail(
                    CwtValidationFailureCodes.InvalidToken,
                    "The encrypted CWT does not enclose a signed or authenticated message."
                );
            }

            if (info.MessageType is CoseMessageType.Encrypt0 or CoseMessageType.Encrypt)
                return CwtValidationResult.Fail(
                    CwtValidationFailureCodes.InvalidToken,
                    "The encrypted CWT does not enclose a signed or authenticated message."
                );
        }

        var keys =
            parameters.ValidationKeys
            ?? await ResolveKeysAsync(parameters, info, cancellationToken);

        var understood = parameters.UnderstoodCriticalHeaders;
        if (parameters.TokenType is not null)
            understood = [.. understood, new CoseLabel(CoseHeaderLabels.Type)];

        var verified = Serializer.Verify(
            message,
            new CoseVerificationParameters
            {
                ValidAlgorithms = parameters.ValidAlgorithms,
                Keys = keys,
                ValidKeyDistributionAlgorithms = parameters.ValidKeyDistributionAlgorithms,
                ExternalAad = parameters.ExternalAad,
                UnderstoodCriticalHeaders = understood,
                ClockSkew = parameters.ClockSkew,
                UntaggedMessageType = isEncrypted ? null : parameters.UntaggedMessageType,
            }
        );

        if (
            parameters.TokenType is { } tokenType
            && !(
                verified.ProtectedHeaders.TryGetValue(CoseHeaderLabels.Type, out var typeValue)
                && typeValue.TryGetTextString(out var declaredType)
                && string.Equals(declaredType, tokenType, StringComparison.Ordinal)
            )
        )
            return CwtValidationResult.Fail(
                CwtValidationFailureCodes.InvalidTokenType,
                "The token does not declare the expected type."
            );

        IReadOnlyDictionary<CoseLabel, CoseValue> claimMap;
        try
        {
            claimMap = Serializer.DecodeMap(verified.Content);
        }
        catch (CoseException)
        {
            return CwtValidationResult.Fail(
                CwtValidationFailureCodes.InvalidToken,
                "The CWT claims set is not a valid CBOR map."
            );
        }

        var claims = CwtClaimsReader.Read(claimMap, Serializer);

        if (
            claims.Issuer is not { } issuer
            || !parameters.ValidIssuers.Contains(issuer, StringComparer.Ordinal)
        )
            return CwtValidationResult.Fail(
                CwtValidationFailureCodes.InvalidIssuer,
                "The issuer of the token is not accepted."
            );

        if (CheckAudience(claims, parameters) is { } audienceFailure)
            return audienceFailure;

        if (CheckLifetime(claims, parameters) is { } lifetimeFailure)
            return lifetimeFailure;

        foreach (var required in parameters.RequiredClaims)
        {
            if (!claims.Claims.ContainsKey(required))
                return CwtValidationResult.Fail(
                    CwtValidationFailureCodes.MissingClaim,
                    $"The token does not carry the required claim '{required}'."
                );
        }

        return CwtValidationResult.Success(claims, verified, decryption);
    }

    private static void CheckParameters(CwtValidationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.ValidAlgorithms);
        ArgumentNullException.ThrowIfNull(parameters.ValidIssuers);

        if (parameters.ValidAlgorithms.Count == 0)
            throw new ArgumentException(
                "At least one valid algorithm must be named.",
                nameof(parameters)
            );

        if (parameters.ValidIssuers.Count == 0 || parameters.ValidIssuers.Any(string.IsNullOrEmpty))
            throw new ArgumentException(
                "At least one valid issuer must be named, and none may be empty.",
                nameof(parameters)
            );

        if (parameters.ValidationKeys is null && parameters.ResolveValidationKeysAsync is null)
            throw new ArgumentException(
                "The validation keys, or a callback that resolves them, must be given.",
                nameof(parameters)
            );

        if (parameters.ClockSkew < TimeSpan.Zero)
            throw new ArgumentException("The clock skew must not be negative.", nameof(parameters));

        if (parameters.MaxLifetime is { } maxLifetime && maxLifetime <= TimeSpan.Zero)
            throw new ArgumentException(
                "The maximum lifetime must be positive.",
                nameof(parameters)
            );

        if (parameters.ValidAudiences is { Count: 0 })
            throw new ArgumentException(
                "When valid audiences are given, at least one must be named.",
                nameof(parameters)
            );

        if (
            parameters.DecryptionKeys is not null
            && (parameters.ValidContentEncryptionAlgorithms?.Count ?? 0) == 0
        )
            throw new ArgumentException(
                "When decryption keys are given, at least one valid content encryption algorithm must be named.",
                nameof(parameters)
            );
    }

    private static ReadOnlyMemory<byte> RemoveCwtTag(ReadOnlyMemory<byte> token)
    {
        try
        {
            var reader = new CborReader(token, CborConformanceMode.Strict);
            if (
                reader.PeekState() == CborReaderState.Tag
                && (ulong)reader.PeekTag() == CwtConstants.Tag
            )
            {
                reader.ReadTag();
                return token[^reader.BytesRemaining..];
            }

            return token;
        }
        catch (Exception exception)
            when (exception is CborContentException or InvalidOperationException)
        {
            throw new CoseException(
                CoseFailureCodes.InvalidMessage,
                "The token is not a well-formed CBOR data item.",
                exception
            );
        }
    }

    private static async ValueTask<IReadOnlyCollection<SecretKey>> ResolveKeysAsync(
        CwtValidationParameters parameters,
        CoseMessageInfo info,
        CancellationToken cancellationToken
    )
    {
        var resolver =
            parameters.ResolveValidationKeysAsync
            ?? throw new InvalidOperationException("No validation key source is available.");
        return await resolver(info, cancellationToken) ?? [];
    }

    private static CwtValidationResult? CheckAudience(
        CwtClaimsSet claims,
        CwtValidationParameters parameters
    )
    {
        if (parameters.ValidAudiences is not { } validAudiences)
        {
            // The validator identifies with no audience, so a token addressed to one is not meant for it
            // (RFC 8392 §3.1.3, RFC 7519 §4.1.3).
            return claims.Claims.ContainsKey(CwtClaimKeys.Audience)
                ? CwtValidationResult.Fail(
                    CwtValidationFailureCodes.InvalidAudience,
                    "The token names an audience and the validator names none."
                )
                : null;
        }

        return claims.Audiences.Any(audience =>
            validAudiences.Contains(audience, StringComparer.Ordinal)
        )
            ? null
            : CwtValidationResult.Fail(
                CwtValidationFailureCodes.InvalidAudience,
                "The audience of the token is not accepted."
            );
    }

    private CwtValidationResult? CheckLifetime(
        CwtClaimsSet claims,
        CwtValidationParameters parameters
    )
    {
        var notBefore = claims.NotBefore;
        var issuedAt = claims.IssuedAt;
        var expires = claims.ExpiresAt;

        if (notBefore is null && parameters.RequireNotBefore)
            return Lifetime(
                CwtValidationFailureCodes.InvalidLifetime,
                "The token is missing a not-before time."
            );

        if (issuedAt is null && parameters.RequireIssuedAt)
            return Lifetime(
                CwtValidationFailureCodes.InvalidLifetime,
                "The token is missing an issued-at time."
            );

        // A lifetime cap cannot hold for a token that never expires.
        if (
            expires is null
            && (parameters.RequireExpirationTime || parameters.MaxLifetime.HasValue)
        )
            return Lifetime(
                CwtValidationFailureCodes.InvalidLifetime,
                "The token is missing an expiration time."
            );

        if (notBefore > expires)
            return Lifetime(
                CwtValidationFailureCodes.InvalidLifetime,
                "The not-before time of the token is after its expiration time."
            );

        var nowTicks = TimeProvider.GetTimestampWithPrecisionInSeconds();
        var clockSkewTicks = parameters.ClockSkew.Ticks;
        var latestStartTicks = nowTicks + clockSkewTicks;

        if (notBefore?.UtcTicks > latestStartTicks)
            return Lifetime(CwtValidationFailureCodes.NotYetValid, "The token is not valid yet.");

        if (issuedAt?.UtcTicks > latestStartTicks)
            return Lifetime(
                CwtValidationFailureCodes.NotYetValid,
                "The token was issued in the future."
            );

        if (expires?.UtcTicks < nowTicks - clockSkewTicks)
            return Lifetime(CwtValidationFailureCodes.Expired, "The token has expired.");

        if (parameters.MaxLifetime is { } maxLifetime && expires is { } expiresAt)
        {
            // Measured from the earliest asserted start; a token asserting none starts at latestStartTicks.
            var startTicks = Math.Min(
                issuedAt?.UtcTicks ?? latestStartTicks,
                notBefore?.UtcTicks ?? latestStartTicks
            );
            if (expiresAt.UtcTicks - startTicks > maxLifetime.Ticks)
                return Lifetime(
                    CwtValidationFailureCodes.InvalidLifetime,
                    "The lifetime of the token exceeds the maximum."
                );
        }

        return null;
    }

    private static CwtValidationResult Lifetime(string code, string reason) =>
        CwtValidationResult.Fail(code, reason);
}
