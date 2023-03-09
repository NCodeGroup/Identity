#region Copyright Preamble

// 
//    Copyright @ 2022 NCode Group
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
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.HttpResults;

public abstract class HttpResult : IHttpResult
{
    /// <summary>
    /// Invoked before the primary <see cref="ExecuteCoreAsync"/> method. The default implementation does nothing.
    /// </summary>
    public Func<HttpContext, ValueTask> OnExecuting { get; set; } = _ => ValueTask.CompletedTask;

    /// <summary>
    /// Invoked after the primary <see cref="ExecuteCoreAsync"/> method. The default implementation does nothing.
    /// </summary>
    public Func<HttpContext, ValueTask> OnExecuted { get; set; } = _ => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask ExecuteAsync(HttpContext httpContext)
    {
        await ExecutingAsync(httpContext);
        await ExecuteCoreAsync(httpContext);
        await ExecutedAsync(httpContext);
    }

    /// <summary>
    /// Invoked before the primary <see cref="ExecuteCoreAsync"/> method. The default implementation does nothing.
    /// </summary>
    /// <param name="httpContext"><see cref="HttpContext"/></param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected virtual ValueTask ExecutingAsync(HttpContext httpContext) => OnExecuting(httpContext);

    /// <summary>
    /// Executes the result operation of the HTTP operation asynchronously. This method is called by the framework
    /// to process the result of a HTTP operation.
    /// </summary>
    /// <param name="httpContext">The context in which the result is executed. The context information includes
    /// information about the HTTP operation that was executed and request information.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected abstract ValueTask ExecuteCoreAsync(HttpContext httpContext);

    /// <summary>
    /// Invoked after the primary <see cref="ExecuteCoreAsync"/> method. The default implementation does nothing.
    /// </summary>
    /// <param name="httpContext"><see cref="HttpContext"/></param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected virtual ValueTask ExecutedAsync(HttpContext httpContext) => OnExecuted(httpContext);
}
