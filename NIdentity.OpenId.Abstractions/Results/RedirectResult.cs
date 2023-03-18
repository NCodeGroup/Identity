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

using NIdentity.OpenId.Endpoints;

namespace NIdentity.OpenId.Results;

/// <summary>
/// An implementation of <see cref="IOpenIdResult"/> that when executed, will issue a <c>302 Found</c> status HTTP response.
/// </summary>
public class RedirectResult : OpenIdResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RedirectResult"/> class.
    /// </summary>
    /// <param name="url">The url to redirect to.</param>
    public RedirectResult(string url)
    {
        Url = url;
    }

    /// <summary>
    /// Gets or sets the url to redirect to.
    /// </summary>
    public string Url { get; set; }

    /// <inheritdoc />
    public override async ValueTask ExecuteResultAsync(OpenIdEndpointContext context) =>
        await GetExecutor<RedirectResult>(context).ExecuteResultAsync(context, this);
}
