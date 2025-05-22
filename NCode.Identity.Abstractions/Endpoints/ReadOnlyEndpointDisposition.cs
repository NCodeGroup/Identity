#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Endpoints;

/// <summary>
/// Indicates whether an endpoint was handled and optionally provides the HTTP response to return.
/// </summary>
[PublicAPI]
public readonly record struct ReadOnlyEndpointDisposition
{
    /// <summary>
    /// Creates a new instance of the <see cref="ReadOnlyEndpointDisposition"/> class that indicates the endpoint was not handled.
    /// </summary>
    public static ReadOnlyEndpointDisposition UnHandled() =>
        new() { WasHandled = false };

    /// <summary>
    /// Creates a new instance of the <see cref="ReadOnlyEndpointDisposition"/> class that indicates the endpoint was handled.
    /// </summary>
    public static ReadOnlyEndpointDisposition Handled() =>
        new() { WasHandled = true };

    /// <summary>
    /// Creates a new instance of the <see cref="ReadOnlyEndpointDisposition"/> class that indicates the endpoint was handled
    /// and provides the HTTP response to return.
    /// </summary>
    public static ReadOnlyEndpointDisposition Handled(IResult httpResult) =>
        new() { WasHandled = true, HttpResult = httpResult };

    /// <summary>
    /// Gets a value indicating whether the endpoint was handled or not.
    /// </summary>
    public bool WasHandled { get; init; }

    /// <summary>
    /// Gets the optional <see cref="IResult"/> that represents the HTTP response to return for the endpoint.
    /// </summary>
    public IResult? HttpResult { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="HttpResult"/> is not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(HttpResult))]
    public bool HasHttpResult => HttpResult is not null;
}
