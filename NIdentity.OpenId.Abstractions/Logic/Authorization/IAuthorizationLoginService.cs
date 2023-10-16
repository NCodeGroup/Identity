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
/// Provides the ability for the authorization server to generate redirect URLs that are used to authenticate an end-user.
/// </summary>
public interface IAuthorizationLoginService
{
    /// <summary>
    /// Gets the URL that the authorization server may redirect the user-agent in order to authenticate an end-user.
    /// </summary>
    /// <param name="authorizationContext">The <see cref="AuthorizationContext"/> for the current authorization request.</param>
    /// <param name="returnUrl">The URL that the user-agent may return to the authorization server after successfully authenticating the end-user.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the redirect URL.</returns>
    ValueTask<string> GetRedirectUrlAsync(
        AuthorizationContext authorizationContext,
        string returnUrl,
        CancellationToken cancellationToken);
}
