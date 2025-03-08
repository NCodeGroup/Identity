#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Identity.OpenId.Endpoints;

/// <summary>
/// Indicates whether an <c>OAuth</c> or <c>OpenID Connect</c> endpoint was handled and optionally provides the HTTP response to return.
/// </summary>
/// <param name="WasHandled">Indicates whether the endpoint was handled or not.</param>
/// <param name="HttpResult">The optional <see cref="IResult"/> that represents the HTTP response to return for the endpoint.</param>
[PublicAPI]
public readonly record struct EndpointDisposition(bool WasHandled, IResult? HttpResult = null)
{
    /// <summary>
    /// Creates a new instance of the <see cref="EndpointDisposition"/> class that indicates the endpoint was not handled.
    /// </summary>
    public static EndpointDisposition UnHandled() => new(WasHandled: false);

    /// <summary>
    /// Creates a new instance of the <see cref="EndpointDisposition"/> class that indicates the endpoint was handled.
    /// </summary>
    public static EndpointDisposition Handled() => new(WasHandled: true);

    /// <summary>
    /// Creates a new instance of the <see cref="EndpointDisposition"/> class that indicates the endpoint was handled
    /// and provides the HTTP response to return.
    /// </summary>
    public static EndpointDisposition Handled(IResult httpResult) => new(WasHandled: true, httpResult);

    /// <summary>
    /// Gets a value indicating whether <see cref="HttpResult"/> is not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(HttpResult))]
    public bool HasHttpResult => HttpResult is not null;
}
