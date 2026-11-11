// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Defines an interface for a service which can execute a particular kind of <see cref="IResult"/> by
/// manipulating the <see cref="HttpResponse"/>.
/// </summary>
/// <typeparam name="TResult">The type of <see cref="IResult"/>.</typeparam>
/// <remarks>
/// Implementations of <see cref="IResultExecutor{TResult}"/> are typically called by the
/// <see cref="IResult.ExecuteAsync"/> method of the corresponding action result type.
/// Implementations should be registered as singleton services.
/// </remarks>
[PublicAPI]
public interface IResultExecutor<in TResult>
    where TResult : IResult
{
    /// <summary>
    /// Asynchronously executes the <see cref="IResult"/>, by modifying the <see cref="HttpResponse"/>.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request."/></param>
    /// <param name="result">The <see cref="IResult"/> to execute.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> which represents the asynchronous operation.</returns>
    ValueTask ExecuteAsync(
        HttpContext httpContext,
        TResult result,
        CancellationToken cancellationToken
    );
}
