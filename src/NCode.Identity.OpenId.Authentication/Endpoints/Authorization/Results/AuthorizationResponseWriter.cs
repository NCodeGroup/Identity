#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
using Microsoft.Extensions.Primitives;
using NCode.Buffers;
using NCode.Identity.OpenId.Messages;
using Nerdbank.Streams;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;

/// <summary>
/// Writes authorization response parameters to the client's redirect URI, as a redirect that carries them in the query
/// or the fragment, or as an auto-submitting form post. The response-mode handlers share it, so a JWT-secured response
/// mode delivers its single <c>response</c> parameter exactly as its plain counterpart delivers every parameter.
/// </summary>
internal static class AuthorizationResponseWriter
{
    /// <summary>
    /// Gets the parameters of a response message that are serialized to the client.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, StringValues>> GetParameters(
        IOpenIdMessageResponse message
    )
    {
        var environment = message.OpenIdEnvironment;
        return message
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
            )
            .ToList();
    }

    /// <summary>
    /// Redirects to the redirect URI with the parameters in its query or in its fragment. The redirect URI equals a
    /// registered one as written (ADR-0203), so its query is the registered query, which the response keeps unless a
    /// response parameter replaces it (RFC 6749 §3.1.2).
    /// </summary>
    public static void WriteRedirect(
        HttpResponse httpResponse,
        string redirectUri,
        bool useQuery,
        IReadOnlyCollection<KeyValuePair<string, StringValues>> parameters
    )
    {
        // ADR-0203: delivered as written; re-serializing through Uri adds a trailing slash and lower-cases the host
        var serialized = SerializeUriParameters(parameters);

        var location = useQuery
            ? AppendToQuery(redirectUri, parameters, serialized)
            : $"{redirectUri}#{serialized}";

        ExecuteUsingRedirect(httpResponse, location);
    }

    private static string AppendToQuery(
        string redirectUri,
        IReadOnlyCollection<KeyValuePair<string, StringValues>> parameters,
        string serialized
    )
    {
        var queryStart = redirectUri.IndexOf('?', StringComparison.Ordinal);
        var withoutQuery = queryStart < 0 ? redirectUri : redirectUri[..queryStart];
        var registeredQuery = queryStart < 0 ? string.Empty : redirectUri[(queryStart + 1)..];
        var registeredPairs = registeredQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);

        List<string> pairs = [.. registeredPairs.Where(pair => !IsReplaced(pair, parameters))];
        if (serialized.Length > 0)
        {
            pairs.Add(serialized);
        }

        if (pairs.Count == 0)
        {
            return $"{withoutQuery}#_=_";
        }

        var query = string.Join('&', pairs);
        return $"{withoutQuery}?{query}#_=_";
    }

    private static bool IsReplaced(
        string registeredPair,
        IReadOnlyCollection<KeyValuePair<string, StringValues>> parameters
    )
    {
        var nameEnd = registeredPair.IndexOf('=', StringComparison.Ordinal);
        var encodedName = nameEnd < 0 ? registeredPair : registeredPair[..nameEnd];
        var name = Uri.UnescapeDataString(encodedName);

        return parameters.Any(parameter =>
            string.Equals(parameter.Key, name, StringComparison.OrdinalIgnoreCase)
        );
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

    private static void ExecuteUsingRedirect(HttpResponse httpResponse, string location)
    {
        httpResponse.SetNoStore();

        // 303 See Other (not 302) is required for the authorization response (FAPI 2.0 §5.2.2 / OAuth 2.1): it forces
        // the user-agent to issue a GET to the redirect_uri regardless of the original request method.
        httpResponse.StatusCode = StatusCodes.Status303SeeOther;
        httpResponse.Headers.Location = RedirectLocation.Escape(location);
    }

    private const string FormPostJavascript =
        "window.addEventListener('load',function(){document.forms[0].submit();});";
    private const string FormPostHtmlStart =
        "<html><head><title>Working...</title></head><body><form method='POST' action='";
    private const string FormPostHtmlEnd =
        $"<noscript><p>Script is disabled. Click Submit to continue.</p><input type='submit' value='Submit'/></noscript></form><script>{FormPostJavascript}</script></body></html>";

    private static readonly string FormPostContentSecurityPolicy =
        $"default-src 'none'; script-src 'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(FormPostJavascript)))}'";

    /// <summary>
    /// Answers with an auto-submitting HTML form that posts the parameters to the redirect URI.
    /// </summary>
    public static async ValueTask WriteFormPostAsync(
        HttpResponse httpResponse,
        string redirectUri,
        IEnumerable<KeyValuePair<string, StringValues>> parameters,
        CancellationToken cancellationToken
    )
    {
        httpResponse.SetNoStore();
        httpResponse.Headers["Referrer-Policy"] = "no-referrer";
        httpResponse.Headers.ContentSecurityPolicy = FormPostContentSecurityPolicy;

        // The page is rendered into a pooled buffer and transcoded straight into the response pipe, so no per-request
        // document string is built.
        using var html = new Sequence<char>(ArrayPool<char>.Shared);
        WriteFormPostHtml(html, redirectUri, parameters);

        httpResponse.StatusCode = StatusCodes.Status200OK;
        httpResponse.ContentType = "text/html; charset=utf-8";

        SecureEncoding.UTF8.GetBytes(html.AsReadOnlySequence, httpResponse.BodyWriter);
        await httpResponse.BodyWriter.FlushAsync(cancellationToken);
    }

    private static void WriteFormPostHtml(
        IBufferWriter<char> writer,
        string redirectUri,
        IEnumerable<KeyValuePair<string, StringValues>> parameters
    )
    {
        writer.Write(FormPostHtmlStart);
        WriteEncoded(writer, redirectUri);
        writer.Write("'>");

        // The browser form-urlencodes each field when it submits the form, so a value is only HTML-encoded here;
        // url-encoding it as well would reach the client double-encoded.
        foreach (var (parameterName, stringValues) in parameters)
        {
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
