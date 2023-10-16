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

using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Results;

/// <summary>
/// Defines an interface for a service which can execute a particular kind of <see cref="IOpenIdResult"/> by
/// manipulating the <see cref="HttpResponse"/>.
/// </summary>
/// <typeparam name="TOpenIdResult">The type of <see cref="IOpenIdResult"/>.</typeparam>
/// <remarks>
/// Implementations of <see cref="IOpenIdResultExecutor{THttpResult}"/> are typically called by the
/// <see cref="IOpenIdResult.ExecuteResultAsync"/> method of the corresponding action result type.
/// Implementations should be registered as singleton services.
/// </remarks>
public interface IOpenIdResultExecutor<in TOpenIdResult>
    where TOpenIdResult : IOpenIdResult
{
    /// <summary>
    /// Asynchronously executes the OpenID result, by modifying the <see cref="HttpResponse"/>.
    /// </summary>
    /// <param name="context">The <see cref="OpenIdContext"/> associated with the current request."/></param>
    /// <param name="result">The OpenID result to execute.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> which represents the asynchronous operation.</returns>
    ValueTask ExecuteResultAsync(OpenIdContext context, TOpenIdResult result, CancellationToken cancellationToken);
}
