#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Provides an implementation of <see cref="IResult"/> that when executed, will render an <see cref="IOpenIdResponse"/> as a JSON response.
/// </summary>
[PublicAPI]
public class OpenIdResult<T> : IResult
    where T : class, IOpenIdResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdResult{T}"/> class.
    /// </summary>
    /// <param name="response">The <see cref="IOpenIdResponse"/> that contains information about the <c>OAuth</c> or <c>OpenID Connect</c> operation.</param>
    public OpenIdResult(T response)
    {
        Response = response;
    }

    /// <summary>
    /// Gets the response that contains the information about the <c>OAuth</c> or <c>OpenID Connect</c> operation.
    /// </summary>
    public T Response { get; }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var jsonSerializerOptions = Response.OpenIdEnvironment.JsonSerializerOptions;
        var statusCode = Response is ISupportStatusCode supportStatusCode ? supportStatusCode.StatusCode : null;

        var jsonResult = CreateJsonResult(
            Response,
            jsonSerializerOptions,
            statusCode
        );

        await jsonResult.ExecuteAsync(httpContext);
    }

    private static IResult CreateJsonResult(
        IOpenIdResponse response,
        JsonSerializerOptions jsonSerializerOptions,
        int? statusCode
    ) =>
        HttpResults.Json(
            response,
            jsonSerializerOptions.GetTypeInfo(response.GetType()),
            statusCode: statusCode
        );
}
