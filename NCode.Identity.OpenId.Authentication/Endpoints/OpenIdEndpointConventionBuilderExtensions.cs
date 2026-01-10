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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides extension methods for <see cref="IEndpointConventionBuilder"/>.
/// </summary>
public static class OpenIdEndpointConventionBuilderExtensions
{
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/> instance.</param>
    /// <typeparam name="TBuilder">The type of the <see cref="IEndpointConventionBuilder"/> instance.</typeparam>
    extension<TBuilder>(TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Adds <see cref="IOpenIdEndpointDiscoverableMetadata"/> to the <see cref="IEndpointConventionBuilder"/> to indicate whether the endpoint is discoverable.
        /// </summary>
        /// <param name="isDiscoverable">Specifies whether the endpoint is discoverable. Default is <c>true</c>.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithOpenIdDiscoverable(bool isDiscoverable = true) =>
            builder.WithMetadata(
                new OpenIdEndpointDiscoverableMetadata
                {
                    IsDiscoverable = isDiscoverable
                });

        /// <summary>
        /// Adds <see cref="IOpenIdEndpointExceptionHandlerMetadata"/> to the <see cref="IEndpointConventionBuilder"/> to indicate the exception handler for the endpoint.
        /// </summary>
        /// <param name="getter">The delegate to get the <see cref="IOpenIdExceptionHandler"/> instance.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithOpenIdExceptionHandler(
            Func<HttpContext, OpenIdEnvironment, CancellationToken, ValueTask<IOpenIdExceptionHandler>> getter
        ) =>
            builder.WithMetadata(new OpenIdEndpointExceptionHandlerMetadata(getter));

        /// <summary>
        /// Adds <see cref="IOpenIdExceptionHandler"/> to the <see cref="IEndpointConventionBuilder"/> to indicate the exception handler for the endpoint.
        /// </summary>
        /// <param name="exceptionHandler">The <see cref="IOpenIdExceptionHandler"/> instance.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithOpenIdExceptionHandler(IOpenIdExceptionHandler exceptionHandler) =>
            builder.WithOpenIdExceptionHandler((_, _, _) => ValueTask.FromResult(exceptionHandler));
    }
}
