// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// A Selective Disclosure JWT (SD-JWT) in its combined format (RFC 9901 §4): the issuer-signed JWT, the disclosures
/// it carries, and the Key Binding JWT of a presentation, when one is present. <see cref="ToString"/> writes the
/// combined format <c>&lt;Issuer-signed JWT&gt;~&lt;Disclosure 1&gt;~...~&lt;Disclosure N&gt;~[&lt;KB-JWT&gt;]</c>,
/// which ends with <c>~</c> when there is no Key Binding JWT.
/// </summary>
/// <param name="issuerSignedJwt">The compact JWS the issuer signed.</param>
/// <param name="disclosures">The disclosures, in the order they are serialized.</param>
/// <param name="keyBindingJwt">The compact Key Binding JWT of a presentation, or <c>null</c>.</param>
[PublicAPI]
public sealed class SelectiveDisclosureJwt(
    string issuerSignedJwt,
    IEnumerable<Disclosure> disclosures,
    string? keyBindingJwt
)
{
    /// <summary>
    /// The character that separates the parts of the combined format.
    /// </summary>
    public const char Separator = '~';

    /// <summary>
    /// Gets the compact JWS the issuer signed.
    /// </summary>
    public string IssuerSignedJwt { get; } = issuerSignedJwt;

    /// <summary>
    /// Gets the disclosures, in the order they are serialized.
    /// </summary>
    public ImmutableArray<Disclosure> Disclosures { get; } = [.. disclosures];

    /// <summary>
    /// Gets the compact Key Binding JWT of a presentation, or <c>null</c> when the SD-JWT carries none.
    /// </summary>
    public string? KeyBindingJwt { get; } = keyBindingJwt;

    /// <summary>
    /// Gets a value indicating whether the SD-JWT carries a Key Binding JWT.
    /// </summary>
    [MemberNotNullWhen(true, nameof(KeyBindingJwt))]
    public bool HasKeyBinding => KeyBindingJwt is not null;

    /// <summary>
    /// Returns the combined format of the SD-JWT.
    /// </summary>
    public override string ToString() =>
        SdJwtSerialization.Serialize(IssuerSignedJwt, Disclosures, KeyBindingJwt);
}
