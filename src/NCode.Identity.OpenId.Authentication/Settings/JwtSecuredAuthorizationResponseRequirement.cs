#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion


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
