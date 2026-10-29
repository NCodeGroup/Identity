#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Bff.Auditing;

/// <summary>
/// Captures the <see cref="AuditRequest"/> that describes the HTTP request which caused an audited BFF operation.
/// </summary>
/// <remarks>
/// The factory never reads the query string, the request body or any header other than <c>User-Agent</c>,
/// because those can carry codes, tokens or credentials.
/// </remarks>
internal static class BffAuditRequestFactory
{
    private const int MaxUserAgentLength = 512;

    /// <summary>
    /// Captures the request context of the specified HTTP request and of the current <see cref="Activity"/>.
    /// </summary>
    public static AuditRequest Create(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var request = httpContext.Request;
        var activity = Activity.Current;
        var endpoint = httpContext.GetEndpoint();

        return new AuditRequest
        {
            RequestId = NullIfEmpty(httpContext.TraceIdentifier),
            TraceId = activity is { IdFormat: ActivityIdFormat.W3C }
                ? activity.TraceId.ToHexString()
                : null,
            SpanId = activity is { IdFormat: ActivityIdFormat.W3C }
                ? activity.SpanId.ToHexString()
                : null,
            SourceIp = NullIfEmpty(httpContext.Connection.RemoteIpAddress?.ToString()),
            UserAgent = NullIfEmpty(
                Truncate(request.Headers.UserAgent.ToString(), MaxUserAgentLength)
            ),
            HttpMethod = NullIfEmpty(request.Method),
            Path = NullIfEmpty((request.PathBase + request.Path).Value),
            Endpoint = NullIfEmpty(
                endpoint?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                    ?? endpoint?.DisplayName
            ),
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        // never end on the first half of a surrogate pair, which cannot be written as valid text
        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length];
    }
}
