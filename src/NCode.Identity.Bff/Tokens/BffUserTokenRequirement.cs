// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffTokenRequirement"/> named <c>user</c>, which serves the access token of the signed-in user.
/// </summary>
internal class BffUserTokenRequirement(IBffUserTokenService service) : IBffTokenRequirement
{
    private readonly IBffUserTokenService _service = service;

    /// <inheritdoc />
    public string Name => BffTokenRequirementNames.User;

    /// <inheritdoc />
    public ValueTask<BffTokenResult> GetAccessTokenAsync(
        IBffTokenContext context,
        BffTokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var scope = IBffSessionScope.From(context);
        return _service.GetAccessTokenAsync(scope, request, cancellationToken);
    }
}
