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
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Delivers the <c>fragment</c> response mode: a redirect with the response parameters in the fragment.
/// </summary>
internal sealed class FragmentAuthorizationResponseModeHandler : IAuthorizationResponseModeHandler
{
    public string ResponseMode => OpenIdConstants.ResponseModes.Fragment;

    public string? SecuredResponseMode => null;

    public bool DeliversInQuery => false;

    public ValueTask ExecuteAsync(
        HttpContext httpContext,
        AuthorizationResult result,
        IOpenIdMessageResponse message,
        CancellationToken cancellationToken
    )
    {
        AuthorizationResponseWriter.WriteRedirect(
            httpContext.Response,
            result.Delivery.RedirectUri,
            useQuery: false,
            AuthorizationResponseWriter.GetParameters(message)
        );
        return ValueTask.CompletedTask;
    }
}
