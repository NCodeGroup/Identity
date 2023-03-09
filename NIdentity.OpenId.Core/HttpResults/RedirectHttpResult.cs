#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.HttpResults;

/// <summary>
/// Represents an <see cref="IHttpResult"/> that when executed will
/// redirect the client to a specific URL location.
/// </summary>
public class RedirectHttpResult : HttpResult
{
    /// <summary>
    /// Gets or sets the absolute URL to redirect the client to.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectHttpResult"/> class.
    /// </summary>
    /// <param name="location">The absolute URL to redirect the client to.</param>
    public RedirectHttpResult(string location)
    {
        Location = location;
    }

    /// <inheritdoc/>
    protected override ValueTask ExecuteCoreAsync(HttpContext httpContext)
    {
        httpContext.Response.Redirect(Location);
        return ValueTask.CompletedTask;
    }
}
