// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Presentation;

/// <summary>
/// The inputs of presenting a Selective Disclosure JWT (RFC 9901 §6): the SD-JWT the holder received, the
/// disclosures it chooses to reveal, and the Key Binding JWT the verifier asked for, if any.
/// </summary>
[PublicAPI]
public sealed class SdJwtPresentationOptions
{
    /// <summary>
    /// Gets the SD-JWT the holder received from the issuer. A Key Binding JWT it carries is not presented.
    /// </summary>
    public required SelectiveDisclosureJwt SdJwt { get; init; }

    /// <summary>
    /// Gets the disclosures to reveal, each one of <see cref="SelectiveDisclosureJwt.Disclosures"/>. A disclosure
    /// whose digest travels in another disclosure's value is presented with that disclosure, because a verifier can
    /// resolve it in no other way. The default value is empty, which reveals nothing beyond the issuer-signed JWT.
    /// </summary>
    public IReadOnlyCollection<Disclosure> Disclosures { get; init; } = [];

    /// <summary>
    /// Gets the inputs of the Key Binding JWT, or <c>null</c> for a presentation without key binding.
    /// </summary>
    public SdJwtKeyBindingOptions? KeyBinding { get; init; }
}
