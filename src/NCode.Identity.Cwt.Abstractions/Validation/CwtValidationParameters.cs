// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Cose;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cwt.Validation;

/// <summary>
/// States the whole policy a CBOR Web Token (CWT) is validated under. Nothing is unrestricted by default: the algorithm
/// and issuer lists must name what is accepted, the keys are given explicitly, and a token that carries an audience is
/// refused unless the caller names its audiences.
/// </summary>
// See ADR-1410.
[PublicAPI]
public sealed class CwtValidationParameters
{
    /// <summary>
    /// Gets the identifiers of the signature or message authentication code algorithms the validator permits. The list
    /// must not be empty.
    /// </summary>
    public required IReadOnlyCollection<int> ValidAlgorithms { get; init; }

    /// <summary>
    /// Gets the issuers the validator accepts, compared ordinally. The list must not be empty.
    /// </summary>
    public required IReadOnlyCollection<string> ValidIssuers { get; init; }

    /// <summary>
    /// Gets the keys that verify the token. Either the keys or <see cref="ResolveValidationKeysAsync"/> is required;
    /// the validator never falls back to the keys of the server itself.
    /// </summary>
    public IReadOnlyCollection<SecretKey>? ValidationKeys { get; init; }

    /// <summary>
    /// Gets the callback that resolves the verification keys from the headers of the token, used when
    /// <see cref="ValidationKeys"/> is <c>null</c>.
    /// </summary>
    public ResolveCwtValidationKeysAsync? ResolveValidationKeysAsync { get; init; }

    /// <summary>
    /// Gets the audiences the validator identifies as, compared ordinally, or <c>null</c> when it identifies with none,
    /// in which case a token that carries an audience is refused. When set, the token must carry one of them.
    /// </summary>
    public IReadOnlyCollection<string>? ValidAudiences { get; init; }

    /// <summary>
    /// Gets the keys that decrypt an encrypted token, or <c>null</c> to refuse an encrypted token.
    /// </summary>
    public IReadOnlyCollection<SecretKey>? DecryptionKeys { get; init; }

    /// <summary>
    /// Gets the identifiers of the content encryption algorithms the validator permits. The list must not be empty when
    /// <see cref="DecryptionKeys"/> is set.
    /// </summary>
    public IReadOnlyCollection<int> ValidContentEncryptionAlgorithms { get; init; } = [];

    /// <summary>
    /// Gets the identifiers of the key distribution algorithms the validator permits for the recipients of a token.
    /// </summary>
    public IReadOnlyCollection<int> ValidKeyDistributionAlgorithms { get; init; } = [];

    /// <summary>
    /// Gets the type the token must declare in its protected <c>typ</c> header parameter, compared ordinally, or
    /// <c>null</c> to accept any.
    /// </summary>
    public string? TokenType { get; init; }

    /// <summary>
    /// Gets the tolerance for the lifetime of the token and the expiry of keys. The default value is five minutes.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets a value indicating whether the token must carry an expiration time. The default value is <c>true</c>.
    /// </summary>
    public bool RequireExpirationTime { get; init; } = true;

    /// <summary>
    /// Gets the longest lifetime the validator accepts, measured from the earlier of the issued-at and not-before times;
    /// it implies that the token carries an expiration time.
    /// </summary>
    public TimeSpan? MaxLifetime { get; init; }

    /// <summary>
    /// Gets a value indicating whether the token must carry a not-before time.
    /// </summary>
    public bool RequireNotBefore { get; init; }

    /// <summary>
    /// Gets a value indicating whether the token must carry an issued-at time.
    /// </summary>
    public bool RequireIssuedAt { get; init; }

    /// <summary>
    /// Gets the keys of the claims the token must carry.
    /// </summary>
    public IReadOnlyCollection<CoseLabel> RequiredClaims { get; init; } = [];

    /// <summary>
    /// Gets the labels of critical header parameters the validator understands beyond the ones the library does.
    /// </summary>
    public IReadOnlyCollection<CoseLabel> UnderstoodCriticalHeaders { get; init; } = [];

    /// <summary>
    /// Gets the external additional authenticated data the authenticated message is bound to.
    /// </summary>
    public ReadOnlyMemory<byte> ExternalAad { get; init; }

    /// <summary>
    /// Gets the type of the message when it carries no tag, or <c>null</c> to refuse an untagged message.
    /// </summary>
    public CoseMessageType? UntaggedMessageType { get; init; }
}
