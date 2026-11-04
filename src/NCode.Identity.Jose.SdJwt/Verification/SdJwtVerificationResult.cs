// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Jwt;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// The outcome of verifying a presentation of a Selective Disclosure JWT: the disclosed claims with every
/// disclosure resolved into place, or the code of the rule the presentation failed.
/// </summary>
[PublicAPI]
public sealed class SdJwtVerificationResult
{
    private SdJwtVerificationResult() { }

    /// <summary>
    /// Creates the result of a presentation that passed every check.
    /// </summary>
    /// <param name="sdJwt">The parsed presentation.</param>
    /// <param name="issuerJwt">The decoded issuer-signed JWT.</param>
    /// <param name="claims">The claims with every disclosure resolved into place and the selective disclosure
    /// members removed.</param>
    /// <param name="disclosures">Each presented disclosure, resolved to its place in <paramref name="claims"/>.</param>
    /// <param name="keyBinding">The verified Key Binding JWT, or <c>null</c> when none was required.</param>
    /// <returns>The result.</returns>
    public static SdJwtVerificationResult Success(
        SelectiveDisclosureJwt sdJwt,
        DecodedJwt issuerJwt,
        JsonElement claims,
        IEnumerable<DisclosedClaim> disclosures,
        SdJwtKeyBinding? keyBinding
    ) =>
        new()
        {
            SdJwt = sdJwt,
            IssuerJwt = issuerJwt,
            Claims = claims,
            Disclosures = [.. disclosures],
            KeyBinding = keyBinding,
        };

    /// <summary>
    /// Creates the result of a presentation that failed a check.
    /// </summary>
    /// <param name="failureCode">The machine-readable code of the rule the presentation failed: one of the
    /// <see cref="SdJwtVerificationFailureCodes"/>, or a JWT validation failure code of the issuer-signed JWT.</param>
    /// <param name="failureReason">The human-readable description of the failure.</param>
    /// <returns>The result.</returns>
    public static SdJwtVerificationResult Fail(string failureCode, string failureReason)
    {
        ArgumentException.ThrowIfNullOrEmpty(failureCode);
        ArgumentNullException.ThrowIfNull(failureReason);

        return new SdJwtVerificationResult
        {
            FailureCode = failureCode,
            FailureReason = failureReason,
        };
    }

    /// <summary>
    /// Gets a value indicating whether the presentation passed every check.
    /// </summary>
    [MemberNotNullWhen(false, nameof(FailureCode))]
    [MemberNotNullWhen(false, nameof(FailureReason))]
    [MemberNotNullWhen(true, nameof(SdJwt))]
    [MemberNotNullWhen(true, nameof(IssuerJwt))]
    public bool IsValid => FailureCode is null;

    /// <summary>
    /// Gets the machine-readable code of the rule the presentation failed, or <c>null</c> when it is valid.
    /// </summary>
    public string? FailureCode { get; private init; }

    /// <summary>
    /// Gets the human-readable description of the failure, or <c>null</c> when the presentation is valid.
    /// </summary>
    public string? FailureReason { get; private init; }

    /// <summary>
    /// Gets the parsed presentation, or <c>null</c> when it is invalid.
    /// </summary>
    public SelectiveDisclosureJwt? SdJwt { get; private init; }

    /// <summary>
    /// Gets the decoded issuer-signed JWT, or <c>null</c> when the presentation is invalid.
    /// </summary>
    public DecodedJwt? IssuerJwt { get; private init; }

    /// <summary>
    /// Gets the claims with every disclosure resolved into place and the <c>_sd</c>, <c>_sd_alg</c> and <c>...</c>
    /// members removed, or an undefined element when the presentation is invalid.
    /// </summary>
    public JsonElement Claims { get; private init; }

    /// <summary>
    /// Gets each presented disclosure, resolved to its place in <see cref="Claims"/>; empty when the presentation is
    /// invalid.
    /// </summary>
    public ImmutableArray<DisclosedClaim> Disclosures { get; private init; } = [];

    /// <summary>
    /// Gets the verified Key Binding JWT, or <c>null</c> when none was required or the presentation is invalid.
    /// </summary>
    public SdJwtKeyBinding? KeyBinding { get; private init; }
}
