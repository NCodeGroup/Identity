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

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Holds the per-request state that <c>UseBff()</c> sets up and that the session resolver reads.
/// </summary>
internal sealed class BffRequestFeature
{
    /// <summary>
    /// Gets or sets the endpoint that the BFF request checks were run for.
    /// </summary>
    public Endpoint? CheckedEndpoint { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the session of the request was resolved.
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// Gets or sets the resolved session, or <see langword="null"/> when the request has none.
    /// </summary>
    public BffSessionState? Session { get; set; }

    /// <summary>
    /// Gets or sets the principal built from the resolved session.
    /// </summary>
    public ClaimsPrincipal? Principal { get; set; }
}
