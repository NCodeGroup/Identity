// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Requires an authorization response to be protected: a request that returns no ID token through the front channel
/// (plain <c>code</c>) must use a JWT-secured (JARM) response mode, since only the hybrid ID token or a signed
/// response JWT authenticates the response and binds it to the request (FAPI 1.0 Advanced §5.2.2-2). A response is
/// JWT-secured when the delivery's handler declares a <c>SecuredResponseMode</c>.
/// </summary>
internal sealed class JwtSecuredAuthorizationResponseRequirement
    : IAuthorizationRequestProfileRequirement
{
    /// <inheritdoc />
    public ValueTask EvaluateAsync(
        AuthorizationRequestProfileContext context,
        CancellationToken cancellationToken
    )
    {
        var request = context.Request;
        var returnsIdToken = request.ResponseTypes.Contains(
            OpenIdProtocol.ResponseTypes.IdToken,
            StringComparer.Ordinal
        );
        var isJwtSecured = context.Delivery.ResponseModeHandler.SecuredResponseMode is not null;

        if (!returnsIdToken && !isJwtSecured)
        {
            throw context
                .OpenIdContext.ErrorFactory.InvalidRequest(
                    "A selected security profile requires a JWT-secured response mode (such as 'jwt') or the "
                        + "'code id_token' response type."
                )
                .AsException();
        }

        return ValueTask.CompletedTask;
    }
}
