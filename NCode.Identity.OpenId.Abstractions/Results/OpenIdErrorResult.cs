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

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Provides an implementation of <see cref="IResult"/> that when executed, will render an <see cref="IOpenIdError"/>.
/// </summary>
public class OpenIdErrorResult : IResult, ISupportResult, ISupportError
{
    IResult ISupportResult.Result => this;

    /// <summary>
    /// Gets or sets the <see cref="IOpenIdError"/>.
    /// </summary>
    public required IOpenIdError Error { get; init; }

    /// <inheritdoc />
    public virtual async Task ExecuteAsync(HttpContext httpContext)
    {
        var result = TypedResults.Json(
            Error,
            Error.OpenIdServer.JsonSerializerOptions,
            statusCode: Error.StatusCode);
        await result.ExecuteAsync(httpContext);
    }
}
