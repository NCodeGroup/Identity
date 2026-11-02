// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides the ability to continue an <c>OAuth</c> or <c>OpenID Connect</c> operation from callbacks.
/// </summary>
[PublicAPI]
public interface IContinueProvider
{
    /// <summary>
    /// Gets the <see cref="string"/> <c>Continue Code</c> for the current provider.
    /// </summary>
    string ContinueCode { get; }

    /// <summary>
    /// Continues an <c>OAuth</c> or <c>OpenID Connect</c> operation given the current HTTP request and continue payload.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="continuePayloadJson">The <see cref="JsonElement"/> which contains the payload for the continue operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ReadOnlyEndpointDisposition"/> for the operation.</returns>
    ValueTask<ReadOnlyEndpointDisposition> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Ends a parked <c>OAuth</c> or <c>OpenID Connect</c> operation with an error instead of resuming it, which answers
    /// the party that started the operation.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="continuePayloadJson">The <see cref="JsonElement"/> which contains the payload for the continue operation.</param>
    /// <param name="error">The <see cref="IOpenIdError"/> that ends the operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ReadOnlyEndpointDisposition"/> for the operation.</returns>
    ValueTask<ReadOnlyEndpointDisposition> RefuseAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        IOpenIdError error,
        CancellationToken cancellationToken
    );
}
