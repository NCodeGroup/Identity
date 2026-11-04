// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.Jose.SdJwt.Presentation;

/// <summary>
/// The inputs of the Key Binding JWT of a presentation (RFC 9901 §4.3): the holder's key, whose public half the
/// issuer wrote to the <c>cnf</c> claim, and the audience and nonce the verifier supplied.
/// </summary>
[PublicAPI]
public sealed class SdJwtKeyBindingOptions
{
    /// <summary>
    /// Gets the holder's asymmetric key and signature algorithm that sign the Key Binding JWT.
    /// </summary>
    public required JoseSigningCredentials SigningCredentials { get; init; }

    /// <summary>
    /// Gets the verifier the presentation is for, written to the <c>aud</c> claim.
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>
    /// Gets the nonce the verifier supplied for this presentation, written to the <c>nonce</c> claim.
    /// </summary>
    public required string Nonce { get; init; }
}
