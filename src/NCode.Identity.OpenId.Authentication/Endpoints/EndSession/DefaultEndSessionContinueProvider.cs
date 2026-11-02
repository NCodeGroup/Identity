// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueProvider"/> abstraction for resuming an RP-Initiated
/// Logout once the end-user has confirmed it.
/// </summary>
internal class DefaultEndSessionContinueProvider(ILogger<DefaultEndSessionContinueProvider> logger)
    : IContinueProvider
{
    private ILogger<DefaultEndSessionContinueProvider> Logger { get; } = logger;

    /// <inheritdoc />
    public string ContinueCode => OpenIdConstants.ContinueCodes.EndSession;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyEndpointDisposition> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        CancellationToken cancellationToken
    )
    {
        var continuation = continuePayloadJson.Deserialize<EndSessionContinuation>(
            openIdContext.Environment.JsonSerializerOptions
        );
        if (continuation == null)
            throw new InvalidOperationException("JSON deserialization returned null.");

        var request = EndSessionRequest.Create(openIdContext.Environment);
        request.IdTokenHint = continuation.IdTokenHint;
        request.ClientId = continuation.ClientId;
        request.PostLogoutRedirectUri = continuation.PostLogoutRedirectUri;
        request.State = continuation.State;

        var mediator = openIdContext.Mediator;

        var subject = await mediator.SendAsync<ResolveEndSessionSubjectCommand, EndSessionSubject>(
            new ResolveEndSessionSubjectCommand(openIdContext, request),
            cancellationToken
        );

        // The continuation was parked for one session; a different browser, or one signed in as someone else, must not
        // be able to end the session it was not asked for.
        // See ADR-1380.
        if (!string.Equals(subject.SessionId, continuation.SessionId, StringComparison.Ordinal))
        {
            Logger.EndSessionContinuationSessionMismatch();

            var error = openIdContext.ErrorFactory.InvalidRequest(
                "The session changed before the logout was confirmed."
            );
            error.StatusCode = StatusCodes.Status400BadRequest;
            return ReadOnlyEndpointDisposition.Handled(error.AsHttpResult());
        }

        var disposition = await mediator.SendAsync<EndSessionCommand, EndSessionDisposition>(
            new EndSessionCommand(openIdContext, request, subject),
            cancellationToken
        );

        return ReadOnlyEndpointDisposition.Handled(
            EndSessionHttpResults.Create(openIdContext.Http, disposition)
        );
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyEndpointDisposition> RefuseAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        IOpenIdError error,
        CancellationToken cancellationToken
    ) =>
        // A declined logout answers nothing: the continuation is spent and the relying party is not told.
        ValueTask.FromResult(ReadOnlyEndpointDisposition.Handled());
}
