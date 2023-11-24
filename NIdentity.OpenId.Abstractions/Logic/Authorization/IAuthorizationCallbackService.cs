#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NIdentity.OpenId.Endpoints.Authorization.Messages;

namespace NIdentity.OpenId.Logic.Authorization;

/// <summary>
/// Provides the ability for the authorization server to generate return URLs for authorization requests.
/// </summary>
public interface IAuthorizationCallbackService
{
    /// <summary>
    /// Gets the URL that a user-agent, after successfully authenticating an end-user, may return to
    /// the authorization server and continue the authorization flow.
    /// </summary>
    /// <param name="authorizationContext">The <see cref="AuthorizationContext"/> for the current authorization request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the return URL.</returns>
    ValueTask<string> GetContinueUrlAsync(
        AuthorizationContext authorizationContext,
        CancellationToken cancellationToken);

    ValueTask<IAuthorizationRequest?> TryGetAuthorizationRequestAsync(
        OpenIdContext openIdContext,
        string state,
        CancellationToken cancellationToken);
}
