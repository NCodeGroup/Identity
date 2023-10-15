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
/// Provides an implementation of <see cref="IOpenIdResult"/> that redirects an HTTP client to a specified URL
/// and is <c>AJAX</c> aware.
/// </summary>
public class OpenIdRedirectResult : OpenIdResult<OpenIdRedirectResult>
{
    /// <summary>
    /// Gets or sets the URL used for the redirect operation.
    /// </summary>
    public string RedirectUrl { get; }

    /// <summary>
    /// Gets or sets the function that is used to determine if the current request is an <c>AJAX</c> request.
    /// If <c>null</c>, a default implementation is provided that checks the <c>X-Requested-With</c> header for the
    /// value <c>XMLHttpRequest</c>.
    /// See the following for more information: https://github.com/aspnet/Security/issues/1394
    /// </summary>
    public Func<OpenIdContext, bool>? IsAjaxRequest { get; init; }

    /// <summary>
    /// Gets or sets the HTTP status code that is only used for <c>AJAX</c> redirects.
    /// Browser redirects will always use status code <c>302</c>.
    /// </summary>
    public int? AjaxStatusCode { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdRedirectResult"/> class.
    /// </summary>
    /// <param name="redirectUrl">The URL used for the redirect operation.</param>
    public OpenIdRedirectResult(string redirectUrl)
    {
        RedirectUrl = redirectUrl;
    }
}
