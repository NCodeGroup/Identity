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

using Microsoft.Net.Http.Headers;
using NIdentity.OpenId.Endpoints;

namespace NIdentity.OpenId.Results;

// TODO: register in DI

internal class OpenIdRedirectResultExecutor : IOpenIdResultExecutor<OpenIdRedirectResult>
{
    private const string AjaxHeaderValue = "XMLHttpRequest";

    private static bool IsAjaxRequest(OpenIdContext context) =>
        string.Equals(context.HttpContext.Request.Query[HeaderNames.XRequestedWith], AjaxHeaderValue, StringComparison.Ordinal) ||
        string.Equals(context.HttpContext.Request.Headers.XRequestedWith, AjaxHeaderValue, StringComparison.Ordinal);

    /// <inheritdoc />
    public ValueTask ExecuteResultAsync(
        OpenIdContext context,
        OpenIdRedirectResult result,
        CancellationToken cancellationToken)
    {
        var isAjaxRequest = result.IsAjaxRequest ?? IsAjaxRequest;
        if (isAjaxRequest(context))
        {
            context.HttpContext.Response.Headers.Location = result.RedirectUrl;

            if (result.AjaxStatusCode.HasValue)
                context.HttpContext.Response.StatusCode = result.AjaxStatusCode.Value;
        }
        else
        {
            context.HttpContext.Response.Redirect(result.RedirectUrl);
        }

        return ValueTask.CompletedTask;
    }
}
