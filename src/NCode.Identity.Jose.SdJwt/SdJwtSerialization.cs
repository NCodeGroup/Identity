// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NCode.Identity.Jose.SdJwt.Verification;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// Reads and writes the combined format of a Selective Disclosure JWT (RFC 9901 §4):
/// <c>&lt;Issuer-signed JWT&gt;~&lt;Disclosure 1&gt;~...~&lt;Disclosure N&gt;~[&lt;KB-JWT&gt;]</c>.
/// </summary>
internal static class SdJwtSerialization
{
    private const char Separator = SelectiveDisclosureJwt.Separator;

    /// <summary>
    /// Writes the combined format.
    /// </summary>
    /// <param name="issuerSignedJwt">The compact JWS the issuer signed.</param>
    /// <param name="disclosures">The disclosures to serialize, in order.</param>
    /// <param name="keyBindingJwt">The compact Key Binding JWT, or <c>null</c> to end the format with <c>~</c>.</param>
    /// <returns>The combined format.</returns>
    public static string Serialize(
        string issuerSignedJwt,
        IReadOnlyList<Disclosure> disclosures,
        string? keyBindingJwt
    )
    {
        var builder = new StringBuilder(issuerSignedJwt.Length + 1);
        builder.Append(issuerSignedJwt);
        builder.Append(Separator);

        var count = disclosures.Count;
        for (var i = 0; i < count; i++)
        {
            builder.Append(disclosures[i].Encoded);
            builder.Append(Separator);
        }

        if (keyBindingJwt is not null)
            builder.Append(keyBindingJwt);

        return builder.ToString();
    }

    /// <summary>
    /// Reads the combined format, bounding the number of disclosures before any is decoded.
    /// </summary>
    /// <param name="serialized">The combined format.</param>
    /// <param name="maxDisclosureCount">The largest number of disclosures accepted.</param>
    /// <returns>The parsed SD-JWT.</returns>
    /// <exception cref="SdJwtException">The value is not in the combined format, carries more disclosures than the
    /// limit, or carries a disclosure that is not of the expected shape.</exception>
    public static SelectiveDisclosureJwt Parse(string serialized, int maxDisclosureCount)
    {
        ArgumentNullException.ThrowIfNull(serialized);

        var separatorCount = serialized.AsSpan().Count(Separator);
        if (separatorCount == 0)
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "An SD-JWT must carry at least one '~' after the issuer-signed JWT."
            );

        // The parts between the first and the last separator are disclosures; the last part is the KB-JWT or empty.
        var disclosureCount = separatorCount - 1;
        if (disclosureCount > maxDisclosureCount)
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.DisclosureLimitExceeded,
                $"The SD-JWT carries {disclosureCount} disclosures, more than the limit of {maxDisclosureCount}."
            );

        var parts = serialized.Split(Separator);
        var issuerSignedJwt = parts[0];
        if (issuerSignedJwt.Length == 0)
            throw new SdJwtException(
                SdJwtVerificationFailureCodes.InvalidSdJwt,
                "An SD-JWT must begin with the issuer-signed JWT."
            );

        var disclosures = new Disclosure[disclosureCount];
        for (var i = 0; i < disclosureCount; i++)
            disclosures[i] = Disclosure.Parse(parts[i + 1]);

        var keyBindingJwt = parts[^1];

        return new SelectiveDisclosureJwt(
            issuerSignedJwt,
            disclosures,
            keyBindingJwt.Length == 0 ? null : keyBindingJwt
        );
    }

    /// <summary>
    /// Returns the part of a presentation that a Key Binding JWT's <c>sd_hash</c> covers: everything up to and including
    /// the last <c>~</c> (RFC 9901 §4.3.1).
    /// </summary>
    /// <param name="presentation">The presentation as received.</param>
    /// <returns>The presentation without its Key Binding JWT.</returns>
    public static ReadOnlySpan<char> GetKeyBindingInput(string presentation)
    {
        var span = presentation.AsSpan();
        var index = span.LastIndexOf(Separator);
        return span[..(index + 1)];
    }
}
