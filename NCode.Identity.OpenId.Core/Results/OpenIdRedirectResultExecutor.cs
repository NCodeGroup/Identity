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
using Microsoft.Net.Http.Headers;

namespace NCode.Identity.OpenId.Results;

internal class OpenIdRedirectResultExecutor : IResultExecutor<OpenIdRedirectResult>
{
    private const string AjaxHeaderValue = "XMLHttpRequest";

    private static bool IsAjaxRequest(HttpContext httpContext) =>
        string.Equals(httpContext.Request.Query[HeaderNames.XRequestedWith], AjaxHeaderValue, StringComparison.Ordinal) ||
        string.Equals(httpContext.Request.Headers.XRequestedWith, AjaxHeaderValue, StringComparison.Ordinal);

    /// <inheritdoc />
    public ValueTask ExecuteAsync(
        HttpContext httpContext,
        OpenIdRedirectResult result,
        CancellationToken cancellationToken)
    {
        var isAjaxRequest = result.IsAjaxRequest ?? IsAjaxRequest;
        if (isAjaxRequest(httpContext))
        {
            httpContext.Response.Headers.Location = result.RedirectUrl;

            if (result.AjaxStatusCode.HasValue)
                httpContext.Response.StatusCode = result.AjaxStatusCode.Value;
        }
        else
        {
            httpContext.Response.Redirect(result.RedirectUrl);
        }

        return ValueTask.CompletedTask;
    }
}
