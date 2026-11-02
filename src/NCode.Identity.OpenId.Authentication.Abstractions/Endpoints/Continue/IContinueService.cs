// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides the ability for the authorization server to generate return URLs for external operations that need continuations.
/// </summary>
[PublicAPI]
public interface IContinueService
{
    /// <summary>
    /// Gets the URL that a user-agent, after successfully completing an external operation, may return to
    /// the authorization server and resume the continuation. The <paramref name="payload"/> will be
    /// persisted and available to the <see cref="IContinueProvider"/> when the user-agent returns.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="continueCode">The <see cref="string"/> <c>Continue Code</c> for the continuation.</param>
    /// <param name="clientId">The optional <see cref="string"/> <c>ClientId</c> for the continuation.</param>
    /// <param name="subjectId">The optional <see cref="string"/> <c>SubjectId</c> for the continuation.</param>
    /// <param name="lifetime">The <see cref="TimeSpan"/> that contains the maximum allowable lifetime of the continuation.</param>
    /// <param name="payload">The payload for the continuation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <typeparam name="TPayload">The type of the payload for the continuation.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the return URL.</returns>
    ValueTask<string> GetContinueUrlAsync<TPayload>(
        OpenIdContext openIdContext,
        string continueCode,
        string? clientId,
        string? subjectId,
        TimeSpan lifetime,
        TPayload payload,
        CancellationToken cancellationToken
    );
}
