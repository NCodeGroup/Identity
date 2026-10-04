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

using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides extension methods for <see cref="IEndpointConventionBuilder"/>.
/// </summary>
public static class OpenIdEndpointConventionBuilderExtensions
{
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/> instance.</param>
    /// <typeparam name="TBuilder">The type of the <see cref="IEndpointConventionBuilder"/> instance.</typeparam>
    extension<TBuilder>(TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        /// <summary>
        /// Adds <see cref="IOpenIdEndpointDiscoverableMetadata"/> to the <see cref="IEndpointConventionBuilder"/> to indicate whether the endpoint is discoverable.
        /// </summary>
        /// <param name="isDiscoverable">Specifies whether the endpoint is discoverable. Default is <c>true</c>.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithOpenIdDiscoverable(bool isDiscoverable = true) =>
            builder.WithMetadata(
                new DefaultOpenIdEndpointDiscoverableMetadata { IsDiscoverable = isDiscoverable }
            );

        /// <summary>
        /// Adds <see cref="IOpenIdEndpointExceptionHandlerMetadata"/> to the <see cref="IEndpointConventionBuilder"/> to indicate the exception handler for the endpoint.
        /// </summary>
        /// <param name="getter">The delegate to get the <see cref="IOpenIdExceptionHandler"/> instance.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithOpenIdExceptionHandler(
            Func<
                HttpContext,
                OpenIdEnvironment,
                CancellationToken,
                ValueTask<IOpenIdExceptionHandler>
            > getter
        ) => builder.WithMetadata(new DefaultOpenIdEndpointExceptionHandlerMetadata(getter));

        /// <summary>
        /// Adds <see cref="IOpenIdExceptionHandler"/> to the <see cref="IEndpointConventionBuilder"/> to indicate the exception handler for the endpoint.
        /// </summary>
        /// <param name="exceptionHandler">The <see cref="IOpenIdExceptionHandler"/> instance.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithOpenIdExceptionHandler(IOpenIdExceptionHandler exceptionHandler) =>
            builder.WithOpenIdExceptionHandler((_, _, _) => ValueTask.FromResult(exceptionHandler));

        /// <summary>
        /// Adds an <see cref="OpenApiOperation"/> to the <see cref="IEndpointConventionBuilder"/> describing an
        /// <c>application/x-www-form-urlencoded</c> request body whose properties are all the known <c>OAuth</c>
        /// and <c>OpenID Connect</c> parameters.
        /// </summary>
        /// <param name="knownParameterCollectionProvider">The <see cref="IKnownParameterCollectionProvider"/> used to enumerate the known parameters.</param>
        /// <param name="operationId">The unique identifier for the OpenAPI operation.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder"/> instance for method chaining.</returns>
        public TBuilder WithOpenIdFormParameters(
            IKnownParameterCollectionProvider knownParameterCollectionProvider,
            string operationId
        ) =>
            builder.WithMetadata(
                CreateFormUrlEncodedOperation(knownParameterCollectionProvider, operationId)
            );
    }

    private static OpenApiOperation CreateFormUrlEncodedOperation(
        IKnownParameterCollectionProvider knownParameterCollectionProvider,
        string operationId
    )
    {
        ArgumentNullException.ThrowIfNull(knownParameterCollectionProvider);

        var properties = knownParameterCollectionProvider
            .Collection.OrderBy(parameter => parameter.Name)
            .ToDictionary(
                parameter => parameter.Name,
                IOpenApiSchema (_) =>
                    new OpenApiSchema
                    {
                        Type = JsonSchemaType.String | JsonSchemaType.Null,
                        Default = JsonValue.Create(string.Empty),
                    }
            );

        return new OpenApiOperation
        {
            OperationId = operationId,
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [OpenIdConstants.ContentType] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Properties = properties,
                        },
                    },
                },
            },
        };
    }
}
