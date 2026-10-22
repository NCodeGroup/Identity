#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
    public override string ResponseMode => OpenIdConstants.ResponseModes.FragmentJwt;

    public override string SecuredResponseMode => OpenIdConstants.ResponseModes.Fragment;

    public override bool DeliversInQuery => false;

    protected override ValueTask WriteAsync(
        HttpResponse httpResponse,
        Uri redirectUri,
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
