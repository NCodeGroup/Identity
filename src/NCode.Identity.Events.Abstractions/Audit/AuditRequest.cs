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

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Describes the request that caused an audited operation.
/// </summary>
/// <remarks>
/// The context is captured when the event is published, so a sink that runs in the background after the
/// request has completed still sees it. It never contains the query string, the request body or any
/// header other than <c>User-Agent</c>, because those can carry codes, tokens or credentials.
/// </remarks>
[PublicAPI]
public sealed record AuditRequest
{
    /// <summary>
    /// Gets the identifier of the HTTP request, which pivots to the host's request logs.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Gets the W3C trace identifier of the distributed trace the operation ran in, which pivots to traces.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the W3C span identifier of the span that was current when the event was published.
    /// </summary>
    public string? SpanId { get; init; }

    /// <summary>
    /// Gets the IP address of the caller, as corrected by the host for any trusted forwarding proxy.
    /// </summary>
    public string? SourceIp { get; init; }

    /// <summary>
    /// Gets the <c>User-Agent</c> header of the request, truncated to a bounded length.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Gets the HTTP method of the request.
    /// </summary>
    public string? HttpMethod { get; init; }

    /// <summary>
    /// Gets the path of the request, without the query string.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Gets the name of the endpoint that handled the request.
    /// </summary>
    public string? Endpoint { get; init; }
}
