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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEndpointExceptionHandlerMetadata"/> abstraction.
/// </summary>
public class OpenIdEndpointExceptionHandlerMetadata(
    Func<HttpContext, CancellationToken, ValueTask<IOpenIdExceptionHandler>> getter
) : IOpenIdEndpointExceptionHandlerMetadata
{
    private Func<HttpContext, CancellationToken, ValueTask<IOpenIdExceptionHandler>> Getter { get; } = getter;

    /// <inheritdoc />
    public async ValueTask<IOpenIdExceptionHandler> GetExceptionHandlerAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) => await Getter(httpContext, cancellationToken);
}
