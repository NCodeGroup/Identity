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

using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// An endpoint filter, installed once on the OpenID endpoint group, that renders exceptions thrown by
/// OpenID endpoints as standard OpenID error responses. The handler is resolved per-request: an endpoint's
/// own <see cref="IOpenIdEndpointExceptionHandlerMetadata"/> takes precedence, otherwise the globally
/// registered <see cref="IOpenIdExceptionHandler"/> is used.
/// </summary>
internal sealed class OpenIdExceptionEndpointFilter : IEndpointFilter
{
    // Sentinel on HttpContext.Items so a single error is audited at most once per request.
    private const string ErrorPublishedKey = "NCode.Identity.OpenId.ErrorAudited";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        try
        {
            var result = await next(context);
            await TryPublishErrorAsync(httpContext, result);
            return result;
        }
        catch (Exception exception)
        {
            // The OpenId context (environment + mediator) is published on the request by the context factory.
            // If it is absent the request never entered the OpenId pipeline, so let the framework handle it.
            var feature = httpContext.Features.Get<IOpenIdContextFeature>();
            if (feature is null)
                throw;

            var cancellationToken = httpContext.RequestAborted;
            var openIdContext = feature.OpenIdContext;
            var openIdEnvironment = openIdContext.Environment;

            // A protocol exception already carries its error; audit it before rendering so the record
            // survives even if the handler throws. The sentinel de-dupes the disposition publish below.
            if (exception is OpenIdException openIdException)
                await PublishErrorAsync(httpContext, openIdContext, openIdException.Error);

            var handler = await GetExceptionHandlerAsync(
                httpContext,
                openIdEnvironment,
                cancellationToken
            );

            var disposition = await handler.HandleExceptionAsync(
                openIdEnvironment,
                httpContext,
                openIdContext.Mediator,
                ExceptionDispatchInfo.Capture(exception),
                cancellationToken
            );

            await TryPublishErrorAsync(httpContext, disposition.HttpResult);

            return disposition.HttpResult;
        }
    }

    // Audits an OpenID error response from the single funnel, covering both the direct-return and thrown
    // paths. Error-ness of an OpenIdResult is a property of its wrapped response value, not its type.
    private static ValueTask TryPublishErrorAsync(HttpContext httpContext, object? result)
    {
        var error = result switch
        {
            IOpenIdResult { Response: IOpenIdError openIdError } => openIdError,
            ISupportOpenIdError { Error: { } supportedError } => supportedError,
            _ => null,
        };
        if (error is null)
            return ValueTask.CompletedTask;

        var feature = httpContext.Features.Get<IOpenIdContextFeature>();
        if (feature is null)
            return ValueTask.CompletedTask;

        return PublishErrorAsync(httpContext, feature.OpenIdContext, error);
    }

    private static async ValueTask PublishErrorAsync(
        HttpContext httpContext,
        OpenIdContext openIdContext,
        IOpenIdError error
    )
    {
        if (!httpContext.Items.TryAdd(ErrorPublishedKey, true))
            return;

        // Auditing must never influence the response; a failure to record is logged and swallowed.
        try
        {
            var recorder = httpContext.RequestServices.GetRequiredService<IAuditEventRecorder>();
            var endpointName = httpContext.GetEndpoint()?.DisplayName;

            await recorder.RecordOpenIdErrorAsync(
                openIdContext,
                error,
                endpointName,
                httpContext.RequestAborted
            );
        }
        catch (Exception exception)
        {
            httpContext
                .RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger<OpenIdExceptionEndpointFilter>()
                .OpenIdErrorAuditFailed(exception);
        }
    }

    private static async ValueTask<IOpenIdExceptionHandler> GetExceptionHandlerAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    )
    {
        var metadata = httpContext
            .GetEndpoint()
            ?.Metadata.GetMetadata<IOpenIdEndpointExceptionHandlerMetadata>();

        if (metadata is not null)
            return await metadata.GetExceptionHandlerAsync(
                httpContext,
                openIdEnvironment,
                cancellationToken
            );

        return httpContext.RequestServices.GetRequiredService<IOpenIdExceptionHandler>();
    }
}
