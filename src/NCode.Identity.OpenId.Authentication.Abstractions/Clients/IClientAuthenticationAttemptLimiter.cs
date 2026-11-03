// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Limits the failed client authentications that one remote address may make, so a client's credential can be
/// guessed online only a limited number of times. Failures are counted per remote address and per remote address and
/// <c>client_id</c>, never per <c>client_id</c> alone, so a caller that knows a client's identifier cannot refuse the
/// client's own requests from another address. Every handler of a client-authentication method that verifies a
/// credential asks <see cref="CheckAsync"/> before it verifies one, and calls <see cref="RecordFailureAsync"/> for each
/// credential it refuses, the identifier of a client it cannot find included; a request refused at the limit is not
/// counted. A <c>none</c> authentication verifies no credential, so it is neither refused nor counted, and a credential
/// too malformed to carry a secret, such as a Basic header that is empty, is answered without a count.
/// </summary>
// See ADR-1399.
[PublicAPI]
public interface IClientAuthenticationAttemptLimiter
{
    /// <summary>
    /// Determines whether the request's remote address, or the remote address and <paramref name="clientId"/>
    /// together, reached the tenant's limit of failed client authentications in the current window, so the request is
    /// refused before any credential is verified.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="clientId">The unverified <c>client_id</c> the request presents, or <see langword="null"/> when it
    /// names none that can be read.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="RateLimitDecision"/> for the request: admitted, or refused until the window ends.</returns>
    ValueTask<RateLimitDecision> CheckAsync(
        OpenIdContext openIdContext,
        string? clientId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records one failed client authentication for the request's remote address, and for the remote address and
    /// <paramref name="clientId"/> together.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="clientId">The unverified <c>client_id</c> the request presented, or <see langword="null"/> when it
    /// named none that can be read.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RecordFailureAsync(
        OpenIdContext openIdContext,
        string? clientId,
        CancellationToken cancellationToken
    );
}
