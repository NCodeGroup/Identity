// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;

namespace NCode.Identity.AspNetCore;

// See ADR-1503.
/// <summary>
/// Gives every request exactly the permitted HTTP logging fields plus the bodies that its endpoint opts in to, and
/// strips every other field that the options or the endpoint's framework metadata enable, the query string included.
/// </summary>
internal sealed class GuardedHttpLoggingInterceptor : IHttpLoggingInterceptor
{
    /// <inheritdoc />
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);

        logContext.LoggingFields =
            (logContext.LoggingFields & GuardedHttpLoggingDefaults.PermittedFields)
            | logContext.HttpContext.GetEndpoint().LoggableBodyFields;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);

        logContext.LoggingFields &=
            GuardedHttpLoggingDefaults.PermittedFields
            | logContext.HttpContext.GetEndpoint().LoggableBodyFields;

        return ValueTask.CompletedTask;
    }
}
