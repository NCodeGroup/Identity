// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>client</c>, which serves the BFF's own access token to a request
/// that has a session.
/// </summary>
// The BFF's own token is never served to an anonymous caller: the session is what proves the caller is the BFF's
// frontend. See ADR-1381.
internal class BffClientTokenRequirement(BffTokenContexts contexts, IBffClientTokenService service)
    : IBffTokenRequirement
{
    private readonly BffTokenContexts _contexts = contexts;
    private readonly IBffClientTokenService _service = service;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.Client;

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(request);

        var scope = _contexts.ForRequest(httpContext);
        var state = await scope.ResolveAsync(cancellationToken);
        if (state is null)
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

        return await _service.GetAccessTokenAsync(scope, request, cancellationToken);
    }
}
