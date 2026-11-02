// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization.Results;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization;

/// <summary>
/// Provides an abstraction to store and consume pushed authorization requests (RFC 9126).
/// </summary>
[PublicAPI]
public interface IPushedAuthorizationRequestService
{
    /// <summary>
    /// Persists the parameters of a pushed authorization request and issues the one-time <c>request_uri</c> the client
    /// supplies to the authorization endpoint.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that pushed the authorization request.</param>
    /// <param name="requestValues">The raw authorization request parameters the client pushed.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PushedAuthorizationResult"/> with the issued <c>request_uri</c> and its lifetime.</returns>
    ValueTask<PushedAuthorizationResult> StoreAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IRequestValues requestValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Consumes a previously-stored pushed authorization request and returns its replayed parameters. The request is
    /// single-use; a second consumption of the same <c>request_uri</c> fails.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The authenticated <see cref="OpenIdClient"/> invoking the authorization endpoint,
    /// used to bind the pushed request to the client that pushed it.</param>
    /// <param name="requestUri">The <c>request_uri</c> value supplied to the authorization endpoint.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the replayed
    /// <see cref="IRequestValues"/> of the pushed request.</returns>
    /// <exception cref="NCode.Identity.OpenId.Exceptions.OpenIdException">Thrown as an <c>invalid_request_uri</c> error
    /// when the <paramref name="requestUri"/> is unknown, expired, already consumed, or was not issued to
    /// <paramref name="openIdClient"/>.</exception>
    ValueTask<IRequestValues> ConsumeAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string requestUri,
        CancellationToken cancellationToken
    );
}
