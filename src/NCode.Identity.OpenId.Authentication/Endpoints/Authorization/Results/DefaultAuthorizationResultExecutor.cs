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

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using NCode.Buffers;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Metrics;
using NCode.Identity.OpenId.Results;
using NCode.Identity.Results;
using Nerdbank.Streams;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Provides the default implementation that executes an <see cref="AuthorizationResult"/> by writing it to the HTTP response.
/// </summary>
internal class DefaultAuthorizationResultExecutor : IResultExecutor<AuthorizationResult>
{
    /// <inheritdoc />
    public async ValueTask ExecuteAsync(
        HttpContext httpContext,
        AuthorizationResult result,
        CancellationToken cancellationToken
    )
    {
        var httpResponse = httpContext.Response;

        // If an error occurred with an explicit HTTP status code,
        // then return a JSON response with the corresponding status code
        // instead of returning a redirect response.
        if (result.Error is { StatusCode: not null })
        {
            var httpResult = result.Error.AsHttpResult();
            await httpResult.ExecuteAsync(httpContext);
            return;
        }

        // An error returned to the client through its redirect URI never reaches the JSON result executor, so it is
        // tagged here with the same server-chosen error code (ADR-0197).
        if (result.Error is not null)
            OpenIdMetricAttributes.Add(
                httpContext,
                OpenIdMetricAttributes.Error,
                result.Error.Code
            );

        IOpenIdMessageResponse? error = result.Error;
        IOpenIdMessageResponse? ticket = result.Ticket;

        var message =
            error
            ?? ticket
            ?? throw new InvalidOperationException("Both error and ticket are null.");

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (result.ResponseMode)
        {
            case OpenIdConstants.ResponseModes.Query:
            case OpenIdConstants.ResponseModes.Fragment:
                var finalRedirectUri = GetFinalRedirectUri(
                    result.RedirectUri,
                    result.ResponseMode,
                    message
                );
                ExecuteUsingRedirect(httpResponse, finalRedirectUri);
                break;

            case OpenIdConstants.ResponseModes.FormPost:
                await ExecuteUsingFormPostAsync(
                    httpResponse,
                    result.RedirectUri,
                    message,
                    cancellationToken
                );
                break;

            default:
                throw new InvalidOperationException("Unsupported response mode.");
        }
    }

    private static Uri GetFinalRedirectUri(
        Uri redirectUri,
        string responseMode,
        IOpenIdMessageResponse message
    )
    {
        if (responseMode == OpenIdConstants.ResponseModes.FormPost)
        {
            return redirectUri;
        }

        var environment = message.OpenIdEnvironment;
        var useQuery = responseMode == OpenIdConstants.ResponseModes.Query;

        var queryOnlyParameters = useQuery
            ? QueryHelpers
                .ParseQuery(redirectUri.Query)
                .ExceptBy(
                    message.Parameters.Select(parameter => parameter.Descriptor.ParameterName),
                    tuple => tuple.Key,
                    StringComparer.OrdinalIgnoreCase
                )
            : [];

        var messageOnlyParameters = message
            .Parameters.Where(parameter =>
                parameter.Descriptor.ShouldSerialize(
                    environment,
                    parameter,
                    SerializationFormat.OpenId
                )
            )
            .Select(parameter =>
                KeyValuePair.Create(
                    parameter.Descriptor.ParameterName,
                    parameter.GetStringValues(environment)
                )
            );

        var allParameters = queryOnlyParameters.Concat(messageOnlyParameters);
        var serializedParameters = SerializeUriParameters(allParameters);

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

    private static string SerializeUriParameters(
        IEnumerable<KeyValuePair<string, StringValues>> parameters
    )
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

                    if (string.IsNullOrEmpty(stringValue))
                        continue;

                    builder.Append('=');
                    builder.Append(Uri.EscapeDataString(stringValue));
                }
            }
        }

        return builder.ToString();
    }

    private static void ExecuteUsingRedirect(HttpResponse httpResponse, Uri redirectUri)
    {
        httpResponse.SetNoStore();

        // 303 See Other (not 302) is required for the authorization response (FAPI 2.0 §5.2.2 / OAuth 2.1): it forces
        // the user-agent to issue a GET to the redirect_uri regardless of the original request method.
        httpResponse.StatusCode = StatusCodes.Status303SeeOther;
        httpResponse.Headers.Location = redirectUri.AbsoluteUri;
    }

    private const string FormPostJavascript =
        "window.addEventListener('load',function(){document.forms[0].submit();});";
    private const string FormPostHtmlStart =
        "<html><head><title>Working...</title></head><body><form method='POST' action='";
    private const string FormPostHtmlEnd =
        $"<noscript><p>Script is disabled. Click Submit to continue.</p><input type='submit' value='Submit'/></noscript></form><script>{FormPostJavascript}</script></body></html>";

    private static readonly string FormPostContentSecurityPolicy =
        $"default-src 'none'; script-src 'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(FormPostJavascript)))}'";

    private static async ValueTask ExecuteUsingFormPostAsync(
        HttpResponse httpResponse,
        Uri redirectUri,
        IOpenIdMessageResponse message,
        CancellationToken cancellationToken
    )
    {
        httpResponse.SetNoStore();
        httpResponse.Headers["Referrer-Policy"] = "no-referrer";
        httpResponse.Headers.ContentSecurityPolicy = FormPostContentSecurityPolicy;

        // The page is rendered into a pooled buffer and transcoded straight into the response pipe, so no per-request
        // document string is built.
        using var html = new Sequence<char>(ArrayPool<char>.Shared);
        WriteFormPostHtml(html, redirectUri, message);

        httpResponse.StatusCode = StatusCodes.Status200OK;
        httpResponse.ContentType = "text/html; charset=utf-8";

        SecureEncoding.UTF8.GetBytes(html.AsReadOnlySequence, httpResponse.BodyWriter);
        await httpResponse.BodyWriter.FlushAsync(cancellationToken);
    }

    private static void WriteFormPostHtml(
        IBufferWriter<char> writer,
        Uri redirectUri,
        IOpenIdMessageResponse message
    )
    {
        writer.Write(FormPostHtmlStart);
        WriteEncoded(writer, redirectUri.AbsoluteUri);
        writer.Write("'>");

        // The browser form-urlencodes each field when it submits the form, so a value is only HTML-encoded here;
        // url-encoding it as well would reach the client double-encoded.
        var environment = message.OpenIdEnvironment;
        foreach (var parameter in message.Parameters)
        {
            if (
                !parameter.Descriptor.ShouldSerialize(
                    environment,
                    parameter,
                    SerializationFormat.OpenId
                )
            )
            {
                continue;
            }

            var parameterName = parameter.Descriptor.ParameterName;
            var stringValues = parameter.GetStringValues(environment);
            if (stringValues.Count == 0)
            {
                WriteHiddenField(writer, parameterName, value: null);
                continue;
            }

            foreach (var stringValue in stringValues)
            {
                WriteHiddenField(writer, parameterName, stringValue);
            }
        }

        writer.Write(FormPostHtmlEnd);
    }

    private static void WriteHiddenField(IBufferWriter<char> writer, string name, string? value)
    {
        writer.Write("<input type='hidden' name='");
        WriteEncoded(writer, name);
        writer.Write("' value='");
        WriteEncoded(writer, value);
        writer.Write("'/>");
    }

    private static void WriteEncoded(IBufferWriter<char> writer, ReadOnlySpan<char> value)
    {
        var encoder = HtmlEncoder.Default;
        var sizeHint = value.Length;
        while (true)
        {
            var status = encoder.Encode(
                value,
                writer.GetSpan(sizeHint),
                out var consumed,
                out var written,
                isFinalBlock: true
            );
            writer.Advance(written);
            value = value[consumed..];

            if (status == OperationStatus.Done)
            {
                return;
            }

            // An encoded character can take several characters, so ask for more room than the input still needs.
            sizeHint = Math.Max(sizeHint * 2, 16);
        }
    }
}
