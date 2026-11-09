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
internal class BffClientTokenRequirement(IBffClientTokenService service) : IBffTokenRequirement
{
    private IBffClientTokenService Service { get; } = service;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.Client;

    /// <inheritdoc />
    public async ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffTokenContext context,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var scope = IBffSessionScope.From(context);
        var state = await scope.ResolveAsync(cancellationToken);
        if (state is null)
            return BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            );

        return await Service.GetAccessTokenAsync(scope, request, cancellationToken);
    }
}
