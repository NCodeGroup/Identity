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
using NCode.CryptoMemory;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Results;

internal class AuthorizationResultExecutor : IResultExecutor<AuthorizationResult>
{
    public async ValueTask ExecuteAsync(HttpContext httpContext, AuthorizationResult result, CancellationToken cancellationToken)
    {
        var httpResponse = httpContext.Response;

        // If an error occurred with an explicit HTTP status code,
        // then return a JSON response with the corresponding status code
        // instead of returning a redirect response.
        if (result.Error is { StatusCode: not null })
        {
            var httpResult = result.Error.AsResult();
            await httpResult.ExecuteAsync(httpContext);
            return;
        }

        IBaseOpenIdMessage? error = result.Error;
        IBaseOpenIdMessage? ticket = result.Ticket;

        var message = error ?? ticket ?? throw new InvalidOperationException("Both error and ticket are null.");

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (result.ResponseMode)
        {
            case OpenIdConstants.ResponseModes.Query:
            case OpenIdConstants.ResponseModes.Fragment:
                var finalRedirectUri = GetFinalRedirectUri(result.RedirectUri, result.ResponseMode, message);
                ExecuteUsingRedirect(httpResponse, finalRedirectUri);
                break;

            case OpenIdConstants.ResponseModes.FormPost:
                await ExecuteUsingFormPostAsync(httpResponse, result.RedirectUri, message, cancellationToken);
                break;

            default:
                throw new InvalidOperationException("Unsupported response mode.");
        }
    }

    private static Uri GetFinalRedirectUri(Uri redirectUri, string responseMode, IBaseOpenIdMessage message)
    {
        if (responseMode == OpenIdConstants.ResponseModes.FormPost)
        {
            return redirectUri;
        }

        var useQuery = responseMode == OpenIdConstants.ResponseModes.Query;
        var existingParameters = useQuery ?
            QueryHelpers
                .ParseQuery(redirectUri.Query)
                .ExceptBy(
                    message.Keys,
                    kvp => kvp.Key,
                    StringComparer.OrdinalIgnoreCase) :
            [];

        var parameters = existingParameters.Concat(message);
        var serializedParameters = SerializeUriParameters(parameters);

        var uriBuilder = new UriBuilder(redirectUri);
        if (useQuery)
        {
            uriBuilder.Query = serializedParameters;
            uriBuilder.Fragment = "_=_";
        }
        else if (responseMode == OpenIdConstants.ResponseModes.Fragment)
        {
            uriBuilder.Fragment = serializedParameters;
        }
        else
        {
            throw new InvalidOperationException("Unsupported response mode.");
        }

        return uriBuilder.Uri;
    }

    private static string SerializeUriParameters(IEnumerable<KeyValuePair<string, StringValues>> parameters)
    {
        var first = true;
        var builder = new StringBuilder();

        // In the URL, encode spaces as '%20'
        // Uri.EscapeDataString does this for us

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

                    if (string.IsNullOrEmpty(stringValue)) continue;

                    builder.Append('=');
                    builder.Append(Uri.EscapeDataString(stringValue));
                }
            }
        }

        return builder.ToString();
    }

    private static void ExecuteUsingRedirect(HttpResponse httpResponse, Uri redirectUri)
    {
        httpResponse.Headers.Pragma = "no-cache";
        httpResponse.Headers.CacheControl = "no-store, no-cache, max-age=0";

        httpResponse.Redirect(redirectUri.AbsoluteUri);
    }

    private const string FormPostAction = "{FormPostAction}";
    private const string FormPostChildren = "{FormPostChildren}";
    private const string FormPostJavascript = "window.addEventListener('load',function(){document.forms[0].submit();});";
    private const string FormPostHtml = $"<html><head><title>Working...</title></head><body><form method='POST' action='{FormPostAction}'>{FormPostChildren}<noscript><p>Script is disabled. Click Submit to continue.</p><input type='submit' value='Submit'/></noscript></form><script language='javascript'>{FormPostJavascript}</script></body></html>";

    private static async ValueTask ExecuteUsingFormPostAsync(HttpResponse httpResponse, Uri redirectUri, IBaseOpenIdMessage message, CancellationToken cancellationToken)
    {
        httpResponse.Headers.Pragma = "no-cache";
        httpResponse.Headers.CacheControl = "no-store, no-cache, max-age=0";

        var scriptHash = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(FormPostJavascript)));

        httpResponse.Headers["Referrer-Policy"] = "no-referrer";
        httpResponse.Headers.ContentSecurityPolicy = $"default-src 'none'; script-src 'sha256-{scriptHash}'";

        var html = GetFormPostHtml(redirectUri, message);

        httpResponse.StatusCode = StatusCodes.Status200OK;
        httpResponse.ContentType = "text/html; charset=UTF-8";

        await httpResponse.WriteAsync(html, SecureEncoding.UTF8, cancellationToken);
    }

    private static string GetFormPostHtml(Uri redirectUri, IBaseOpenIdMessage message)
    {
        var parameters = message.SelectMany(kvp =>
            kvp.Value.Select(value =>
                KeyValuePair.Create(kvp.Key, value)
            )
        );

        // In the Form, encode spaces as '+'
        // But Uri.EscapeDataString uses '%20', so replace them

        var children = parameters.Aggregate(
            new StringBuilder(),
            (builder, kvp) => builder
                .Append("<input type='hidden' name='")
                .Append(EncodeFormParameter(kvp.Key))
                .Append("' value='")
                .Append(EncodeFormParameter(kvp.Value))
                .Append("'/>")
        );

        var html = new StringBuilder(FormPostHtml);
        html.Replace(FormPostAction, redirectUri.AbsoluteUri);
        html.Replace(FormPostChildren, children.ToString());

        return html.ToString();
    }

    private static string EncodeFormParameter(string? value) =>
        // Reference: `FormUrlEncodedContent.Encode`
        // Escape spaces as '+'.
        string.IsNullOrEmpty(value) ? string.Empty : Uri.EscapeDataString(value).Replace("%20", "+");
}
