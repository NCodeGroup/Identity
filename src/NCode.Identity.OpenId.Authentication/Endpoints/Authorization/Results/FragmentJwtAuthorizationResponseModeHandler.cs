// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Jwt.Encoding;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers the <c>fragment.jwt</c> response mode (JARM §2.3.2): the response JWT in the <c>response</c> fragment parameter.
/// </summary>
internal sealed class FragmentJwtAuthorizationResponseModeHandler(
    TimeProvider timeProvider,
    IJwtEncoder jwtEncoder
) : JwtAuthorizationResponseModeHandler(timeProvider, jwtEncoder)
{
    public override string ResponseMode => OpenIdProtocol.ResponseModes.FragmentJwt;

    public override string SecuredResponseMode => OpenIdProtocol.ResponseModes.Fragment;

    public override bool DeliversInQuery => false;

    protected override ValueTask WriteAsync(
        HttpResponse httpResponse,
        string redirectUri,
        IReadOnlyCollection<KeyValuePair<string, StringValues>> parameters,
        CancellationToken cancellationToken
    )
    {
        AuthorizationResponseWriter.WriteRedirect(
            httpResponse,
            redirectUri,
            useQuery: false,
            parameters
        );
        return ValueTask.CompletedTask;
    }
}
