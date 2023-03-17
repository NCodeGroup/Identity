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

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization.Results;

internal class AuthorizationResultExecutor : IOpenIdResultExecutor<IAuthorizationResult>
{
    public async ValueTask ExecuteResultAsync(OpenIdEndpointContext context, IAuthorizationResult result)
    {
        var httpContext = context.HttpContext;
        var httpResponse = httpContext.Response;

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (result.ResponseMode)
        {
            case ResponseMode.Query:
            case ResponseMode.Fragment:
                var finalRedirectUri = GetFinalRedirectUri(result);
                ExecuteUsingRedirect(httpResponse, finalRedirectUri);
                break;

            case ResponseMode.FormPost:
                IOpenIdMessage? error = result.Error;
                await ExecuteUsingFormPostAsync(httpResponse, result.RedirectUri, error ?? result, httpContext.RequestAborted);
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    private static Uri GetFinalRedirectUri(IAuthorizationResult result)
    {
        if (result.ResponseMode == ResponseMode.FormPost)
        {
            return result.RedirectUri;
        }

        IOpenIdMessage? error = result.Error;
        var parameterSource = error ?? result;

        var useQuery = result.ResponseMode == ResponseMode.Query;
        var existingParameters = useQuery ?
            QueryHelpers
                .ParseQuery(result.RedirectUri.Query)
                .ExceptBy(
                    parameterSource.Keys,
                    kvp => kvp.Key,
                    StringComparer.OrdinalIgnoreCase) :
            Enumerable.Empty<KeyValuePair<string, StringValues>>();

        var parameters = existingParameters.Union(parameterSource);
        var serializedParameters = SerializeParameters(parameters);

        var uriBuilder = new UriBuilder(result.RedirectUri);
        if (useQuery)
        {
            uriBuilder.Query = serializedParameters;
            uriBuilder.Fragment = "_=_";
        }
        else if (result.ResponseMode == ResponseMode.Fragment)
        {
            uriBuilder.Fragment = serializedParameters;
        }
        else
        {
            throw new InvalidOperationException("TODO");
        }

        return uriBuilder.Uri;
    }

    private static string SerializeParameters(IEnumerable<KeyValuePair<string, StringValues>> parameters)
    {
        var first = true;
        var builder = new StringBuilder();

        // TODO: should we replace "+" with "%20" and where (key and/or value)?

        foreach (var (parameterName, stringValues) in parameters)
        {
            if (stringValues.Count == 0)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(parameterName));
            }
            else
            {
                foreach (var stringValue in stringValues)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        builder.Append('&');
                    }

                    builder.Append(Uri.EscapeDataString(parameterName));
                    builder.Append('=');
                    builder.Append(Uri.EscapeDataString(stringValue));
                }
            }
        }

        return builder.ToString();
    }

    private static void ExecuteUsingRedirect(HttpResponse httpResponse, Uri redirectUri)
    {
        httpResponse.Headers.Add("Pragma", "no-cache");
        httpResponse.Headers.Add("Cache-Control", "no-store, no-cache, max-age=0");

        httpResponse.Redirect(redirectUri.AbsoluteUri);
    }

    private const string FormPostAction = "{FormPostAction}";
    private const string FormPostChildren = "{FormPostChildren}";
    private const string FormPostJavascript = "window.addEventListener('load',function(){document.forms[0].submit();});";
    private const string FormPostHtml = $"<html><head><title>Working...</title></head><body><form method='POST' action='{FormPostAction}'>{FormPostChildren}<noscript><p>Script is disabled. Click Submit to continue.</p><input type='submit' value='Submit'/></noscript></form><script language='javascript'>{FormPostJavascript}</script></body></html>";

    private static async ValueTask ExecuteUsingFormPostAsync(HttpResponse httpResponse, Uri redirectUri, IOpenIdMessage message, CancellationToken cancellationToken)
    {
        httpResponse.Headers.Add("Pragma", "no-cache");
        httpResponse.Headers.Add("Cache-Control", "no-store, no-cache, max-age=0");

        var scriptHash = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(FormPostJavascript)));

        httpResponse.Headers.Add("Referrer-Policy", "no-referrer");
        httpResponse.Headers.Add("Content-Security-Policy", $"default-src 'none'; script-src 'sha256-{scriptHash}'");

        var html = GetFormPostHtml(redirectUri, message);

        httpResponse.StatusCode = StatusCodes.Status200OK;
        httpResponse.ContentType = "text/html; charset=UTF-8";

        await httpResponse.WriteAsync(html, Encoding.UTF8, cancellationToken);
    }

    private static string GetFormPostHtml(Uri redirectUri, IOpenIdMessage message)
    {
        var parameters = message.SelectMany(kvp =>
            kvp.Value.Select(value =>
                KeyValuePair.Create(kvp.Key, value)
            )
        );

        var children = parameters.Aggregate(
            new StringBuilder(),
            (builder, kvp) => builder
                .Append("<input type='hidden' name='")
                .Append(Uri.EscapeDataString(kvp.Key))
                .Append("' value='")
                .Append(Uri.EscapeDataString(kvp.Value))
                .Append("'/>")
        );

        var html = new StringBuilder(FormPostHtml);
        html.Replace(FormPostAction, redirectUri.AbsoluteUri);
        html.Replace(FormPostChildren, children.ToString());

        return html.ToString();
    }
}
