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

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Endpoints;

// The BFF cookie authenticates only these endpoints (ADR-0292).

/// <summary>
/// Classifies the endpoints whose requests the BFF cookie may authenticate.
/// </summary>
internal static class BffGuardedEndpoints
{
    /// <summary>
    /// Determines whether the endpoint is an API marked <c>[BffApi]</c> or <c>AsBffApi()</c>: a challenge answers 401
    /// and a request slides the session.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool IsApi(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<BffApiAttribute>() is not null;

    /// <summary>
    /// Determines whether the endpoint is an API or <c>/bff/user</c>: the anti-forgery header and Fetch Metadata are
    /// required.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool RequiresAntiforgery(Endpoint? endpoint) =>
        IsApi(endpoint) || endpoint?.Metadata.GetMetadata<BffUserEndpointMetadata>() is not null;

    /// <summary>
    /// Determines whether the endpoint is a page marked <c>[BffPage]</c> or <c>AsBffPage()</c> and not an API: GET and
    /// HEAD only, a challenge redirects to login.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool IsPage(Endpoint? endpoint) =>
        !RequiresAntiforgery(endpoint)
        && endpoint?.Metadata.GetMetadata<BffPageAttribute>() is not null;

    /// <summary>
    /// Determines whether the endpoint is one the BFF cookie may authenticate.
    /// </summary>
    /// <param name="endpoint">The endpoint of the request, if any.</param>
    public static bool IsGuarded(Endpoint? endpoint) =>
        RequiresAntiforgery(endpoint) || IsPage(endpoint);
}
