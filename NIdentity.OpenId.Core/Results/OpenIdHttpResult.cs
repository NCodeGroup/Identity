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
/// Provides an implementation of <see cref="IOpenIdResult"/> that when executed, will render an <see cref="IResult"/>
/// that contains a HTTP result.
/// </summary>
public class OpenIdHttpResult : OpenIdResult
{
    /// <summary>
    /// Gets the <see cref="IResult"/> that contains the HTTP result.
    /// </summary>
    public IResult Result { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdHttpResult"/> class with an <see cref="IResult"/>
    /// that contains a HTTP result.
    /// </summary>
    /// <param name="result">The <see cref="IResult"/> that contains a HTTP result.</param>
    public OpenIdHttpResult(IResult result)
    {
        Result = result;
    }

    /// <inheritdoc />
    public override async ValueTask ExecuteResultAsync(OpenIdContext context, CancellationToken cancellationToken) =>
        await Result.ExecuteAsync(context.HttpContext);
}