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
using NIdentity.OpenId.Endpoints;

namespace NIdentity.OpenId.Results;

public class HttpResult : OpenIdResult
{
    /// <summary>
    /// Gets or sets the <see cref="IResult"/>.
    /// </summary>
    public IResult Result { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResult"/> class.
    /// </summary>
    /// <param name="result"><see cref="IResult"/></param>
    public HttpResult(IResult result)
    {
        Result = result;
    }

    /// <inheritdoc />
    public override async ValueTask ExecuteResultAsync(OpenIdEndpointContext context) =>
        await Result.ExecuteAsync(context.HttpContext);
}
