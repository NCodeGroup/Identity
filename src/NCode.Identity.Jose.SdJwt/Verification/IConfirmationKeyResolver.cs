// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// Resolves the holder key that a <c>cnf</c> claim (RFC 7800) names through one confirmation method, such as
/// <c>jwk</c>. The confirmation methods are an open vocabulary: a resolver for another method, such as a key
/// identifier looked up in a store, is registered beside the built-in one.
/// </summary>
// See ADR-1405.
[PublicAPI]
public interface IConfirmationKeyResolver
{
    /// <summary>
    /// Gets the member of the <c>cnf</c> claim this resolver reads, such as <c>jwk</c>.
    /// </summary>
    string Member { get; }

    /// <summary>
    /// Resolves the holder key from the <c>cnf</c> claim, which carries <see cref="Member"/>.
    /// </summary>
    /// <param name="confirmation">The <c>cnf</c> claim, a JSON object.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The holder's public key, or <c>null</c> when the member names no usable public key.</returns>
    ValueTask<SecretKey?> ResolveAsync(
        JsonElement confirmation,
        CancellationToken cancellationToken
    );
}
