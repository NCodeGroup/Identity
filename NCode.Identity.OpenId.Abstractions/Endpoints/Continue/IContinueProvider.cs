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

using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Endpoints.Continue;

/// <summary>
/// Provides the ability to continue an <c>OAuth</c> or <c>OpenID Connect</c> operation from callbacks.
/// </summary>
public interface IContinueProvider
{
    /// <summary>
    /// Gets the <see cref="string"/> <c>Continue Code</c> for the current provider.
    /// </summary>
    string ContinueCode { get; }

    /// <summary>
    /// Continues an <c>OAuth</c> or <c>OpenID Connect</c> operation given the current HTTP request and continue payload.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="continuePayload">The <see cref="JsonElement"/> which contains the payload for the continue operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="IResult"/> for the result of the operation.</returns>
    ValueTask<IResult> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayload,
        CancellationToken cancellationToken);
}
