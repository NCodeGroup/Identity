// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>user_or_client</c>, which serves the user's access token when the
/// request has a session and the BFF's own access token when it has none.
/// </summary>
// The choice is made on the session alone: a session whose user token cannot be served fails with that token's
// failure and never falls back to the BFF's own token, which would act with other rights. See ADR-1381.
internal class BffUserOrClientTokenRequirement(
    IBffUserTokenService userService,
    IBffClientTokenService clientService
) : IBffTokenRequirement
{
    private IBffUserTokenService UserService { get; } = userService;
    private IBffClientTokenService ClientService { get; } = clientService;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.UserOrClient;

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
        if (state is not null)
            return await UserService.GetAccessTokenAsync(scope, request, cancellationToken);

        return await ClientService.GetAccessTokenAsync(scope, request, cancellationToken);
    }
}
