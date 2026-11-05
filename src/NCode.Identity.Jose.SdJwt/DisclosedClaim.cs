// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// A disclosure resolved to its place in the claims of a Selective Disclosure JWT: the JSON Pointer (RFC 6901) of the
/// claim it discloses within the resolved claims, and the disclosure whose value carries its digest, when the digest
/// is not in the issuer-signed JWT payload itself.
/// </summary>
/// <param name="disclosure">The disclosure.</param>
/// <param name="path">The JSON Pointer of the disclosed claim within the resolved claims, such as
/// <c>/address/street_address</c> or <c>/nationalities/1</c>.</param>
/// <param name="parent">The disclosure whose value carries the digest of this one, or <c>null</c> when the
/// issuer-signed JWT payload carries it.</param>
[PublicAPI]
public sealed class DisclosedClaim(Disclosure disclosure, string path, Disclosure? parent)
{
    /// <summary>
    /// Gets the disclosure.
    /// </summary>
    public Disclosure Disclosure { get; } = disclosure;

    /// <summary>
    /// Gets the JSON Pointer of the disclosed claim within the resolved claims.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the disclosure whose value carries the digest of this one, or <c>null</c> when the issuer-signed JWT
    /// payload carries it. Presenting this claim requires presenting its parent too.
    /// </summary>
    public Disclosure? Parent { get; } = parent;
}
