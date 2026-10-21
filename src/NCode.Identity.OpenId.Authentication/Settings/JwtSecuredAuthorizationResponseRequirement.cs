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
/// response JWT authenticates the response and binds it to the request (FAPI 1.0 Advanced §5.2.2-2).
/// </summary>
internal sealed class JwtSecuredAuthorizationResponseRequirement
    : IAuthorizationRequestProfileRequirement
{
    // JARM names every JWT-secured response mode by appending '.jwt' to the mode that delivers it (JARM §2.3), and
    // the request's response mode is already resolved, so the 'jwt' shorthand arrives as its full name.
    private const string JwtResponseModeSuffix = ".jwt";

    /// <inheritdoc />
    public ValueTask EvaluateAsync(
        AuthorizationRequestProfileContext context,
        CancellationToken cancellationToken
    )
    {
        var request = context.Request;
        var returnsIdToken = request.ResponseTypes.Contains(
            OpenIdConstants.ResponseTypes.IdToken,
            StringComparer.Ordinal
        );
        var isJwtSecured = request.ResponseMode.EndsWith(
            JwtResponseModeSuffix,
            StringComparison.Ordinal
        );

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
