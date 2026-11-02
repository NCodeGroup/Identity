// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Jwt.Encoding;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers the <c>form_post.jwt</c> response mode (JARM §2.3.3): the response JWT in the <c>response</c> form field.
/// </summary>
internal sealed class FormPostJwtAuthorizationResponseModeHandler(
    TimeProvider timeProvider,
    IJwtEncoder jwtEncoder
) : JwtAuthorizationResponseModeHandler(timeProvider, jwtEncoder)
{
    public override string ResponseMode => OpenIdProtocol.ResponseModes.FormPostJwt;

    public override string SecuredResponseMode => OpenIdProtocol.ResponseModes.FormPost;

    public override bool DeliversInQuery => false;

    protected override ValueTask WriteAsync(
        HttpResponse httpResponse,
        string redirectUri,
        IReadOnlyCollection<KeyValuePair<string, StringValues>> parameters,
        CancellationToken cancellationToken
    ) =>
        AuthorizationResponseWriter.WriteFormPostAsync(
            httpResponse,
            redirectUri,
            parameters,
            cancellationToken
        );
}
