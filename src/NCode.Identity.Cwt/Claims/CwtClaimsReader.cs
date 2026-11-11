// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose;
using NCode.Identity.Cwt.Validation;
using NCode.Identity.Json;

namespace NCode.Identity.Cwt.Claims;

/// <summary>
/// Reads the registered claims of a CBOR Web Token (CWT) into typed members, strictly: a registered claim of the wrong
/// form is refused rather than ignored.
/// </summary>
// See ADR-1410.
internal static class CwtClaimsReader
{
    private static CoseException Invalid(string claim) =>
        new(
            CwtValidationFailureCodes.InvalidClaim,
            $"The '{claim}' claim of the CWT has a form that is not allowed."
        );

    /// <summary>
    /// Reads the claims.
    /// </summary>
    /// <param name="claims">The claims as received.</param>
    /// <param name="serializer">The serializer that decodes the confirmation claim.</param>
    /// <returns>The claims set.</returns>
    /// <exception cref="CoseException">A registered claim has a form that is not allowed.</exception>
    public static CwtClaimsSet Read(
        IReadOnlyDictionary<CoseLabel, CoseValue> claims,
        ICoseSerializer serializer
    )
    {
        return new CwtClaimsSet
        {
            Claims = claims,
            Issuer = ReadText(claims, CwtClaimKeys.Issuer, "iss"),
            Subject = ReadText(claims, CwtClaimKeys.Subject, "sub"),
            Audiences = ReadAudiences(claims),
            ExpiresAt = ReadNumericDate(claims, CwtClaimKeys.ExpirationTime, "exp"),
            NotBefore = ReadNumericDate(claims, CwtClaimKeys.NotBefore, "nbf"),
            IssuedAt = ReadNumericDate(claims, CwtClaimKeys.IssuedAt, "iat"),
            CwtId = ReadCwtId(claims),
            ConfirmationKey = ReadConfirmationKey(claims, serializer),
        };
    }

    private static string? ReadText(
        IReadOnlyDictionary<CoseLabel, CoseValue> claims,
        long key,
        string name
    )
    {
        if (!claims.TryGetValue(key, out var value))
            return null;

        return value.TryGetTextString(out var text) ? text : throw Invalid(name);
    }

    private static List<string> ReadAudiences(IReadOnlyDictionary<CoseLabel, CoseValue> claims)
    {
        if (!claims.TryGetValue(CwtClaimKeys.Audience, out var value))
            return [];

        if (value.TryGetTextString(out var single))
            return [single];

        if (!value.TryGetArray(out var elements) || elements.Count == 0)
            throw Invalid("aud");

        var audiences = new List<string>(elements.Count);
        foreach (var element in elements)
        {
            if (!element.TryGetTextString(out var audience))
                throw Invalid("aud");

            audiences.Add(audience);
        }

        return audiences;
    }

    private static DateTimeOffset? ReadNumericDate(
        IReadOnlyDictionary<CoseLabel, CoseValue> claims,
        long key,
        string name
    )
    {
        if (!claims.TryGetValue(key, out var value))
            return null;

        if (
            value.TryGetInt64(out var seconds)
            && DateCoercion.TryFromNumericDate(seconds, out var instant)
        )
            return instant;

        if (
            value.TryGetDouble(out var fractional)
            && DateCoercion.TryFromNumericDate(fractional, out instant)
        )
            return instant;

        throw Invalid(name);
    }

    private static ReadOnlyMemory<byte>? ReadCwtId(IReadOnlyDictionary<CoseLabel, CoseValue> claims)
    {
        if (!claims.TryGetValue(CwtClaimKeys.CwtId, out var value))
            return null;

        return value.TryGetByteString(out var id) ? id : throw Invalid("cti");
    }

    private static ReadOnlyMemory<byte>? ReadConfirmationKey(
        IReadOnlyDictionary<CoseLabel, CoseValue> claims,
        ICoseSerializer serializer
    )
    {
        if (!claims.TryGetValue(CwtClaimKeys.Confirmation, out var value))
            return null;

        if (!value.IsMap)
            throw Invalid("cnf");

        IReadOnlyDictionary<CoseLabel, CoseValue> confirmation;
        try
        {
            confirmation = serializer.DecodeMap(value.EncodedValue);
        }
        catch (CoseException)
        {
            throw Invalid("cnf");
        }

        if (!confirmation.TryGetValue(CwtConfirmationKeys.CoseKey, out var key))
            return null;

        return key.IsMap ? key.EncodedValue : throw Invalid("cnf");
    }
}
