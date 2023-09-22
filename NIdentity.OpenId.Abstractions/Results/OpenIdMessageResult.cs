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

using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Messages;

namespace NIdentity.OpenId.Results;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdResult"/> using <see cref="OpenIdMessage"/>
/// for any <c>OAuth</c> or<c>OpenID Connect</c> parameters.
/// </summary>
public abstract class OpenIdMessageResult : OpenIdMessage, IOpenIdResult
{
    /// <summary>
    /// Executes the result of the <c>OAuth</c> or <c>OpenID Connect</c> operation asynchronously.
    /// This method is called by the framework to process the result of an <c>OAuth</c> or <c>OpenID Connect</c> operation.
    /// The default implementation of this method calls the <see cref="ExecuteResult(OpenIdEndpointContext)"/> method and
    /// returns a completed task.
    /// </summary>
    /// <param name="context">The context in which the operation is executed. The context includes information about the
    /// operation that was executed and request information.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    public virtual ValueTask ExecuteResultAsync(OpenIdEndpointContext context, CancellationToken cancellationToken)
    {
        ExecuteResult(context);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Executes the result of the <c>OAuth</c> or <c>OpenID Connect</c> operation synchronously.
    /// This method is called by the framework to process the result of an <c>OAuth</c> or <c>OpenID Connect</c> operation.
    /// </summary>
    /// <param name="context">The context in which the operation is executed. The context includes information about the
    /// operation that was executed and request information.</param>
    protected virtual void ExecuteResult(OpenIdEndpointContext context)
    {
        // nothing
    }

    /// <summary>
    /// Helper method to get the <see cref="IOpenIdResultExecutor{T}"/> for the current <see cref="IOpenIdResult"/>.
    /// </summary>
    /// <param name="context"><see cref="OpenIdEndpointContext"/></param>
    /// <typeparam name="T">The type of <see cref="IOpenIdResult"/>.</typeparam>
    /// <returns><see cref="IOpenIdResultExecutor{T}"/></returns>
    protected static IOpenIdResultExecutor<T> GetExecutor<T>(OpenIdEndpointContext context) where T : IOpenIdResult
        => context.HttpContext.RequestServices.GetRequiredService<IOpenIdResultExecutor<T>>();
}
