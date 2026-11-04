// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Keys;
using NCode.Identity.Cwt.Claims;

namespace NCode.Identity.Cwt.Issuance;

/// <summary>
/// Provides the default implementation of <see cref="ICwtIssuer"/>: the claims set is written in the deterministic
/// encoding and protected by a <c>COSE_Sign1</c> or <c>COSE_Mac0</c> message, which a <c>COSE_Encrypt0</c> or
/// <c>COSE_Encrypt</c> message may enclose.
/// </summary>
// See ADR-1410.
internal class DefaultCwtIssuer(
    ICoseSerializer serializer,
    ICoseKeyConverter keyConverter,
    TimeProvider timeProvider
) : ICwtIssuer
{
    private ICoseSerializer Serializer { get; } = serializer;
    private ICoseKeyConverter KeyConverter { get; } = keyConverter;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public byte[] Issue(CwtIssuanceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.Credentials);
        ArgumentNullException.ThrowIfNull(parameters.Audiences);
        ArgumentNullException.ThrowIfNull(parameters.AdditionalClaims);

        Validate(parameters);

        var claims = CwtClaimsWriter.Build(
            parameters,
            TimeProvider.GetUtcNow(),
            Serializer,
            KeyConverter
        );
        var claimsSet = Serializer.EncodeMap(claims);

        var options = new CoseEncodingOptions
        {
            ProtectedHeaders = parameters.TokenType is { } tokenType
                ? new Dictionary<CoseLabel, CoseValue>
                {
                    [CoseHeaderLabels.Type] = CoseValue.FromTextString(tokenType),
                }
                : new Dictionary<CoseLabel, CoseValue>(),
        };
        var authenticated =
            parameters.Credentials.Algorithm.Kind == CoseAlgorithmKind.Signature
                ? Serializer.EncodeSign1(claimsSet, parameters.Credentials, options)
                : Serializer.EncodeMac0(claimsSet, parameters.Credentials, options);

        var token = authenticated;
        if (parameters.EncryptionCredentials is { } credentials)
            token = Serializer.EncodeEncrypt0(
                authenticated,
                credentials,
                new CoseEncodingOptions()
            );
        else if (parameters.EncryptionRecipients is { } recipients)
            token = Serializer.EncodeEncrypt(authenticated, recipients, new CoseEncodingOptions());

        return parameters.WriteCwtTag ? [0xD8, (byte)CwtConstants.Tag, .. token] : token;
    }

    private static void Validate(CwtIssuanceParameters parameters)
    {
        if (
            parameters.Credentials.Algorithm.Kind
            is not (CoseAlgorithmKind.Signature or CoseAlgorithmKind.Mac)
        )
            throw new ArgumentException(
                "The credentials must use a signature or a message authentication code algorithm.",
                nameof(parameters)
            );

        if (
            parameters.EncryptionCredentials is not null
            && parameters.EncryptionRecipients is not null
        )
            throw new ArgumentException(
                "A token is encrypted with credentials or with recipients, not both.",
                nameof(parameters)
            );

        foreach (var label in parameters.AdditionalClaims.Keys)
        {
            if (!label.IsText && label.IntegerValue is >= 1 and <= 8)
                throw new ArgumentException(
                    "The claims with the keys one through eight are set from the typed members.",
                    nameof(parameters)
                );
        }

        if (
            parameters.ExpiresAt is { } expiresAt
            && (
                (parameters.NotBefore is { } notBefore && expiresAt <= notBefore)
                || (parameters.IssuedAt is { } issuedAt && expiresAt <= issuedAt)
            )
        )
            throw new ArgumentException(
                "The expiration time must be after the not-before time and the issued-at time.",
                nameof(parameters)
            );
    }
}
