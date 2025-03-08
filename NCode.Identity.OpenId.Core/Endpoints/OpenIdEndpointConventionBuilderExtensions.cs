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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Endpoints;

/// <summary>
/// Provides extension methods for <see cref="IEndpointConventionBuilder"/>.
/// </summary>
[PublicAPI]
public static class OpenIdEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="IOpenIdEndpointDiscoverableMetadata"/> to the <see cref="IEndpointConventionBuilder"/> to indicate whether the endpoint is discoverable.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/> instance.</param>
    /// <param name="isDiscoverable">Specifies whether the endpoint is discoverable. Default is <c>true</c>.</param>
    /// <typeparam name="TBuilder">The type of the <see cref="IEndpointConventionBuilder"/> instance.</typeparam>
    /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
    public static TBuilder WithOpenIdDiscoverable<TBuilder>(
        this TBuilder builder,
        bool isDiscoverable = true
    )
        where TBuilder : IEndpointConventionBuilder
        =>
            builder.WithMetadata(
                new OpenIdEndpointDiscoverableMetadata
                {
                    IsDiscoverable = isDiscoverable
                });

    /// <summary>
    /// Adds <see cref="IOpenIdEndpointExceptionHandlerMetadata"/> to the <see cref="IEndpointConventionBuilder"/> to indicate the exception handler for the endpoint.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/> instance.</param>
    /// <param name="getter">The delegate to get the <see cref="IOpenIdExceptionHandler"/> instance.</param>
    /// <typeparam name="TBuilder">The type of the <see cref="IEndpointConventionBuilder"/> instance.</typeparam>
    /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
    public static TBuilder WithOpenIdExceptionHandler<TBuilder>(
        this TBuilder builder,
        Func<HttpContext, OpenIdEnvironment, CancellationToken, ValueTask<IOpenIdExceptionHandler>> getter
    )
        where TBuilder : IEndpointConventionBuilder
        =>
            builder.WithMetadata(new OpenIdEndpointExceptionHandlerMetadata(getter));

    /// <summary>
    /// Adds <see cref="IOpenIdExceptionHandler"/> to the <see cref="IEndpointConventionBuilder"/> to indicate the exception handler for the endpoint.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/> instance.</param>
    /// <param name="exceptionHandler">The <see cref="IOpenIdExceptionHandler"/> instance.</param>
    /// <typeparam name="TBuilder">The type of the <see cref="IEndpointConventionBuilder"/> instance.</typeparam>
    /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
    public static TBuilder WithOpenIdExceptionHandler<TBuilder>(
        this TBuilder builder,
        IOpenIdExceptionHandler exceptionHandler
    )
        where TBuilder : IEndpointConventionBuilder
        =>
            builder.WithOpenIdExceptionHandler((_, _, _) => ValueTask.FromResult(exceptionHandler));
}
