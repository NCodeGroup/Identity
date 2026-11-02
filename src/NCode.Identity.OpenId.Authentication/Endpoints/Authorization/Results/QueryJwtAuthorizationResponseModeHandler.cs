// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Jwt.Encoding;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers the <c>query.jwt</c> response mode (JARM §2.3.1): the response JWT in the <c>response</c> query parameter.
/// </summary>
internal sealed class QueryJwtAuthorizationResponseModeHandler(
    TimeProvider timeProvider,
    IJwtEncoder jwtEncoder
) : JwtAuthorizationResponseModeHandler(timeProvider, jwtEncoder)
{
    public override string ResponseMode => OpenIdProtocol.ResponseModes.QueryJwt;

    public override string SecuredResponseMode => OpenIdProtocol.ResponseModes.Query;

    public override bool DeliversInQuery => true;

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
            useQuery: true,
            parameters
        );
        return ValueTask.CompletedTask;
    }
}
