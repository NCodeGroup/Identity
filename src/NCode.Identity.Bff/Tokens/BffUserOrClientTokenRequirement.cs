// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>user_or_client</c>, which serves the user's access token when the
/// request has a session and the BFF's own access token when it has none.
/// </summary>
// The choice is made on the session alone: a session whose user token cannot be served fails with that token's
// failure and never falls back to the BFF's own token, which would act with other rights. See ADR-1381.
internal class BffUserOrClientTokenRequirement(
    BffSessionResolver sessionResolver,
    IBffUserTokenService userService,
    IBffClientTokenService clientService
) : IBffTokenRequirement
{
    private readonly BffSessionResolver _sessionResolver = sessionResolver;
    private readonly IBffUserTokenService _userService = userService;
    private readonly IBffClientTokenService _clientService = clientService;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.UserOrClient;

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        HttpContext httpContext,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(request);

        var state = await _sessionResolver.ResolveAsync(httpContext, cancellationToken);
        if (state is not null)
            return await _userService.GetAccessTokenAsync(
                httpContext,
                request.Resource,
                request.Scope,
                cancellationToken
            );

        return await _clientService.GetAccessTokenAsync(
            request.Resource,
            request.Scope,
            cancellationToken
        );
    }
}
